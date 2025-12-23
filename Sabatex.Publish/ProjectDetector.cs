namespace sabatex_publish;

/// <summary>
/// Detects project configuration mode (single or batch)
/// </summary>
public static class ProjectDetector
{
    private const string BatchConfigFileName = "sabatex-publish-solution.json";

    /// <summary>
    /// Detection result
    /// </summary>
    public enum DetectionMode
    {
        /// <summary>
        /// Batch mode: found sabatex-publish-solution.json
        /// </summary>
        Batch,
        
        /// <summary>
        /// Single mode: found one .csproj file
        /// </summary>
        Single,
        
        /// <summary>
        /// Error: no configuration found
        /// </summary>
        NoConfiguration,
        
        /// <summary>
        /// Error: multiple .csproj files without batch config
        /// </summary>
        AmbiguousProjects
    }

    /// <summary>
    /// Detection result with details
    /// </summary>
    public class DetectionResult
    {
        /// <summary>
        /// Detected mode
        /// </summary>
        public DetectionMode Mode { get; set; }
        
        /// <summary>
        /// Path to project file (for Single mode)
        /// </summary>
        public string? ProjectPath { get; set; }
        
        /// <summary>
        /// Path to batch config file (for Batch mode)
        /// </summary>
        public string? BatchConfigPath { get; set; }
        
        /// <summary>
        /// Found .csproj files
        /// </summary>
        public List<string> FoundProjects { get; set; } = new();
        
        /// <summary>
        /// Error message (if any)
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Recommendations for user
        /// </summary>
        public List<string> Recommendations { get; set; } = new();

        /// <summary>
        /// Check if detection was successful
        /// </summary>
        public bool IsSuccess => Mode == DetectionMode.Batch || Mode == DetectionMode.Single;
    }

    /// <summary>
    /// Detect project configuration mode in current directory
    /// </summary>
    /// <param name="directory">Directory to analyze (null = current directory)</param>
    /// <returns>Detection result</returns>
    public static DetectionResult Detect(string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();
        
        var result = new DetectionResult();
        
        // Step 1: check for batch configuration file
        var batchConfigPath = Path.Combine(directory, BatchConfigFileName);
        var hasBatchConfig = File.Exists(batchConfigPath);
        
        // Step 2: search for .csproj files
        var csprojFiles = Directory.GetFiles(directory, "*.csproj");
        result.FoundProjects = csprojFiles.ToList();
        
        // Step 3: determine mode based on findings
        if (hasBatchConfig)
        {
            // Batch mode has higher priority
            result.Mode = DetectionMode.Batch;
            result.BatchConfigPath = batchConfigPath;
            Logger.Info($"✓ Detected batch mode: {BatchConfigFileName}");
        }
        else if (csprojFiles.Length == 1)
        {
            // One project - single mode
            result.Mode = DetectionMode.Single;
            result.ProjectPath = csprojFiles[0];
            Logger.Info($"✓ Detected single project mode: {Path.GetFileName(result.ProjectPath)}");
        }
        else if (csprojFiles.Length > 1)
        {
            // Multiple projects without batch config - ambiguity
            result.Mode = DetectionMode.AmbiguousProjects;
            result.ErrorMessage = $"Found {csprojFiles.Length} .csproj files but no batch configuration.";
            result.Recommendations.Add("Create batch configuration file:");
            result.Recommendations.Add("  Run: sabatex-publish init-batch");
            result.Recommendations.Add("");
            result.Recommendations.Add("Or specify project explicitly:");
            result.Recommendations.Add("  Run: sabatex-publish --csproj <path-to-project.csproj>");
            result.Recommendations.Add("");
            result.Recommendations.Add("Found projects:");
            foreach (var proj in csprojFiles)
            {
                result.Recommendations.Add($"  - {Path.GetFileName(proj)}");
            }
        }
        else
        {
            // Nothing found
            result.Mode = DetectionMode.NoConfiguration;
            result.ErrorMessage = "No project configuration found in current directory.";
            result.Recommendations.Add("For single project:");
            result.Recommendations.Add("  • Ensure *.csproj file exists in current directory");
            result.Recommendations.Add("  • Or specify path: sabatex-publish --csproj <path>");
            result.Recommendations.Add("");
            result.Recommendations.Add("For batch publishing:");
            result.Recommendations.Add($"  • Create '{BatchConfigFileName}' file");
            result.Recommendations.Add("  • Run: sabatex-publish init-batch");
        }
        
        return result;
    }

    /// <summary>
    /// Print detection result and recommendations
    /// </summary>
    /// <param name="result">Detection result</param>
    public static void PrintDetectionResult(DetectionResult result)
    {
        if (!result.IsSuccess)
        {
            Logger.Error($"{result.ErrorMessage}");
            Logger.Info(""); // empty line
            
            if (result.Recommendations.Count > 0)
            {
                Logger.Warn("Recommendations:");
                foreach (var recommendation in result.Recommendations)
                {
                    Logger.Info(recommendation);
                }
            }
        }
    }

    /// <summary>
    /// Check if batch config file exists
    /// </summary>
    /// <param name="directory">Directory to check (null = current directory)</param>
    /// <returns>True if batch config exists</returns>
    public static bool HasBatchConfig(string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();
        var batchConfigPath = Path.Combine(directory, BatchConfigFileName);
        return File.Exists(batchConfigPath);
    }

    /// <summary>
    /// Get batch config file path
    /// </summary>
    /// <param name="directory">Directory (null = current directory)</param>
    /// <returns>Full path to batch config file</returns>
    public static string GetBatchConfigPath(string? directory = null)
    {
        directory ??= Directory.GetCurrentDirectory();
        return Path.Combine(directory, BatchConfigFileName);
    }

    /// <summary>
    /// Try to find single .csproj file in directory
    /// </summary>
    /// <param name="directory">Directory to search (null = current directory)</param>
    /// <param name="projectPath">Output: found project path</param>
    /// <returns>True if exactly one .csproj found</returns>
    public static bool TryFindSingleProject(string? directory, out string? projectPath)
    {
        directory ??= Directory.GetCurrentDirectory();
        var csprojFiles = Directory.GetFiles(directory, "*.csproj");
        
        if (csprojFiles.Length == 1)
        {
            projectPath = csprojFiles[0];
            return true;
        }
        
        projectPath = null;
        return false;
    }
}