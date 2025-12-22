// file : CommandProcessor.cs
using Sabatex.Publish;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sabatex_publish;

public static class CommandProcessor
{
    static RootCommand InitialCMD()
    {
        Option<bool> migrateOption = new("--migrate", new string[] { "-m" })
        {
            Description = "The migrate database after publish project."
        };

        Option<bool> updateServiceOption = new("--updateservice", new string[] { "-s" })
        {
            Description = "Update service file on linux server."
        };

        Option<bool> updateNginxOption = new("--updatenginx", new string[] { "-n" })
        {
            Description = "Update nginx config on linux server."
        };

        Option<string> projFileOption = new("--csproj", new string[] { "-p" })
        {
            Description = "The csproj file path. If not set, the program will auto-detect mode (batch or single)."
        };

        var cmdSet = new Command("set", "Set global values")
        {
            new Option<string>("--NuGetDebugPackagePath", "-p")
            {
                Description = "Path to NuGet DebugPackagePath, example: /home/user/.nuget/packages/sabatex.publish/1.0.0/debug"
            },
            new Option<string>("--NuGetKeyPath", "-k")
            {
                Description = "The path to NUGET Key, example: /home/user/.NUGETKEY"
            },
            new Option<bool>("--shared", "-s")
            {
                Description = "Save globally (OneDrive)"
            }
        };

        cmdSet.Action = new SetAction();

        // NEW COMMAND: init-batch
        var cmdInitBatch = new Command("init-batch", "Create sample batch publishing configuration file")
        {
            new Option<string>("--output", new string[] { "-o" })
            {
                Description = "Output path for configuration file (default: sabatex-publish-solution.json)"
            }
        };

        cmdInitBatch.SetAction(parseResult =>
        {
            var outputPath = parseResult.GetValue<string>("--output");
            BatchPublisher.CreateSampleConfig(outputPath);
        });

        RootCommand rootCommand = new("Sabatex publish tool - automatic publishing for .NET projects")
        {
            migrateOption,
            updateServiceOption,
            updateNginxOption,
            projFileOption,
            cmdSet,
            cmdInitBatch  // add new command
        };

        return rootCommand;
    }

    public static async Task<(int exitCode, bool shouldExit)> Process(string[] args, SabatexSettings settings)
    {
        var rootCommand = InitialCMD();

        rootCommand.SetAction(parseResult =>
        {
            settings.Migrate = parseResult.GetValue<bool>("--migrate");
            settings.UpdateService = parseResult.GetValue<bool>("--updateservice");
            settings.UpdateNginx = parseResult.GetValue<bool>("--updatenginx");
            settings.ProjFile = parseResult.GetValue<string>("--csproj");
        });

        var parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            Logger.Error("Error parse arguments:");
            foreach (var error in parseResult.Errors)
            {
                Logger.Error(error.Message);
            }
            return (exitCode: 1, shouldExit: true);
        }

        await parseResult.InvokeAsync();

        // check if special command was invoked (help, set, init-batch)
        var actionType = parseResult.Action.GetType();
        if (actionType == typeof(System.CommandLine.Help.HelpAction) || 
            actionType == typeof(SetAction) ||
            parseResult.CommandResult.Command.Name == "init-batch")
        {
            return (exitCode: 0, shouldExit: true);
        }

        // NEW LOGIC: automatic mode detection
        if (settings.ProjFile == null)
        {
            // use ProjectDetector for smart mode detection
            var detection = ProjectDetector.Detect();

            if (!detection.IsSuccess)
            {
                // detection error - print recommendations
                ProjectDetector.PrintDetectionResult(detection);
                return (exitCode: 2, shouldExit: true);
            }

            if (detection.Mode == ProjectDetector.DetectionMode.Batch)
            {
                // found batch configuration - start batch publishing
                Logger.Info("Starting batch publishing mode...");
                Console.WriteLine();

                var exitCode = await BatchPublisher.PublishAllAsync(
                    detection.BatchConfigPath,
                    settings.Migrate,
                    settings.UpdateService,
                    settings.UpdateNginx);

                return (exitCode: exitCode, shouldExit: true);
            }
            else if (detection.Mode == ProjectDetector.DetectionMode.Single)
            {
                // found one project - use it
                settings.ProjFile = detection.ProjectPath;
                Logger.Info($"Auto-detected single project: {Path.GetFileName(settings.ProjFile)}");
            }
        }
        else
        {
            // explicitly specified --csproj, check existence
            if (!File.Exists(settings.ProjFile))
            {
                Logger.Error($"Specified project file not found: {settings.ProjFile}");
                return (exitCode: 4, shouldExit: true);
            }
            Logger.Info($"Using specified project: {Path.GetFileName(settings.ProjFile)}");
        }

        // continue with single-project mode
        return (exitCode: 0, shouldExit: false);
    }
}

