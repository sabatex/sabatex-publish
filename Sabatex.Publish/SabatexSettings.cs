﻿// filename: SabatexSettings.cs
using Microsoft.Extensions.Configuration;
using sabatex_publish;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Sabatex.Publish;

public class SabatexSettings : AppConfig
{
    const string configFileName = "appsettings.json";
    //readonly string _projectFilePath;
    string _version;

    public string? ProjFile { get; set; }
    public bool UpdateNginx { get; set; } = false;
    public bool UpdateService { get; set; } = false;
    public bool Migrate { get; set; } = false;


    #region bindable from appsetings.json
    /// <summary>
    /// May be set from project appsetings.json sabatex:TempFolder,
    /// default {user temp folder}/sabatex
    /// </summary>
    public string TempFolder { get; set; }

    #endregion
    /// <summary>
    /// The project name set as csproj file name
    /// </summary>
    public string ProjectName => Path.GetFileNameWithoutExtension(ProjFile ?? throw new Exception("The ProjFile is null"));
    public string ProjectFolder
    {
        get
        {
            var result = Path.GetDirectoryName(ProjFile ?? throw new Exception("The ProjFile is null")) ?? string.Empty;
            if (result == string.Empty)
            {
                throw new Exception($"Error get folder path from: {ProjFile}");
            }
            return result;
        }
    }
    public string Version => _version;

    public string TempPublishProjectFolder { get; private set; } = default!;

    public bool IsPreRelease { get; private set; }
    public string OutputPath { get; private set; }
    public string BuildConfiguration { get; private set; }


    public Linux? Linux { get; set; }

    public NUGET? NUGET { get; set; }


    public bool IsLibrary { get; private set; }
    
    public SabatexSettings()
    {
    }

    public int ResolveConfig()
    {
        if (ProjFile == null)
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.csproj");
            if (files.Length == 0)
            {
                Logger.Error("The Current directory must contains  *.csproj file");
                return 2;
            }
            if (files.Length > 1)
            {
                Logger.Error("The Current directory must contains only one *.csproj file");
                return 3;
            }
            ProjFile = files[0];
        }


        if (!File.Exists(ProjFile))
        {
            Logger.Error("The file not exist: " + ProjFile);
            return 4;
        }

        if (Path.GetExtension(ProjFile) != ".csproj")
        {
            Logger.Error($"The file:{ProjFile} must be extensions *.csproj!");
            return 5;
        }

        // read csproj data
        var xml = new System.Xml.XmlDocument();
        xml.Load(ProjFile);
        _version = xml.SelectSingleNode("Project/PropertyGroup/Version")?.InnerText;
        if (_version == null)
        {
            Logger.Error($"The project file {ProjFile} do not include section <PropertyGroup/Version>");
            return 6;
        }
        
        var ver = new Version(_version);
        IsPreRelease = ver.IsPreRelease;
        BuildConfiguration = IsPreRelease ? "Debug" : "Release";
        OutputPath = $"{Path.GetTempPath()}sabatex\\{ProjectName}\\bin\\{BuildConfiguration}";
  
        var sdk = xml.SelectSingleNode("Project")?.Attributes?.GetNamedItem("Sdk")?.Value;
        if (sdk == null)
        {
            Logger.Error($"Do not read SDK type from project");
            return 7;
        }
        switch (sdk)
        {
            case "Microsoft.NET.Sdk.Web":
                break; //exe
            case "Microsoft.NET.Sdk":
            case "Microsoft.NET.Sdk.Razor":
                var library = xml.SelectSingleNode("Project/PropertyGroup/OutputType")?.InnerText;
                if (library == null || library.ToLower() != "exe")
                    IsLibrary = true;
                break;
            default:
                Logger.Error($"Uknown SDK type");
                return 8;

        }
        var userSecretId = xml.SelectSingleNode("Project/PropertyGroup/UserSecretsId")?.InnerText;



        Linux = new Linux(ProjectName);
        var builder = new ConfigurationBuilder().SetBasePath(ProjectFolder);
        string appConfig = $"{AppDomain.CurrentDomain.BaseDirectory}/sabatex-publish.json";
        if (File.Exists($"{appConfig}"))
            builder.AddJsonFile(appConfig);

        if (File.Exists($"{ProjectFolder}/{configFileName}"))
            builder.AddJsonFile(configFileName);
        if (!String.IsNullOrWhiteSpace(userSecretId))
            builder.AddUserSecrets(userSecretId);


        var conf = builder.Build();
        var sabatexSection = conf.GetSection("SabatexSettings");
        if (sabatexSection == null)
        {
            Logger.Error("The file appsetting.json d'nt contains section Sabatex!!! ");
            return 9;
        }
        sabatexSection.Bind(this);

        if (NUGET == null)
        {
            NUGET = new NUGET();
        }

        if (TempFolder == null)
        {
            TempFolder = $"{Path.GetTempPath()}Sabatex";
        }

        if (!Directory.Exists(TempFolder))
        {
            Directory.CreateDirectory(TempFolder);
        }


        TempPublishProjectFolder = $"{TempFolder}\\{ProjectName}";
        if (!Directory.Exists(TempPublishProjectFolder))
        {
            Directory.CreateDirectory(TempPublishProjectFolder);
        }

        return 0;
    }

    public IEnumerable<string> GetServiceConfig()
    {
        yield return "[Unit]";
        yield return $"Description = ASP.NET Core {ProjectName}";
        yield return "[Service]";
        yield return $"WorkingDirectory ={Linux.PublishFolder}";
        yield return $"ExecStart =/usr/bin/dotnet {Linux.PublishFolder}/{ProjectName}.dll";
        yield return "Restart=always";
        yield return "RestartSec=10";
        yield return "KillSignal=SIGINT";
        yield return $"SyslogIdentifier={ProjectName}";
        yield return "User=www-data";
        yield return "Environment=ASPNETCORE_ENVIRONMENT=Production";
        yield return "Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false";

        yield return $"Environment=ASPNETCORE_URLS=http://localhost:{Linux.Port}";
        yield return "[Install]";
        yield return "WantedBy=multi-user.target";
    }

    public IEnumerable<string> GetNginxConfig()
    {
        yield return "server {";
        yield return "    listen 80;";
        yield return $"    server_name {Linux.NGINX.HostNames};";
        yield return "    location / {";
        yield return "        add_header Strict-Transport-Security max-age=15768000;";
        yield return "        return 301 https://$host$request_uri;";
        yield return "    }";
        yield return "}";
        yield return "";
        yield return "server {";
        yield return "    listen *:443              ssl;";
        yield return $"    server_name               {Linux.NGINX.HostNames};";
        if (Linux.NGINX.SSLPublic != null)
        {
            yield return $"    ssl_certificate           {Linux.NGINX.SSLPublic};";
        }

        if (Linux.NGINX.SSLPrivate != null)
        {
            yield return $"    ssl_certificate_key       {Linux.NGINX.SSLPrivate};";
        }


        yield return "    ssl_protocols             TLSv1.1 TLSv1.2;";
        yield return "    ssl_prefer_server_ciphers on;";
        yield return "    ssl_ciphers               \"EECDH+AESGCM:EDH+AESGCM:AES256+EECDH:AES256+EDH\";";
        yield return "    ssl_ecdh_curve            secp384r1;";
        yield return "    ssl_session_cache         shared:SSL:10m;";
        yield return "    ssl_session_tickets       off;";
        yield return "    ssl_stapling              on;";
        yield return "    ssl_stapling_verify       on;";
        yield return $"    ssl_certificate           /etc/ssl/certs/{ProjectName}.crt";
        yield return $"    ssl_certificate_key       /etc/ssl/private/{ProjectName}.key";
        yield return "";
        yield return "    add_header Strict-Transport-Security \"max-age=63072000; includeSubdomains; preload\";";
        yield return "    add_header X-Frame-Options           DENY;";
        yield return "    add_header X-Content-Type-Options    nosniff;";
        yield return "    proxy_redirect   off;";
        yield return "    proxy_set_header Host             $host;";
        yield return "    proxy_set_header X-Real-IP        $remote_addr;";
        yield return "    proxy_set_header X-Forwarded-For  $proxy_add_x_forwarded_for;";
        yield return "    proxy_set_header X-Forwarded-Proto $scheme;";
        yield return "    client_max_body_size    10m;";
        yield return "    client_body_buffer_size 128k;";
        yield return "    proxy_connect_timeout   90;";
        yield return "    proxy_send_timeout      90;";
        yield return "    proxy_read_timeout      90;";
        yield return "    proxy_buffers        8 16k;";
        yield return "    proxy_buffer_size    16k;";
        yield return "";
        yield return "    #Redirects all traffic";
        yield return "    location / {";
        yield return $"        proxy_pass       http://localhost:{Linux.Port};";
        yield return "    }";
        yield return "}";
    }


}
