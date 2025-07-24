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
            Description = "The csproj file path. If not set, the program search *.csproj in current directory."
        };
 
        var cmdSet = new Command("set",  "Set global values")
        {
            new Option<string>("--NuGetDebugPackagePath","-p")
            {
                Description = "Шлях до NuGet DebugPackagePath, наприклад: /home/user/.nuget/packages/sabatex.publish/1.0.0/debug"
            },
            new Option<string>("--NuGetKeyPath","-k")
            {
                Description = "The path to NUGET Key , наприклад: /home/user/.NUGETKEY"
            },

            // опція: глобально чи локально
            new Option<bool>("--shared", "-s")
            {
                Description = "Записати глобально (OneDrive)"
            }
        };

        cmdSet.Action = new SetAction();
        //a   SetAction(typeof(SetAction)) parseResult =>GlobalConfigManager.SetValue(parseResult.GetValue<string>("--NuGetDebugPackagePath"),
        //                                                            parseResult.GetValue<string>("--NuGetKeyPath"),
        //                                                            parseResult.GetValue<bool>("--shared")));
        RootCommand rootCommand = new("Sabatex publish tool")
        {
            migrateOption,
            updateServiceOption,
            updateNginxOption,
            projFileOption,
            cmdSet
        };

        return rootCommand;
    }


    public static async Task<(int exitCode, bool shouldExit)> Process(string[] args, SabatexSettings settings)
    {
        var help = false;
        var rootCommand = InitialCMD();

        rootCommand.SetAction(parseResult =>
        {
            settings.Migrate = parseResult.GetValue<bool>("--migrate");
            settings.UpdateService = parseResult.GetValue<bool>("--updateservice");
            settings.UpdateNginx = parseResult.GetValue<bool>("--updatenginx");
            settings.ProjFile = parseResult.GetValue<string>("--csproj");
            help = parseResult.GetValue<bool>("--help");
            
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

        if (parseResult.Action.GetType() == typeof(System.CommandLine.Help.HelpAction) || parseResult.Action.GetType() == typeof(SetAction))
        {
            return (exitCode: 0, shouldExit: true);
        }
        
        
        if (settings.ProjFile == null)
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            if (files.Length == 0)
            {
                Logger.Error("The Current directory must contains  *.csproj file");
                return (exitCode: 2, shouldExit: true);
            }
            if (files.Length > 1)
            {
                Logger.Error("The Current directory must contains only one *.csproj file");
                return (exitCode: 3, shouldExit: true);
            }
            settings.ProjFile = files[0];
        }


        return (exitCode: 0,shouldExit:false);

    }

}

