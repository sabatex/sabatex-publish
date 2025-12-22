using System.Text.Json;
using Sabatex.Publish;

namespace sabatex_publish;

/// <summary>
/// Handles batch publishing of multiple projects
/// </summary>
public static class BatchPublisher
{
    private const string DefaultConfigFileName = "sabatex-publish-solution.json";

    /// <summary>
    /// Load solution publish configuration from file
    /// </summary>
    /// <param name="configPath">Path to config file or null for default</param>
    /// <returns>Loaded configuration</returns>
    public static SolutionPublishConfig LoadConfig(string? configPath = null)
    {
        // use default file if not specified
        configPath ??= Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"Solution publish config not found: {configPath}. " +
                $"Create '{DefaultConfigFileName}' with project paths or run: sabatex-publish init-batch");
        }

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<SolutionPublishConfig>(json)
            ?? throw new Exception("Failed to parse solution config");

        // set base directory from config file location
        config.BaseDirectory = Path.GetDirectoryName(Path.GetFullPath(configPath))
            ?? Directory.GetCurrentDirectory();

        // validation
        ValidateConfig(config);

        return config;
    }

    /// <summary>
    /// Validate configuration
    /// </summary>
    private static void ValidateConfig(SolutionPublishConfig config)
    {
        if (config.Projects.Count == 0)
        {
            throw new Exception("Configuration must contain at least one project");
        }

        var enabledCount = config.Projects.Count(p => p.Enabled);
        if (enabledCount == 0)
        {
            throw new Exception("All projects are disabled. Enable at least one project in configuration");
        }
    }

    /// <summary>
    /// Publish all enabled projects from configuration
    /// </summary>
    /// <param name="configPath">Path to config file or null for default</param>
    /// <param name="migrate">Run database migrations</param>
    /// <param name="updateService">Update systemd service files</param>
    /// <param name="updateNginx">Update Nginx configuration</param>
    /// <returns>Exit code (0 = success, non-zero = errors)</returns>
    public static async Task<int> PublishAllAsync(
        string? configPath = null,
        bool migrate = false,
        bool updateService = false,
        bool updateNginx = false)
    {
        var config = LoadConfig(configPath);
        
        var enabledProjects = config.GetEnabledProjectPaths().ToList();
        
        Logger.Info($"📦 Batch publishing {enabledProjects.Count} project(s)...");
        Logger.Info($"📁 Base directory: {config.BaseDirectory}");
        Console.WriteLine();

        int successCount = 0;
        int failedCount = 0;
        var failedProjects = new List<(string path, string error)>();

        for (int i = 0; i < enabledProjects.Count; i++)
        {
            var projectPath = enabledProjects[i];
            var fullPath = Path.IsPathRooted(projectPath)
                ? projectPath
                : Path.Combine(config.BaseDirectory!, projectPath);

            Logger.Info($"[{i + 1}/{enabledProjects.Count}] Publishing: {projectPath}");
            Console.WriteLine(new string('=', 60));

            try
            {
                var exitCode = await PublishSingleProjectAsync(
                    fullPath,
                    migrate,
                    updateService,
                    updateNginx);

                if (exitCode == 0)
                {
                    successCount++;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Logger.Info($"✓ Success: {projectPath}");
                    Console.ResetColor();
                }
                else
                {
                    failedCount++;
                    failedProjects.Add((projectPath, $"Exit code: {exitCode}"));
                    Console.ForegroundColor = ConsoleColor.Red;
                    Logger.Error($"✗ Failed: {projectPath} (exit code: {exitCode})");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                failedCount++;
                failedProjects.Add((projectPath, ex.Message));
                Console.ForegroundColor = ConsoleColor.Red;
                Logger.Error($"✗ Error: {projectPath}");
                Logger.Error($"  {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        // print summary
        PrintSummary(successCount, failedCount, failedProjects);

        return failedCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Publish single project (delegates to Program class)
    /// </summary>
    private static async Task<int> PublishSingleProjectAsync(
        string projFilePath,
        bool migrate,
        bool updateService,
        bool updateNginx)
    {
        // create temporary settings for project
        var settings = new SabatexSettings
        {
            ProjFile = projFilePath,
            Migrate = migrate,
            UpdateService = updateService,
            UpdateNginx = updateNginx
        };

        // validate project
        if (!File.Exists(settings.ProjFile))
        {
            Logger.Error($"Project file not found: {settings.ProjFile}");
            return 1;
        }

        // resolve project configuration
        var errorCode = settings.ResolveConfig();
        if (errorCode != 0)
        {
            return errorCode;
        }

        // call publishing through Program (added in Step 4)
        return await Program.PublishProjectAsync(settings);
    }

    /// <summary>
    /// Print batch publishing summary
    /// </summary>
    private static void PrintSummary(
        int successCount, 
        int failedCount, 
        List<(string path, string error)> failedProjects)
    {
        Console.WriteLine(new string('=', 60));
        Logger.Info("📊 Batch Publish Summary:");
        
        Console.ForegroundColor = ConsoleColor.Green;
        Logger.Info($"  ✓ Successful: {successCount}");
        
        if (failedCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Logger.Error($"  ✗ Failed: {failedCount}");
            Console.ResetColor();
            Logger.Error("  Failed projects:");
            foreach (var (path, error) in failedProjects)
            {
                Logger.Error($"    - {path}");
                Logger.Error($"      Reason: {error}");
            }
        }
        
        Console.ResetColor();
    }

    /// <summary>
    /// Create sample configuration file
    /// </summary>
    /// <param name="path">Output path or null for default</param>
    public static void CreateSampleConfig(string? path = null)
    {
        path ??= Path.Combine(Directory.GetCurrentDirectory(), DefaultConfigFileName);

        if (File.Exists(path))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Logger.Warn($"Configuration file already exists: {path}");
            Console.ResetColor();
            Console.Write("Overwrite? (y/n): ");
            var answer = Console.ReadLine()?.ToLower();
            if (answer != "y" && answer != "yes")
            {
                Logger.Info("Operation cancelled");
                return;
            }
        }

        var sampleConfig = new SolutionPublishConfig
        {
            Projects = new List<ProjectConfig>
            {
                new ProjectConfig 
                { 
                    Path = "Example.Library1/Example.Library1.csproj",
                    Enabled = true
                },
                new ProjectConfig 
                { 
                    Path = "Example.Library2/Example.Library2.csproj",
                    Enabled = true
                },
                new ProjectConfig 
                { 
                    Path = "Example.WebApp/Example.WebApp.csproj",
                    Enabled = false
                }
            }
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(sampleConfig, options);
        File.WriteAllText(path, json);
        
        Console.ForegroundColor = ConsoleColor.Green;
        Logger.Info($"✓ Sample configuration created: {path}");
        Console.ResetColor();
        Logger.Info("  Edit this file to add your project paths.");
        Logger.Info($"  Relative paths are resolved from: {Path.GetDirectoryName(Path.GetFullPath(path))}");
    }
}