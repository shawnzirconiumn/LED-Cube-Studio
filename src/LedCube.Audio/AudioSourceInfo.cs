namespace LedCube.Audio;

public sealed record AudioSourceInfo(
    string Id,
    string DisplayName,
    bool IsOutputMonitor = false,
    bool IsSystemDefault = false)
{
    public override string ToString() => DisplayName;
}

public static class AudioSourceSelection
{
    public static AudioSourceInfo? SelectPreferred(
        IReadOnlyList<AudioSourceInfo> sources,
        string? savedSourceId = null)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return sources.FirstOrDefault(source => source.Id == savedSourceId)
               ?? sources.FirstOrDefault(source => source.IsSystemDefault && source.IsOutputMonitor)
               ?? sources.FirstOrDefault(source => source.IsOutputMonitor);
    }
}
