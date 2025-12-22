using System.Text.Json.Serialization;

namespace sabatex_publish;

/// <summary>
/// Configuration for batch publishing multiple projects
/// </summary>
public class SolutionPublishConfig
{
    /// <summary>
    /// List of projects to publish
    /// </summary>
    [JsonPropertyName("projects")]
    public List<ProjectConfig> Projects { get; set; } = new();

    /// <summary>
    /// Base directory (встановлюється програмно з розташування конфігураційного файлу)
    /// </summary>
    [JsonIgnore]
    public string? BaseDirectory { get; set; }

    /// <summary>
    /// Get only enabled projects
    /// </summary>
    /// <returns>List of enabled project paths</returns>
    public IEnumerable<string> GetEnabledProjectPaths()
    {
        return Projects
            .Where(p => p.Enabled)
            .Select(p => p.Path);
    }
}

/// <summary>
/// Single project configuration
/// </summary>
public class ProjectConfig
{
    /// <summary>
    /// Relative path to .csproj file from solution config location
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Enable or disable project publishing
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}