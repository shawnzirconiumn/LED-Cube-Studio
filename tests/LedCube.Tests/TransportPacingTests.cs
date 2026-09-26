using LedCube.App.Settings;
using LedCube.Core.Devices;
using LedCube.Hardware.Transport;

namespace LedCube.Tests;

public sealed class TransportPacingTests
{
    [Fact]
    public void WireTimeUsesTenBitsPerByteAndAddsGuard()
    {
        var pacing = FrameTransportPacing.ForWireTime(
            197, 115200, TimeSpan.FromMilliseconds(1));

        Assert.NotNull(pacing.MinimumPacketStartInterval);
        Assert.InRange(pacing.MinimumPacketStartInterval.Value.TotalMilliseconds, 18.1005, 18.1008);
    }

    [Fact]
    public void Rgb8x8x8ProfileDefaultsToValidatedBaudAndOptimizedPacing()
    {
        var profile = DeviceProfiles.All.Single(candidate =>
            candidate.Id == DeviceProfiles.Rgb8x8x8ProfileId);
        var pacing = FrameTransportPacing.ForProfile(profile);

        Assert.Equal(115200, profile.DefaultBaudRate);
        Assert.True(profile.RestrictBaudToDefault);
        Assert.InRange(pacing.MinimumPacketStartInterval!.Value.TotalMilliseconds, 18.1005, 18.1008);
        Assert.Equal(55, profile.FrameSchedulerInterval!.Value.TotalMilliseconds);
    }

    [Fact]
    public void UnsupportedExperimentalBaudRatesAreNotExposedForRgb8x8x8()
    {
        var profile = DeviceProfiles.All.Single(candidate =>
            candidate.Id == DeviceProfiles.Rgb8x8x8ProfileId);
        int[] generalRates = [9600, 57600, 115200, 230400, 460800, 2_000_000];

        var available = SettingsSelection.AvailableBaudRates(profile, generalRates);

        Assert.Equal([115200], available);
        Assert.DoesNotContain(460800, available);
        Assert.DoesNotContain(2_000_000, available);
    }

    [Fact]
    public void BaudRestrictionIsScopedToValidatedRgb8x8x8Profile()
    {
        var futureProfile = DeviceProfiles.All.Single(candidate => candidate.Id == "rgb-8x8x32");
        int[] generalRates = [115200, 460800, 2_000_000];

        var available = SettingsSelection.AvailableBaudRates(futureProfile, generalRates);

        Assert.Equal(generalRates, available);
    }

    [Theory]
    [InlineData(0, 115200)]
    [InlineData(197, 0)]
    public void InvalidWireParametersAreRejected(int bytes, int baudRate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            FrameTransportPacing.ForWireTime(bytes, baudRate, TimeSpan.Zero));
    }
}
