using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Mooseware.TimeToAir
{
    public abstract class SettingsManager<T> where T : SettingsManager<T>, new()
    {
        private static readonly string filePath =  GetLocalFilePath($"{typeof(T).Name}.json");

        public static T Settings { get; private set; }

        private static string GetLocalFilePath(string fileName)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var orgName = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyCompanyAttribute>().FirstOrDefault();
            var prodName = Assembly.GetEntryAssembly().GetCustomAttributes<AssemblyProductAttribute>().FirstOrDefault();
            return Path.Combine(appData, 
                orgName?.Company ?? MethodInfo.GetCurrentMethod().ReflectedType.Namespace, 
                prodName?.Product ?? Assembly.GetEntryAssembly().GetName().Name,
                fileName);
        }

        public static void Load()
        {
            if (File.Exists(filePath))
            {
                Settings = System.Text.Json.JsonSerializer.Deserialize<T>(File.ReadAllText(filePath));
            }
            else
            {
                Settings = new T();
            }
        }

        public static void Save()
        {
            string json = System.Text.Json.JsonSerializer.Serialize(Settings);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllText(filePath, json);
        }
    }
}
