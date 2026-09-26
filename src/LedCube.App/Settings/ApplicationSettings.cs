using LedCube.Core.Devices;

namespace LedCube.App.Settings;

public sealed record ApplicationSettings
{
    public const double DefaultSensitivity = 100;
    public const double DefaultDecay = 15;

    public string? SelectedProfileId { get; init; }
    public string? SelectedSerialPath { get; init; }
    public int? BaudRate { get; init; }
    public string? SelectedAudioSourceId { get; init; }
    public string? SelectedVisualizerId { get; init; }
    public double Sensitivity { get; init; } = DefaultSensitivity;
    public double Decay { get; init; } = DefaultDecay;
    public bool DiagnosticsVisible { get; init; }

    public ApplicationSettings Sanitize() => this with
    {
        BaudRate = BaudRate is null ? null : Math.Clamp(BaudRate.Value, 1200, 4_000_000),
        Sensitivity = Math.Clamp(double.IsFinite(Sensitivity) ? Sensitivity : DefaultSensitivity, 0, 200),
        Decay = Math.Clamp(double.IsFinite(Decay) ? Decay : DefaultDecay, 0, 100)
    };
}

public static class SettingsSelection
{
    public static IReadOnlyList<int> AvailableBaudRates(
        CubeDeviceProfile profile,
        IReadOnlyList<int> generalBaudRates)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(generalBaudRates);
        return profile.RestrictBaudToDefault
            ? [profile.DefaultBaudRate]
            : generalBaudRates;
    }

    public static T? ResolveAvailable<T>(
        IReadOnlyList<T> available,
        string? savedId,
        Func<T, string> getId,
        Func<T, bool>? preferred = null)
    {
        ArgumentNullException.ThrowIfNull(available);
        ArgumentNullException.ThrowIfNull(getId);
        return available.FirstOrDefault(item => getId(item) == savedId)
               ?? (preferred is null ? default : available.FirstOrDefault(preferred))
               ?? available.FirstOrDefault();
    }

    public static int ResolveBaudRate(IReadOnlyList<int> available, int? savedBaudRate, int fallback)
    {
        ArgumentNullException.ThrowIfNull(available);
        if (savedBaudRate is { } saved && available.Contains(saved)) return saved;
        if (available.Contains(fallback)) return fallback;
        return available.FirstOrDefault(fallback);
    }

    public static int ResolveBaudRateForProfile(
        CubeDeviceProfile profile,
        IReadOnlyList<int> generalBaudRates,
        int? savedBaudRate)
    {
        var available = AvailableBaudRates(profile, generalBaudRates);
        return ResolveBaudRate(available, savedBaudRate, profile.DefaultBaudRate);
    }
}
