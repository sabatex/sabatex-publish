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

        Option<string> solutionOption = new("--solution", new string[] { "-d" })
        {
            Description = "The solution folder path for batch publishing. If not set, uses current directory."
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
            solutionOption,
            cmdSet,
            cmdInitBatch
        };

        return rootCommand;
    }

    public static async Task<(int exitCode,bool shouldExit, string? projectFolder,string? projFile,bool migrate,bool updateService,bool updateNginx)> Process(string[] args)
    {
        var rootCommand = InitialCMD();

        string? projectFolder = null;
        string? projFile = null;
        bool migrate = false;
        bool updateService = false;
        bool updateNginx = false;

        rootCommand.SetAction(parseResult =>
        {
            migrate = parseResult.GetValue<bool>("--migrate");
            updateService = parseResult.GetValue<bool>("--updateservice");
            updateNginx = parseResult.GetValue<bool>("--updatenginx");
            projFile = parseResult.GetValue<string>("--csproj");
            projectFolder = parseResult.GetValue<string>("--solution");
        });

        var parseResult = rootCommand.Parse(args);

        if (parseResult.Errors.Count > 0)
        {
            Logger.Error("Error parse arguments:");
            foreach (var error in parseResult.Errors)
            {
                Logger.Error(error.Message);
            }
            return (1,true,projectFolder,projFile,migrate,updateService,updateService);
        }

        await parseResult.InvokeAsync();

        // check if special command was invoked (help, set, init-batch)
        var actionType = parseResult.Action?.GetType() ?? typeof(object);
        //# Line 122 warning - check Command.Name
        if (actionType == typeof(System.CommandLine.Help.HelpAction) || 
            actionType == typeof(SetAction) ||
            (parseResult.CommandResult.Command.Name != null && parseResult.CommandResult.Command.Name == "init-batch"))
        {
            return (exitCode: 0, shouldExit: true, null, null, false, false, false);
        }

        return (0,false, projectFolder, projFile, migrate, updateService, updateNginx);
    }
}

