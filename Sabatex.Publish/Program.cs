using Microsoft.Extensions.Configuration;
using Sabatex.Publish;
using sabatex_publish;
using System.CommandLine;
using System.IO;
using System.Reflection.Metadata;
using System.Text.Json;

namespace sabatex_publish;

public class Program
{
    private const string BatchConfigFileName = "sabatex-publish-solution.json";
    static async Task PackNugetAsync(SabatexSettings settings)
    {
        var localScriptShell = new LocalScriptShell(settings.TempPublishProjectFolder);
        string includeSource = settings.IsPreRelease ? "--include-source" : string.Empty;
        string script = $"dotnet pack --configuration {settings.BuildConfiguration} {includeSource} --output \"{settings.OutputPath}\" \"{settings.ProjFile}\" ";
        if (!await localScriptShell.RunAsync(script))
            throw new Exception("Error build project!");
    }

    static async Task<bool> Error(string message)
    {
        Logger.Error(message);
        await Task.Yield();
        return false;
    }

    static void Build(SabatexSettings settings)
    {
        if (Directory.Exists(settings.TempPublishProjectFolder))
        {
            Directory.Delete(settings.TempPublishProjectFolder, true);
        }
        Directory.CreateDirectory(settings.TempPublishProjectFolder);
        
        var localScriptShell = new LocalScriptShell(settings.TempPublishProjectFolder);
        string script = $"dotnet publish {settings.ProjectFolder}/{settings.ProjectName}.csproj --configuration Release  -o {settings.TempPublishProjectFolder}";
        if (!localScriptShell.Run(script, settings.ProjectFolder))
            throw new Exception("Error build project!");
    }

    static async Task PutStringAsFileAsync(SabatexSettings settings, IEnumerable<string> text, string linuxDestinationPath, string fileName)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var tempProjectFolder = linux.TempProjectFolder ?? throw new InvalidOperationException("TempProjectFolder not configured");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));

        await System.IO.File.WriteAllLinesAsync($"{settings.TempFolder}/{fileName}", text);
        linuxScriptShell.PutFile(settings.TempFolder, tempProjectFolder, fileName);
        System.IO.File.Delete($"{settings.TempFolder}/{fileName}");
        if (!linuxScriptShell.Move($"{tempProjectFolder}/{fileName}", $"{linuxDestinationPath}/{fileName}", true))
            throw new Exception($"Error move file {tempProjectFolder}/{fileName} to {linuxDestinationPath}/{fileName}");
    }

    static void PutTolinux(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var localScriptShell = new LocalScriptShell(settings.TempPublishProjectFolder);
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
        
        var tarFileName = linux.TarFileName;
        var tarFilePath = $"{settings.TempFolder}/{tarFileName}";
        var projectName = settings.ProjectName;
        var tempFolder = settings.TempFolder;
        var linuxTempFolder = linux.TempFolder ?? throw new InvalidOperationException("Linux.TempFolder not configured");

        if (File.Exists(tarFilePath))
            File.Delete(tarFilePath);

        if (!localScriptShell.Run($"tar -czvf {tarFileName} {projectName}", tempFolder))
            throw new Exception("Error pack !");
        if (!linuxScriptShell.DirectoryExist(linuxTempFolder))
        {
            if (!linuxScriptShell.Mkdir(linuxTempFolder))
                throw new Exception($"Error create folder {linuxTempFolder} !");
        }
        if (linuxScriptShell.DirectoryExist(linux.TempProjectFolder))
        {
            if (!linuxScriptShell.RemoveFolder(linux.TempProjectFolder))
                throw new Exception($"Error delete {linux.TempProjectFolder}");
        }

        linuxScriptShell.PutFile(tempFolder, linuxTempFolder, tarFileName);
        linuxScriptShell.UnPack(linuxTempFolder, tarFileName);
        Directory.Delete($"{tempFolder}/{projectName}", true);
    }

    static void UpdateBlazorwasm(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var publishFolder = linux.PublishFolder ?? throw new InvalidOperationException("PublishFolder not configured");
        var tempProjectFolder = linux.TempProjectFolder ?? throw new InvalidOperationException("TempProjectFolder not configured");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempPublishProjectFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
        
        if (!linuxScriptShell.DirectoryExist(publishFolder))
        {
            if (!linuxScriptShell.Mkdir(publishFolder, true))
                throw new Exception($"Error create folder {publishFolder}");
        }
        else
        {
            if (!linuxScriptShell.RemoveFolder(publishFolder, true))
                Logger.Warn($"Error clean {publishFolder}");
        }

        if (!linuxScriptShell.Move($"{tempProjectFolder}/wwwroot/*", publishFolder))
            throw new Exception($"Error move content !");
        if (!linuxScriptShell.Chown("www-data", publishFolder))
        {
            throw new Exception($"Error set www-data:www-data !");
        }
    }

    static async Task StopService(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
    
        Logger.Info($"Stop linux service {linux.ServiceName}");
        if (string.IsNullOrWhiteSpace(linux.ServiceName))
            throw new NullReferenceException(nameof(linux.ServiceName));
        if (await linuxScriptShell.FileExistAsync($"/etc/systemd/system/{linux.ServiceName}.service"))
        {
            if (!(await linuxScriptShell.StopServiceAsync(linux.ServiceName)))
                throw new Exception($"Do not stop service {linux.ServiceName}");
        }
    }

    static async Task StartServiceAsync(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var tempProjectFolder = linux.TempProjectFolder ?? throw new InvalidOperationException("TempProjectFolder not configured");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
    
        if (string.IsNullOrWhiteSpace(linux.ServiceName))
            throw new NullReferenceException(nameof(linux.ServiceName));

        var tempServiceFileName = $"{settings.TempFolder}/{linux.ServiceName}.service";

        if (!linuxScriptShell.FileExist($"/etc/systemd/system/{linux.ServiceName}.service"))
        {
            var text = settings.GetServiceConfig();
            await File.WriteAllLinesAsync(tempServiceFileName, text);
            linuxScriptShell.PutFile(settings.TempFolder, tempProjectFolder, $"{linux.ServiceName}.service");
            File.Delete(tempServiceFileName);

            if (!linuxScriptShell.Move($"{tempProjectFolder}/{linux.ServiceName}.service", $"/etc/systemd/system/{linux.ServiceName}.service", true))
                throw new Exception($"Do not create service {linux.ServiceName}");
            if (!linuxScriptShell.EnableService(linux.ServiceName))
                throw new Exception($"Do not enable service {linux.ServiceName}");
        }
        else
        {
            if (settings.UpdateService)
            {
                var text = settings.GetServiceConfig();
                await File.WriteAllLinesAsync(tempServiceFileName, text);
                linuxScriptShell.PutFile(settings.TempFolder, tempProjectFolder, $"{linux.ServiceName}.service");
                File.Delete(tempServiceFileName);
                if (!linuxScriptShell.Move($"{tempProjectFolder}/{linux.ServiceName}.service", $"/etc/systemd/system/{linux.ServiceName}.service", true))
                    throw new Exception($"Do not move service {linux.ServiceName}");

                linuxScriptShell.DaemonReload();
            }
        }

        if (!linuxScriptShell.StartService(linux.ServiceName))
            throw new Exception($"Do not start service {linux.ServiceName}");
    }

    static async Task<bool> MigrateAsync(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var tempProjectFolder = linux.TempProjectFolder ?? throw new InvalidOperationException("TempProjectFolder not configured");
        var publishFolder = linux.PublishFolder ?? throw new InvalidOperationException("PublishFolder not configured");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
    
        if (!linuxScriptShell.DirectoryExist(linux.PublishFolder))
        {
            throw new Exception($"Do not exist directory {linux.PublishFolder}");
        }

        var configFileName = $"/etc/sabatex/{linux.ServiceName}";
        var tempConfigFileName = $"{settings.TempFolder}/{linux.ServiceName}";

        if (!linuxScriptShell.FileExist(configFileName))
        {
            var text = settings.GetConfig();
            await File.WriteAllTextAsync(tempConfigFileName, text);
            linuxScriptShell.PutFile(settings.TempFolder, tempProjectFolder, linux.ServiceName);
            File.Delete(tempConfigFileName);
            if (!linuxScriptShell.DirectoryExist("/etc/sabatex"))
            {
                if (!linuxScriptShell.Mkdir("/etc/sabatex", true))
                    throw new Exception("Error create folder /etc/sabatex");
            }

            linuxScriptShell.Move($"{linux.TempProjectFolder}/{linux.ServiceName}", $"/etc/sabatex/{linux.ServiceName}", true);
        }

        if (!linuxScriptShell.DotnetRun(publishFolder, $"{settings.ProjectName}.dll", "--migrate", true))
        {
            throw new Exception($"Error run project {publishFolder}");
        }
        return true;
    }

    static async Task<bool> UpdateNginx(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
        
        if (!linuxScriptShell.FileExist("/etc/nginx/sites-available/default"))
        {
            if (!linuxScriptShell.sexec("sudo apt update"))
                return await Error("Do not execute sudo apt update");
            if (!linuxScriptShell.sexec("sudo apt install -y nginx"))
                return await Error("Do not install nginx");
        }

        var configFileName = $"/etc/nginx/sites-available/{linux.ServiceName}";
        var tempConfigFileName = $"{settings.TempFolder}/{linux.ServiceName}";
        bool change = false;
        string backup = string.Empty;
        
        if (!linuxScriptShell.FileExist(configFileName) || !linuxScriptShell.FileExist($"/etc/nginx/sites-enabled/{linux.ServiceName}"))
        {
            await PutStringAsFileAsync(settings, settings.GetNginxConfig(), "/etc/nginx/sites-available", linux.ServiceName);
            if (!linuxScriptShell.CreateSymlink(configFileName, $"/etc/nginx/sites-enabled/{linux.ServiceName}", true))
                return await Error($"Error create symlink /etc/nginx/sites-enabled/{linux.ServiceName}");
            var hostNames = linux.NGINX.HostNames ?? throw new InvalidOperationException("HostNames not configured");
            if (!linuxScriptShell.CreateSSLCertificate(settings.ProjectName, hostNames, "192.168.1.1"))
                return await Error($"Error create SSL certificate for {linux.ServiceName}");

            change = true;
        }
        if (settings.UpdateNginx && !change)
        {
            backup = $"{configFileName}-{DateTime.Now.ToString().Replace(':', '-').Replace('.', '-').Replace(' ', '-')}$";
            if (!linuxScriptShell.Copy(configFileName, backup, true))
                return await Error($"Error copy file {configFileName}");
            await PutStringAsFileAsync(settings, settings.GetNginxConfig(), "/etc/nginx/sites-available", settings.Linux.ServiceName);
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

    static async Task<bool> UpdateBackendAsync(SabatexSettings settings)
    {
        var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
        var linuxScriptShell = new LinuxScriptShell(
            settings.TempFolder, 
            linux.BitviseTlpFile ?? throw new InvalidOperationException("BitviseTlpFile not configured"));
    
        var serviceName = linux.ServiceName;
        var publishFolder = linux.PublishFolder ?? throw new InvalidOperationException("PublishFolder not configured");
        var tempProjectFolder = linux.TempProjectFolder ?? throw new InvalidOperationException("TempProjectFolder not configured");
        
        if (linux.FrontEnd)
            throw new Exception("Try update backend for frontend project");

        await StopService(settings);

        if (!linuxScriptShell.DirectoryExist(publishFolder))
        {
            if (!linuxScriptShell.Mkdir(publishFolder, true))
                throw new Exception($"Error create folder {publishFolder}");
        }
        else
        {
            if (!linuxScriptShell.Tar(linux.TarFileName, publishFolder, true))
                Logger.Warn($"Error create archive {publishFolder}");
            if (!linuxScriptShell.Move(linux.TarFileName, $"{linux.TarFileName}{DateTime.Now.ToString("yyyy-MM-dd")}.tar.gz", true))
                Logger.Warn($"Error move {publishFolder}");
            if (!linuxScriptShell.RemoveFolder($"{publishFolder}/*", true))
                Logger.Warn($"Error clean {publishFolder}");
        }

        if (!linuxScriptShell.Move($"{tempProjectFolder}/*", publishFolder, true))
            throw new Exception($"Error move content !");
        if (!linuxScriptShell.Chown("www-data", publishFolder, true))
        {
            throw new Exception($"Error set www-data:www-data !");
        }

        if (settings.Migrate)
        {
            await MigrateAsync(settings);
        }

        if (!await UpdateNginx(settings))
            return await Error("Error update nginx");

        Logger.Info($"Start linux service {serviceName}");
        await StartServiceAsync(settings);
        return true;
    }

    /// <summary>
    /// Publish single project (extracted from Main for reusability)
    /// </summary>
    /// <param name="projectSettings">Project settings</param>
    /// <returns>Exit code (0 = success)</returns>
    public static async Task<int> PublishProjectAsync(SabatexSettings settings)
    {
        try
        {
            if (settings.IsLibrary)
            {
                var localScriptShell = new LocalScriptShell(settings.TempPublishProjectFolder);
                
                string nugetAuthToken = settings.NUGET?.GetToken() ?? throw new Exception("The NUGET section not initialized!");
                if (Directory.Exists(settings.OutputPath))
                {
                    string packagesPath = $"{settings.OutputPath}\\*.nupkg";
                    localScriptShell.Delete(packagesPath);
                }
                await PackNugetAsync(settings);

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
                //# Ensure Linux is initialized for non-library projects
                var linux = settings.Linux ?? throw new InvalidOperationException("Linux configuration not initialized");
                
                Build(settings);
                PutTolinux(settings);
                if (!linux.FrontEnd)
                    await UpdateBackendAsync(settings);
                else
                    UpdateBlazorwasm(settings);
            }

            Logger.Info("Done!");
            return 0;
        }
        catch (Exception ex)
        {
            Logger.Error(ex.Message);
            return 1;
        }
    }

    static async Task<int> Main(string[] args)
    {
        var (exitCode, shouldExit, folderPath, projFile,  migrate, updateService, updateNginx) 
            = await CommandProcessor.Process(args);
        
        if (shouldExit)
        {
            return exitCode;
        }


        if (folderPath != null && projFile != null) 
        {
            Logger.Error("Cannot use both --folder and --csproj options together.");
            return 1;
        }

        var projects = new List<string>();

        if (projFile != null)
        {
            projects.Add(projFile);
        }
        else
        {
            var directory = folderPath ?? Directory.GetCurrentDirectory();

            var configPath = Path.Combine(directory, "sabatex-publish-solution.json");

            if (File.Exists(configPath))
            {
                Logger.Info("Batch publishing configuration found in the folder.");
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<SolutionPublishConfig>(json)
                    ?? throw new Exception("Failed to parse solution config");
                foreach (var item in config.Projects)
                {
                    if (item.Enabled)
                    {
                        var projectPath = Path.Combine(directory, item.Path);
                        projects.Add(projectPath);
  
                    }
                }

            }
            else
            {
                var csprojFiles = Directory.GetFiles(directory, "*.csproj");
                if (csprojFiles.Length == 0)
                {
                    Logger.Error("No .csproj files found in the specified folder.");
                    return 2;
                }
                else if (csprojFiles.Length > 1)
                {
                    Logger.Error("Multiple .csproj files found. Please specify one using --csproj option.");
                    return 2;
                }
                else
                {
                    projects.Add(csprojFiles[0]);
                }
            }

        }

   
        foreach (var project in projects)
        {
            Logger.Info($"Publishing project: {project}");
            var settings = new SabatexSettings() { ProjFile = project ,Migrate = migrate, UpdateService = updateService, UpdateNginx = updateNginx};

            var errorCode = settings.ResolveConfig();
            if (errorCode != 0)
            {
                return errorCode;
            }
            var result = await PublishProjectAsync(settings);
            if (result != 0)
            {
                return result;
            }
        }
        return 0;
    }
}