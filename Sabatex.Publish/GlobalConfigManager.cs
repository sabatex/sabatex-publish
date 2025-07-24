using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace sabatex_publish
{
    public static class GlobalConfigManager
    {
        // Shared (OneDrive) and local (tool folder) file paths
        private static readonly string SharedFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                       .Replace("Documents", "OneDrive"),
            ".sabatex",
            "sabatex-publish.json");

        private static readonly string LocalFilePath = Path.Combine(
            AppContext.BaseDirectory,
            "sabatex-publish.json");

        private static IConfigurationRoot LoadConfiguration(bool? shared = null)
        {
            var builder = new ConfigurationBuilder();

            if (shared == null || shared == true)
                builder.AddJsonFile(SharedFilePath, optional: true, reloadOnChange: false);

            if (shared == null || shared == false)
                builder.AddJsonFile(LocalFilePath, optional: true, reloadOnChange: false);

            return builder.Build();
        }



        public static T? Get<T>(string key, bool? shared = null)
        {

            var config = LoadConfiguration(shared);
            return config.GetValue<T>(key);
        }


        // Записує ключ-значення у JSON (створить файл/розділ, якщо потрібно)
        public static void Set(string key, string value,bool shared)
        {
            var path = shared ? SharedFilePath : LocalFilePath;

            // Зчитати існуючий JSON у словник
            var dict = File.Exists(path)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(
                      File.ReadAllText(path))!
                : new Dictionary<string, object>();


            // 2) Оновити/додати ключ
            dict[key] = value;

            // 3) Зберегти назад у файл, з відступами
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path,
                JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true }));
        }
    
    
        public static void SetValue(string NuGetDebugPackagePath,string NuGetKeyPath,bool shared)
        {
            if (string.IsNullOrEmpty(NuGetDebugPackagePath) && string.IsNullOrEmpty(NuGetKeyPath))
            {
                throw new ArgumentException("NuGetDebugPackagePath and NuGetKeyPath cannot be null or empty.");
            }

            if (!string.IsNullOrEmpty(NuGetDebugPackagePath))
            {
                Set("LocalDebugStorage", NuGetDebugPackagePath, shared);
            }
            if (!string.IsNullOrEmpty(NuGetKeyPath))
            {
                Set("NugetAuthTokenPath", NuGetKeyPath, shared);
            }

        }
    
    }
}
