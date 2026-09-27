using LedCube.App.Settings;
using LedCube.Core.Visualization.Builder;

namespace LedCube.App.VisualizerBuilder;

public sealed record SavedVisualizerInfo(string Name, string Path)
{
    public override string ToString() => Name;
}

public sealed class JsonVisualizerDefinitionStore
{
    private readonly string _directory;

    public JsonVisualizerDefinitionStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Path.GetDirectoryName(JsonApplicationSettingsStore.GetDefaultPath())!, "visualizers");
    }

    public string DirectoryPath => _directory;

    public async Task<IReadOnlyList<SavedVisualizerInfo>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory)) return [];
        var result = new List<SavedVisualizerInfo>();
        try
        {
            foreach (var path in Directory.EnumerateFiles(_directory, "*.json").Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var definition = await LoadAsync(path, cancellationToken).ConfigureAwait(false);
                    result.Add(new SavedVisualizerInfo(definition.Name, path));
                }
                catch (VisualizerDefinitionException) { }
                catch (IOException) { }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
        return result;
    }

    public async Task<VisualizerDefinition> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureManagedPath(path);
        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var definition = VisualizerDefinitionSerializer.Deserialize(json);
            VisualizerDefinitionValidator.ThrowIfInvalid(definition);
            return definition;
        }
        catch (VisualizerDefinitionException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new VisualizerDefinitionException($"Could not open '{Path.GetFileName(path)}': {exception.Message}", exception);
        }
    }

    public async Task<string> SaveAsync(VisualizerDefinition definition, string? existingPath = null,
        CancellationToken cancellationToken = default)
    {
        VisualizerDefinitionValidator.ThrowIfInvalid(definition);
        Directory.CreateDirectory(_directory);
        var path = existingPath ?? UniquePath(definition.Name);
        EnsureManagedPath(path);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, VisualizerDefinitionSerializer.Serialize(definition), cancellationToken)
                .ConfigureAwait(false);
            File.Move(temporaryPath, path, true);
            return path;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private string UniquePath(string name)
    {
        var slug = string.Concat(name.Trim().Select(character =>
            char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-')).Trim('-');
        if (string.IsNullOrWhiteSpace(slug)) slug = "visualizer";
        var path = Path.Combine(_directory, $"{slug}.json");
        for (var suffix = 2; File.Exists(path); suffix++) path = Path.Combine(_directory, $"{slug}-{suffix}.json");
        return path;
    }

    private void EnsureManagedPath(string path)
    {
        var root = Path.GetFullPath(_directory) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!fullPath.StartsWith(root, comparison))
            throw new VisualizerDefinitionException("Visualizer files must be inside the application's visualizers directory.");
    }
}
