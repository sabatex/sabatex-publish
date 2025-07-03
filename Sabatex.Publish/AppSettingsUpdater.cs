
namespace sabatex_publish;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class AppSettingsUpdater
{
    // Визначаємо валідні ключі для кожного рівня
    private static readonly string[] SabatexKeys = {
        "TempFolder", "NUGET", "Linux"
    };
    private static readonly string[] NugetKeys = {
        "NugetAuthTokenPath", "LocalDebugStorage"
    };
    private static readonly string[] LinuxKeys = {
        "ServiceName", "UserHomeFolder", "Port",
        "FrontEnd", "PublishFolder", "BitviseTlpFile", "NGINX"
    };
    private static readonly string[] NginxKeys = {
        "HostName", "SSLPublic", "SSLPrivate", "AppPort"
    };

    public static void EnsureSabatexSettings(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("appsettings.json not found", filePath);

        // Парсимо JSON у дерево JsonNode
        var root = JsonNode.Parse(File.ReadAllText(filePath))?.AsObject()
                   ?? throw new Exception("Invalid JSON");

        // Отримуємо або створюємо секцію SabatexSettings
        if (!root.ContainsKey("SabatexSettings"))
            root["SabatexSettings"] = new JsonObject();
        var sabatex = root["SabatexSettings"]!.AsObject();
        CleanSection(sabatex, SabatexKeys);

        // Підсекція NUGET
        if (!sabatex.ContainsKey("NUGET"))
            sabatex["NUGET"] = new JsonObject();
        CleanSection(sabatex["NUGET"]!.AsObject(), NugetKeys);

        // Підсекція Linux
        if (!sabatex.ContainsKey("Linux"))
            sabatex["Linux"] = new JsonObject();
        var linux = sabatex["Linux"]!.AsObject();
        CleanSection(linux, LinuxKeys);

        // Підсекція Linux → NGINX
        if (!linux.ContainsKey("NGINX"))
            linux["NGINX"] = new JsonObject();
        CleanSection(linux["NGINX"]!.AsObject(), NginxKeys);

        // Записуємо назад з відступами
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(filePath, root.ToJsonString(options));
    }

    // Перейменовуємо всі незнайомі ключі в UNUSED_<oldName>
    private static void CleanSection(JsonObject section, IEnumerable<string> validKeys)
    {
        var toRename = section
            .Select(p => p.Key)
            .Where(k => !validKeys.Contains(k))
            .ToList();

        foreach (var oldKey in toRename)
        {
            var value = section[oldKey];
            section.Remove(oldKey);
            section[$"UNUSED_{oldKey}"] = value;
        }
    }
}

// Виклик:
//
// AppSettingsUpdater.EnsureSabatexSettings(@"C:\path\to\appsettings.json");