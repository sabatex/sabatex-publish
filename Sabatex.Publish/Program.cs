﻿using Microsoft.Extensions.Configuration;
using Sabatex.Publish;
using sabatex_publish;
using System.CommandLine;
using System.Reflection.Metadata;

namespace sabatex_publish;

public class Program
{

    static SabatexSettings settings;

    static LinuxScriptShell linuxScriptShell => new LinuxScriptShell(settings.TempFolder, settings.Linux.BitviseTlpFile);
    static LocalScriptShell localScriptShell => new LocalScriptShell(settings.TempPublishProjectFolder);

    static async Task PackNugetAsync()
    {
        string includeSource = settings.IsPreRelease ? "--include-source" : string.Empty;
        string script = $"dotnet pack --configuration {settings.BuildConfiguration} {includeSource} --output \"{settings.OutputPath}\" \"{settings.ProjFile}\" ";
        if (!await localScriptShell.RunAsync(script))
            throw new Exception("Error build project!");

    }

    static async Task<bool> Error(string message)
    {
        Console.WriteLine(message);
        await Task.Yield();
        return false;
    }

    static void Build()
    {
        if (Directory.Exists(settings.TempPublishProjectFolder))
        {
            Directory.Delete(settings.TempPublishProjectFolder, true);
        }
        Directory.CreateDirectory(settings.TempPublishProjectFolder);
        string script = $"dotnet publish {settings.ProjectFolder}/{settings.ProjectName}.csproj --configuration Release  -o {settings.TempPublishProjectFolder}";
        if (!localScriptShell.Run(script, settings.ProjectFolder))
            throw new Exception("Error build project!");
    }

    static async Task PutStringAsFileAsync(string text, string linuxDestinationPath, string fileName)
    {
        await System.IO.File.WriteAllTextAsync($"{settings.TempFolder}/{fileName}", text);
        linuxScriptShell.PutFile(settings.TempFolder, settings.Linux.TempProjectFolder, fileName);
        System.IO.File.Delete($"{settings.TempFolder}/{fileName}");
        if (!linuxScriptShell.Move($"{settings.TempFolder}/{fileName}", $"{settings.TempFolder}/{fileName}", true))
            throw new Exception($"Error move file {settings.TempFolder}/{fileName} to {settings.TempFolder}/{fileName}");

    }
    static async Task PutStringAsFileAsync(IEnumerable<string> text, string linuxDestinationPath, string fileName)
    {
        await System.IO.File.WriteAllLinesAsync($"{settings.TempFolder}/{fileName}", text);
        linuxScriptShell.PutFile(settings.TempFolder, settings.Linux.TempProjectFolder, fileName);
        System.IO.File.Delete($"{settings.TempFolder}/{fileName}");
        if (!linuxScriptShell.Move($"{settings.Linux.TempProjectFolder}/{fileName}", $"{linuxDestinationPath}/{fileName}", true))
            throw new Exception($"Error move file {{settings.Linux.TempProjectFolder}}/{{fileName to {linuxDestinationPath}/{fileName}");

    }

    static void PutTolinux()
    {

        var tarFileName = settings.Linux.TarFileName;
        var tarFilePath = $"{settings.TempFolder}/{tarFileName}";
        var projectName = settings.ProjectName;
        var tempFolder = settings.TempFolder;
        if (string.IsNullOrWhiteSpace(settings.Linux.TempFolder))
            throw new NullReferenceException(nameof(settings.Linux.TempFolder));
        var linuxTempFolder = settings.Linux.TempFolder;

        if (File.Exists(tarFilePath))
            File.Delete(tarFilePath);


        // pack project
        if (!localScriptShell.Run($"tar -czvf {tarFileName} {projectName}", tempFolder))
            throw new Exception("Error pack !");
        // create temp linux folder
        if (!linuxScriptShell.DirectoryExist(linuxTempFolder))
        {
            if (!linuxScriptShell.Mkdir(linuxTempFolder))
                throw new Exception($"Error create folder {linuxTempFolder} !");
        }
        // remove old folder  
        if (linuxScriptShell.DirectoryExist(settings.Linux.TempProjectFolder))
        {
            if (!linuxScriptShell.RemoveFolder(settings.Linux.TempProjectFolder))
                throw new Exception($"Error delete {settings.Linux.TempProjectFolder}");
        }

        linuxScriptShell.PutFile(tempFolder, linuxTempFolder, tarFileName);
        linuxScriptShell.UnPack(linuxTempFolder, tarFileName);
        Directory.Delete($"{tempFolder}/{projectName}", true);
    }

    static void UpdateBlazorwasm()
    {
        var publishFolder = settings.Linux.PublishFolder;
        if (string.IsNullOrWhiteSpace(settings.Linux.TempProjectFolder))
            throw new NullReferenceException(nameof(Linux.TempProjectFolder));
        var tempProjectFolder = settings.Linux.TempProjectFolder;
        var linuxScriptShell = new LinuxScriptShell(settings.TempPublishProjectFolder, settings.Linux.BitviseTlpFile);
        // check web folder
        if (!linuxScriptShell.DirectoryExist(publishFolder))
        {
            //
            if (!linuxScriptShell.Mkdir(publishFolder, true))
                throw new Exception($"Error create folder {publishFolder}");
        }
        else
        {
            // clean webfolder
            if (!linuxScriptShell.RemoveFolder(publishFolder, true))
                Console.WriteLine($"Error clean {publishFolder}");
        }

        // move content
        if (!linuxScriptShell.Move($"{tempProjectFolder}/wwwroot/*", publishFolder))
            throw new Exception($"Error move content !");
        // set credential
        if (!linuxScriptShell.Chown("www-data", publishFolder))
        {
            throw new Exception($"Error set www-data:www-data !");
        }

    }

    static async Task StopService()
    {
        Console.WriteLine($"Stop linux service {settings.Linux.ServiceName}");
        if (string.IsNullOrWhiteSpace(settings.Linux.ServiceName))
            throw new NullReferenceException(nameof(settings.Linux.ServiceName));
        if (await linuxScriptShell.FileExistAsync($"/etc/systemd/system/{settings.Linux.ServiceName}.service"))
        {
            if (!(await linuxScriptShell.StopServiceAsync(settings.Linux.ServiceName)))
                throw new Exception($"Do not stop service {settings.Linux.ServiceName}");
        }
    }


    static async Task StartServiceAsync()
    {
        if (string.IsNullOrWhiteSpace(settings.Linux.ServiceName))
            throw new NullReferenceException(nameof(settings.Linux.ServiceName));


        var tempServiceFileName = $"{settings.TempFolder}/{settings.Linux.ServiceName}.service";

        if (!linuxScriptShell.FileExist($"/etc/systemd/system/{settings.Linux.ServiceName}.service"))
        {

            var text = settings.GetServiceConfig();
            await File.WriteAllLinesAsync(tempServiceFileName, text);
            linuxScriptShell.PutFile(settings.TempFolder, settings.Linux.TempProjectFolder, $"{settings.Linux.ServiceName}.service");
            File.Delete(tempServiceFileName);

            if (!linuxScriptShell.Move($"{settings.Linux.TempProjectFolder}/{settings.Linux.ServiceName}.service", $"/etc/systemd/system/{settings.Linux.ServiceName}.service", true))
                throw new Exception($"Do not create service {settings.Linux.ServiceName}");
            if (!linuxScriptShell.EnableService(settings.Linux.ServiceName))
                throw new Exception($"Do not enable service {settings.Linux.ServiceName}");
        }
        else
        {
            if (settings.UpdateService)
            {
                var text = settings.GetServiceConfig();
                await File.WriteAllLinesAsync(tempServiceFileName, text);
                linuxScriptShell.PutFile(settings.TempFolder, settings.Linux.TempProjectFolder, $"{settings.Linux.ServiceName}.service");
                File.Delete(tempServiceFileName);
                if (!linuxScriptShell.Move($"{settings.Linux.TempProjectFolder}/{settings.Linux.ServiceName}.service", $"/etc/systemd/system/{settings.Linux.ServiceName}.service", true))
                    throw new Exception($"Do not move service {settings.Linux.ServiceName}");

                linuxScriptShell.DaemonReload();
            }
        }

        if (!linuxScriptShell.StartService(settings.Linux.ServiceName))
            throw new Exception($"Do not start service {settings.Linux.ServiceName}");


    }

    static async Task<bool> MigrateAsync()
    {
        if (!linuxScriptShell.DirectoryExist(settings.Linux.PublishFolder))
        {
            throw new Exception($"Do not exist directory {settings.Linux.PublishFolder}");
        }

        var configFileName = $"/etc/sabatex/{settings.Linux.ServiceName}";
        var tempConfigFileName = $"{settings.TempFolder}/{settings.Linux.ServiceName}";

        if (!linuxScriptShell.FileExist(configFileName))
        {
            var text = settings.GetConfig();
            await File.WriteAllTextAsync(tempConfigFileName, text);
            linuxScriptShell.PutFile(settings.TempFolder, settings.Linux.TempProjectFolder, settings.Linux.ServiceName);
            File.Delete(tempConfigFileName);
            if (!linuxScriptShell.DirectoryExist("/etc/sabatex"))
            {
                if (!linuxScriptShell.Mkdir("/etc/sabatex", true))
                    throw new Exception("Error create folder /etc/sabatex");
            }

            linuxScriptShell.Move($"{settings.Linux.TempProjectFolder}/{settings.Linux.ServiceName}", $"/etc/sabatex/{settings.Linux.ServiceName}", true);
        }



        if (!linuxScriptShell.DotnetRun(settings.Linux.PublishFolder, $"{settings.ProjectName}.dll", "--migrate", true))
        {
            throw new Exception($"Error run project {settings.Linux.PublishFolder}");
        }
        return true;
    }

    static async Task<bool> UpdateNginx()
    {
        if (!linuxScriptShell.FileExist("/etc/nginx/sites-available/default"))
        {
            if (!linuxScriptShell.sexec("sudo apt update"))
                return await Error("Do not execute sudo apt update");
            if (!linuxScriptShell.sexec("sudo apt install -y nginx"))
                return await Error("Do not install nginx");
        }

        var configFileName = $"/etc/nginx/sites-available/{settings.Linux.ServiceName}";
        var tempConfigFileName = $"{settings.TempFolder}/{settings.Linux.ServiceName}";
        bool change = false;
        string backup = string.Empty;
        if (!linuxScriptShell.FileExist(configFileName) || !linuxScriptShell.FileExist($"/etc/nginx/sites-enabled/{settings.Linux.ServiceName}"))
        {
            await PutStringAsFileAsync(settings.GetNginxConfig(), "/etc/nginx/sites-available", settings.Linux.ServiceName);
            if (!linuxScriptShell.CreateSymlink(configFileName, $"/etc/nginx/sites-enabled/{settings.Linux.ServiceName}", true))
                return await Error($"Error create symlink /etc/nginx/sites-enabled/{settings.Linux.ServiceName}");
            if ( !linuxScriptShell.CreateSSLCertificate(settings.ProjectName,settings.Linux.NGINX.HostNames,"192.168.1.1"))
                return await Error($"Error create SSL certificate for {settings.Linux.ServiceName}");

            change = true;
            // create config file for new site
        }
        if (settings.UpdateNginx && !change)
        {
            backup = $"{configFileName}-{DateTime.Now.ToString().Replace(':', '-').Replace('.', '-').Replace(' ', '-')}";
            if (!linuxScriptShell.Copy(configFileName, backup, true))
                return await Error($"Error copy file {configFileName}");
            await PutStringAsFileAsync(settings.GetNginxConfig(), "/etc/nginx/sites-available", settings.Linux.ServiceName);
            change = true;
        }

        if (change)
        {
            if (!linuxScriptShell.NginxTestConfig())
            {
                // restore backup
                if (!string.IsNullOrWhiteSpace(backup))
                {
                    if (!linuxScriptShell.Move(backup, configFileName, true))
                        return await Error($"Error restore backup {backup}");
                }
                return await Error("Error test nginx config");

            }
            if (!linuxScriptShell.NginxReload())
                return await Error("Error restart nginx");
        }


        return true;
    }

    static async Task<bool> UpdateBackendAsync()
    {
        var serviceName = settings.Linux.ServiceName;
        var publishFolder = settings.Linux.PublishFolder;
        if (string.IsNullOrWhiteSpace(settings.Linux.TempProjectFolder))
            throw new NullReferenceException(nameof(settings.Linux.TempProjectFolder));
        var tempProjectFolder = settings.Linux.TempProjectFolder;
        if (settings.Linux.FrontEnd)
            throw new Exception("Try update backend for frontend project");

        // stop service and create service file if not exist
        StopService();

        // update service file

        // move project
        // check web folder
        if (!linuxScriptShell.DirectoryExist(publishFolder))
        {
            //
            if (!linuxScriptShell.Mkdir(publishFolder, true))
                throw new Exception($"Error create folder {publishFolder}");
        }
        else
        {
            // backup old version
            if (!linuxScriptShell.Tar(settings.Linux.TarFileName, publishFolder, true))
                Console.WriteLine($"Error create archive {publishFolder}");
            if (!linuxScriptShell.Move(settings.Linux.TarFileName, $"{settings.Linux.TarFileName}{DateTime.Now.ToString("yyyy-MM-dd")}.tar.gz", true))
                Console.WriteLine($"Error move {publishFolder}");
            // clean webfolder
            if (!linuxScriptShell.RemoveFolder($"{publishFolder}/*", true))
                Console.WriteLine($"Error clean {publishFolder}");
        }

        // move content
        if (!linuxScriptShell.Move($"{tempProjectFolder}/*", publishFolder, true))
            throw new Exception($"Error move content !");
        // set credential
        if (!linuxScriptShell.Chown("www-data", publishFolder, true))
        {
            throw new Exception($"Error set www-data:www-data !");
        }

        if (settings.Migrate)
        {
            await MigrateAsync();
        }

        if (!await UpdateNginx())
            return await Error("Error update nginx");


        Console.WriteLine($"Start linux service {serviceName}");
        await StartServiceAsync();
        return true;
    }


    static IConfiguration BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            // 1️⃣ Локальний у папці проекту
            .AddJsonFile("sabatex-publish.json", optional: true, reloadOnChange: true)
            // 2️⃣ Локальний поруч із утилітою
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "sabatex-publish.json"),
                         optional: true, reloadOnChange: true);

        // 3️⃣ Глобальний у OneDrive\.sabatex
        var oneDrive = Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                    .Replace("Documents", "OneDrive");
        var globalPath = Path.Combine(oneDrive, ".sabatex", "sabatex-publish.json");
        builder.AddJsonFile(globalPath, optional: true, reloadOnChange: true);

        return builder.Build();
    }
    static async Task<int> Main(string[] args)
    {
        settings = new SabatexSettings();
        var result = await CommandProcessor.Process(args, settings);
        if (result.shouldExit)
        {
            return result.exitCode;
        }

 

        if (!File.Exists(settings.ProjFile))
        {
            Console.WriteLine("The file not exist: " + settings.ProjFile);
            Environment.Exit(1);
        }

        try
        {
            var errorCode = settings.ResolveConfig();
            if (errorCode != 0)
            {
                return errorCode;
            }
            if (settings.IsLibrary)
            {
                string nugetAuthToken = settings.NUGET.GetToken();
                if (Directory.Exists(settings.OutputPath))
                {
                    string packagesPath = $"{settings.OutputPath}\\*.nupkg";
                    localScriptShell.Delete(packagesPath);
                }
                await PackNugetAsync();

                string symbols = settings.IsPreRelease ? ".symbols" : string.Empty;

                if (settings.IsPreRelease)
                {
                    if (!await localScriptShell.RunAsync($"nuget add \"{settings.OutputPath}\\{settings.ProjectName}.{settings.Version}.symbols.nupkg\" -source {settings.NUGET.GetLocalStorage()}"))
                    {
                        throw new Exception("Error publish to nuget!");
                    }

                }
                else
                {
                    if (!await localScriptShell.RunAsync($"dotnet nuget push \"{settings.OutputPath}\\*{symbols}.nupkg\" -k {nugetAuthToken} -s https://api.nuget.org/v3/index.json --skip-duplicate"))
                    {
                        throw new Exception("Error publish to nuget!");
                    }
                }
            }
            else
            {
                Build();
                PutTolinux();
                if (!settings.Linux.FrontEnd)
                    await UpdateBackendAsync();
                else
                    UpdateBlazorwasm();

            }
            Console.WriteLine("Done!");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            Environment.Exit(1);
        }

        return 0;
    }
}