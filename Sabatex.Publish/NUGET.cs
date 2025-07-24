using sabatex_publish;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sabatex.Publish;

public class NUGET
{
    public string? NugetAuthTokenPath { get; set; }
    public string? LocalDebugStorage { get; set; }

    public string GetToken()
    {
        if (NugetAuthTokenPath == null)
        {
            NugetAuthTokenPath = GlobalConfigManager.Get<string>("NugetAuthTokenPath") ?? throw new Exception("The parameter 'NugetAuthTokenPath'  must be defined in file SabatexSettings.json");
        }
            

        string[] token = File.ReadAllLines(NugetAuthTokenPath);
        if (token.Length == 0 || token.Length > 1)
            throw new Exception("The NUGET TOKEN is wrong!");
        return token[0];

    }

    public string GetLocalStorage()
    {
        if (LocalDebugStorage == null)
        {
            LocalDebugStorage = GlobalConfigManager.Get<string>("LocalDebugStorage") ?? throw new Exception("The parameter 'LocalDebugStorage'  must be defined in file SabatexSettings.json");
        }
        return LocalDebugStorage;
    }
}