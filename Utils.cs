using FrooxEngine;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace NeosVRMImporter
{
    public static class Utils
    {
        public static readonly string CACHE_PATH = Path.Combine(Engine.Current.CachePath, "Cache");
        public static readonly string MOD_WORKING_DIRECTORY = Path.Combine(CACHE_PATH, "NeosVRMImporter");

        public class UpdateCheckResult
        {
            public bool IsAvailableUpdate { get; set; }
            public string CurrentVersion { get; set; }
            public string LatestVersion { get; set; }

            public UpdateCheckResult(bool isAvailableUpdate, string currentVersion, string latestVersion)
            {
                IsAvailableUpdate = isAvailableUpdate;
                CurrentVersion = currentVersion;
                LatestVersion = latestVersion;
            }
        }

        public static async Task<UpdateCheckResult> CheckUpdate(string repoOwner, string repoName)
        {
            string currentVersion = string.Empty;
            try
            {
                using var sr = new StreamReader(Path.Combine(MOD_WORKING_DIRECTORY, $".{repoOwner}.{repoName}.version"));
                currentVersion = sr.ReadToEnd();
            }
            catch { }
            using var webClient = new WebClient();
            webClient.Headers.Add("User-Agent", "hantabaru1014/NeosVRMImporter");
            UpdateCheckResult result = new UpdateCheckResult(false, currentVersion, "");
            try
            {
                var jsonStr = await webClient.DownloadStringTaskAsync($"https://api.github.com/repos/{repoOwner}/{repoName}/releases");
                var latestEntry = JArray.Parse(jsonStr)[0];
                var latestVersion = latestEntry.Value<string>("tag_name");
                result.LatestVersion = latestVersion;
                result.IsAvailableUpdate = latestVersion != currentVersion;
            }
            catch { }
            return result;
        }

        public static void WriteDownloadedVersion(string repoOwner, string repoName, string version)
        {
            using var sw = new StreamWriter(Path.Combine(MOD_WORKING_DIRECTORY, $".{repoOwner}.{repoName}.version"));
            sw.WriteLine(version);
        }

        public static string? GetInstalledPath(string productName)
        {
            RegistryKey key64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            RegistryKey key = key64.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");

            if (key != null)
            {
                foreach (RegistryKey subkey in key.GetSubKeyNames().Select(keyName => key.OpenSubKey(keyName)))
                {
                    var displayName = subkey.GetValue("DisplayName") as string;
                    if (displayName != null && displayName.Contains(productName))
                    {
                        return subkey.GetValue("InstallLocation").ToString();
                    }
                }
            }

            return null;
        }

        public static string GetBlenderPath()
        {
            var inToolsPath = Path.Combine(Engine.Current.AppPath, "Tools", "Blender", "blender.exe");
            if (File.Exists(inToolsPath))
            {
                return inToolsPath;
            }
            var installedPath = GetInstalledPath("blender");
            if (string.IsNullOrEmpty(installedPath))
            {
                return string.Empty;
            }
            else
            {
                return Path.Combine(installedPath, "blender.exe");
            }
        }
    }
}
