using System.Numerics;
using LedCube.Core.Diagnostics;

namespace LedCube.Core.Audio;

/// <summary>
/// Shared 2048-sample, 50%-overlapped FFT analysis adapted from the proven WinForms music path.
/// </summary>
public sealed class SpectrumAnalyzer
{
    public const int FftSize = 2048;
    public const int HopSize = FftSize / 2;
    public const int BandCount = 8;

    private const double NoiseFloorDb = -72;
    private const double DynamicRangeDb = 60;
    private const double SlowReleaseHalfLifeMilliseconds = 1500;
    private const double FastReleaseHalfLifeMilliseconds = 75;
    private static readonly double[] BandEdges = [50, 100, 200, 400, 800, 1600, 3200, 6400, 12800];
    private readonly object _sync = new();
    private readonly float[] _leftSamples = new float[FftSize];
    private readonly float[] _rightSamples = new float[FftSize];
    private readonly double[] _smoothedLeft = new double[BandCount];
    private readonly double[] _smoothedRight = new double[BandCount];
    private readonly double[] _smoothedCombined = new double[BandCount];
    private int _writeIndex;
    private int _collected;
    private int _sinceTransform;
    private double _bassFast;
    private double _bassSlow;
    private double _beatEnvelope;
    private long _lastSmoothingAudioTimestamp;
    private DateTimeOffset _lastBeat = DateTimeOffset.MinValue;
    private AudioAnalysisSnapshot _latest = AudioAnalysisSnapshot.Silence;

    public int SensitivityPercent { get; set; } = 100;
    public int DecayPercent { get; set; } = 15;

    public AudioAnalysisSnapshot Latest
    {
        get { lock (_sync) return _latest; }
    }

    public event EventHandler<AudioAnalysisSnapshot>? SnapshotAvailable;

    public void Process(AudioBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.SampleRate <= 0)
            throw new ArgumentOutOfRangeException(nameof(buffer), "Sample rate must be positive.");

        var count = buffer.SampleCount;
        if (count == 0)
            return;
        var analyzerSubmittedTimestamp = PipelineClock.GetTimestamp();
        var captureReceivedTimestamp = buffer.CaptureReceivedTimestamp > 0
            ? buffer.CaptureReceivedTimestamp
            : analyzerSubmittedTimestamp;
        var audioDataEndTimestamp = buffer.AudioDataTimestamp > 0
            ? buffer.AudioDataTimestamp
            : captureReceivedTimestamp;

        double leftSquares = 0;
        double rightSquares = 0;
        for (var i = 0; i < count; i++)
        {
            var left = Math.Clamp(buffer.Left[i], -1, 1);
            var right = Math.Clamp(buffer.Right[i], -1, 1);
            leftSquares += left * left;
            rightSquares += right * right;
            _leftSamples[_writeIndex] = left;
            _rightSamples[_writeIndex] = right;
            _writeIndex = (_writeIndex + 1) % FftSize;

            if (_collected < FftSize)
            {
                _collected++;
                if (_collected == FftSize)
                {
                    _sinceTransform = 0;
                    AnalyzeWindow(buffer.SampleRate, Math.Sqrt(leftSquares / count), Math.Sqrt(rightSquares / count),
                        WindowCenterTimestamp(audioDataEndTimestamp, i, count, buffer.SampleRate),
                        captureReceivedTimestamp, analyzerSubmittedTimestamp);
                }
            }
            else if (++_sinceTransform >= HopSize)
            {
                _sinceTransform -= HopSize;
                AnalyzeWindow(buffer.SampleRate, Math.Sqrt(leftSquares / count), Math.Sqrt(rightSquares / count),
                    WindowCenterTimestamp(audioDataEndTimestamp, i, count, buffer.SampleRate),
                    captureReceivedTimestamp, analyzerSubmittedTimestamp);
            }
        }
    }

    private void AnalyzeWindow(int sampleRate, double leftRms, double rightRms,
        long audioDataTimestamp, long captureReceivedTimestamp, long analyzerSubmittedTimestamp)
    {
        var left = new Complex[FftSize];
        var right = new Complex[FftSize];
        double windowSquares = 0;
        for (var i = 0; i < FftSize; i++)
        {
            var source = (_writeIndex + i) % FftSize;
            var window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (FftSize - 1)));
            windowSquares += window * window;
            left[i] = new Complex(_leftSamples[source] * window, 0);
            right[i] = new Complex(_rightSamples[source] * window, 0);
        }

        Transform(left);
        Transform(right);
        var scale = 4.0 * FftSize / windowSquares;
        var binWidth = sampleRate / (double)FftSize;
        var leftLevels = new double[BandCount];
        var rightLevels = new double[BandCount];
        var combined = new double[BandCount];

        for (var band = 0; band < BandCount; band++)
        {
            var firstBin = Math.Min(FftSize / 2, Math.Max(1, (int)Math.Ceiling(BandEdges[band] / binWidth)));
            var lastBin = Math.Min(FftSize / 2, Math.Max(firstBin + 1,
                (int)Math.Ceiling(BandEdges[band + 1] / binWidth)));
            var leftPower = CalculatePower(left, firstBin, lastBin, scale);
            var rightPower = CalculatePower(right, firstBin, lastBin, scale);
            leftLevels[band] = PowerToLevel(leftPower);
            rightLevels[band] = PowerToLevel(rightPower);
            combined[band] = PowerToLevel((leftPower + rightPower) * 0.5);
        }

        var bassPower = (CalculateFrequencyPower(left, binWidth, scale, 50, 180) +
                         CalculateFrequencyPower(right, binWidth, scale, 50, 180)) * 0.5;
        var fftCompletedTimestamp = PipelineClock.GetTimestamp();
        Publish(leftLevels, rightLevels, combined, bassPower, leftRms, rightRms,
            new AudioPipelineTiming(audioDataTimestamp, captureReceivedTimestamp,
                analyzerSubmittedTimestamp, fftCompletedTimestamp, 0),
            TimeSpan.FromSeconds(HopSize / (double)sampleRate));
    }

    private void Publish(double[] left, double[] right, double[] combined, double bassPower,
        double leftRms, double rightRms, AudioPipelineTiming pipelineTiming,
        TimeSpan nominalUpdateInterval)
    {
        _bassFast = _bassFast == 0 ? bassPower : _bassFast * 0.72 + bassPower * 0.28;
        _bassSlow = _bassSlow == 0 ? bassPower : _bassSlow * 0.98 + bassPower * 0.02;
        var now = DateTimeOffset.UtcNow;
        var ratio = _bassSlow > 1e-12 ? _bassFast / _bassSlow : 0;
        var beat = bassPower > 1e-10 && ratio > 1.45 && now - _lastBeat >= TimeSpan.FromMilliseconds(120);
        if (beat)
        {
            _beatEnvelope = Math.Clamp((ratio - 1) / 1.5, 0.35, 1);
            _lastBeat = now;
        }
        else
        {
            _beatEnvelope *= 0.82;
        }

        var releaseElapsed = _lastSmoothingAudioTimestamp > 0
            ? PipelineClock.Elapsed(_lastSmoothingAudioTimestamp, pipelineTiming.AudioDataTimestamp)
            : nominalUpdateInterval;
        if (releaseElapsed <= TimeSpan.Zero)
            releaseElapsed = nominalUpdateInterval;
        _lastSmoothingAudioTimestamp = pipelineTiming.AudioDataTimestamp;
        var release = ReleaseCoefficient(DecayPercent, releaseElapsed);
        for (var i = 0; i < BandCount; i++)
        {
            _smoothedLeft[i] = Smooth(_smoothedLeft[i], left[i], release);
            _smoothedRight[i] = Smooth(_smoothedRight[i], right[i], release);
            _smoothedCombined[i] = Smooth(_smoothedCombined[i], combined[i], release);
        }

        var publishedTimestamp = PipelineClock.GetTimestamp();
        var snapshot = new AudioAnalysisSnapshot(
            Math.Sqrt((leftRms * leftRms + rightRms * rightRms) * 0.5), leftRms, rightRms,
            (double[])_smoothedCombined.Clone(), (double[])_smoothedLeft.Clone(),
            (double[])_smoothedRight.Clone(),
            _smoothedCombined.Take(2).Max(), _smoothedCombined.Skip(2).Take(3).Average(),
            _smoothedCombined.Skip(5).Take(3).Average(), beat, _beatEnvelope, now)
        {
            PipelineTiming = pipelineTiming with { SnapshotPublishedTimestamp = publishedTimestamp },
            SensitivityPercent = Math.Clamp(SensitivityPercent, 0, 200)
        };
        lock (_sync) _latest = snapshot;
        SnapshotAvailable?.Invoke(this, snapshot);
    }

    private double PowerToLevel(double power)
    {
        var db = 10 * Math.Log10(Math.Max(power, 1e-12));
        var normalized = Math.Clamp((db - NoiseFloorDb) / DynamicRangeDb, 0, 1);
        return Math.Clamp(normalized * Math.Clamp(SensitivityPercent, 0, 200) / 100.0, 0, 1);
    }

    private static double Smooth(double previous, double current, double release) =>
        previous + (current - previous) * (current >= previous ? 0.65 : release);

    public static TimeSpan ReleaseHalfLife(int decayPercent)
    {
        var position = Math.Clamp(decayPercent, 0, 100) / 100d;
        var milliseconds = SlowReleaseHalfLifeMilliseconds * Math.Pow(
            FastReleaseHalfLifeMilliseconds / SlowReleaseHalfLifeMilliseconds, position);
        return TimeSpan.FromMilliseconds(milliseconds);
    }

    public static double ReleaseCoefficient(int decayPercent, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero) return 0;
        var halfLifeSeconds = ReleaseHalfLife(decayPercent).TotalSeconds;
        return 1 - Math.Exp(-Math.Log(2) * elapsed.TotalSeconds / halfLifeSeconds);
    }

    private static long SampleTimestamp(long audioDataEndTimestamp, int sampleIndex, int sampleCount,
        int sampleRate) => PipelineClock.Add(audioDataEndTimestamp,
        TimeSpan.FromSeconds((sampleIndex - sampleCount + 1) / (double)sampleRate));

    private static long WindowCenterTimestamp(long audioDataEndTimestamp, int sampleIndex, int sampleCount,
        int sampleRate) => PipelineClock.Add(
        SampleTimestamp(audioDataEndTimestamp, sampleIndex, sampleCount, sampleRate),
        TimeSpan.FromSeconds(-FftSize / (2d * sampleRate)));

    private static double CalculateFrequencyPower(Complex[] fft, double binWidth, double scale,
        double low, double high)
    {
        var first = Math.Max(1, (int)Math.Ceiling(low / binWidth));
        var last = Math.Min(FftSize / 2, (int)Math.Ceiling(high / binWidth));
        return CalculatePower(fft, first, last, scale);
    }

    private static double CalculatePower(Complex[] fft, int first, int last, double scale)
    {
        double power = 0;
        for (var bin = first; bin < last && bin < FftSize / 2; bin++)
            power += fft[bin].Magnitude * fft[bin].Magnitude;
        return power * scale;
    }

    private static void Transform(Complex[] data)
    {
        var n = data.Length;
        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;
            for (; (j & bit) != 0; bit >>= 1) j ^= bit;
            j ^= bit;
            if (i < j) (data[i], data[j]) = (data[j], data[i]);
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var root = Complex.FromPolarCoordinates(1, -2 * Math.PI / length);
            for (var start = 0; start < n; start += length)
            {
                var factor = Complex.One;
                for (var offset = 0; offset < length / 2; offset++)
                {
                    var even = data[start + offset];
                    var odd = data[start + offset + length / 2] * factor;
                    data[start + offset] = even + odd;
                    data[start + offset + length / 2] = even - odd;
                    factor *= root;
                }
            }
        }

        for (var i = 0; i < n; i++) data[i] /= n;
    }
}
