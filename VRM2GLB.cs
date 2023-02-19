using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading.Tasks;

namespace NeosVRMImporter
{
    public static class VRM2GLB
    {
        public static readonly string CONV_TOOL_PATH = Path.Combine(Utils.MOD_WORKING_DIRECTORY, "vrmtoglb_autoconvert");
        public static readonly string BLENDER_ADDON_PATH = Path.Combine(Utils.MOD_WORKING_DIRECTORY, "VRM_Addon_for_Blender.zip");
        public static readonly string BLENDER_EXE_PATH = Utils.GetBlenderPath();

        private const string CONV_TOOL_REPO_OWNER = "kazu0617";
        private const string CONV_TOOL_REPO_NAME = "vrmtoglb_autoconvert";
        private const string CONV_TOOL_DL_URL = "https://github.com/kazu0617/vrmtoglb_autoconvert/archive/refs/heads/master.zip";

        private const string BLENDER_ADDON_REPO_OWNER = "saturday06";
        private const string BLENDER_ADDON_REPO_NAME = "VRM-Addon-for-Blender";
        private const string BLENDER_ADDON_DL_URL = "https://github.com/saturday06/VRM_Addon_for_Blender/raw/release-archive/VRM_Addon_for_Blender-release.zip";

        public static async Task UpdateConvertTool(bool forceDownload = false)
        {
            var repoInfo = await Utils.CheckUpdate(CONV_TOOL_REPO_OWNER, CONV_TOOL_REPO_NAME);
            if (!forceDownload && !repoInfo.IsAvailableUpdate) return;

            using var webClient = new WebClient();
            var dlPath = Path.Combine(Utils.CACHE_PATH, "vrmtoglb_autoconvert.zip");
            await webClient.DownloadFileTaskAsync(CONV_TOOL_DL_URL, dlPath);
            if (Directory.Exists(CONV_TOOL_PATH))
            {
                Directory.Delete(CONV_TOOL_PATH, true);
            }
            ZipFile.ExtractToDirectory(dlPath, CONV_TOOL_PATH);
            Utils.WriteDownloadedVersion(CONV_TOOL_REPO_OWNER, CONV_TOOL_REPO_NAME, repoInfo.LatestVersion);
            if (!File.Exists(Path.Combine(CONV_TOOL_PATH, "vrmconv.py")) && Directory.GetDirectories(CONV_TOOL_PATH).Length == 1)
            {
                Directory.Move(CONV_TOOL_PATH, CONV_TOOL_PATH + "2");
                Directory.Move(Directory.GetDirectories(CONV_TOOL_PATH + "2")[0], CONV_TOOL_PATH);
                Directory.Delete(CONV_TOOL_PATH + "2");
            }
        }

        public static async Task UpdateBlenderAddon(bool forceDownload = false)
        {
            var repoInfo = await Utils.CheckUpdate(BLENDER_ADDON_REPO_OWNER, BLENDER_ADDON_REPO_NAME);
            if (!forceDownload && !repoInfo.IsAvailableUpdate) return;

            using var webClient = new WebClient();
            await webClient.DownloadFileTaskAsync(BLENDER_ADDON_DL_URL, BLENDER_ADDON_PATH);
            Utils.WriteDownloadedVersion(BLENDER_ADDON_REPO_OWNER, BLENDER_ADDON_REPO_NAME, repoInfo.LatestVersion);
        }

        public static async Task UpdateTools(bool forceDownload = false)
        {
            // HACK: 平行実行すると上手く動かない
            await UpdateConvertTool(forceDownload);
            await UpdateBlenderAddon(forceDownload);
        }

        public static bool IsReadyToConvert()
        {
            return File.Exists(BLENDER_EXE_PATH)
                && File.Exists(Path.Combine(CONV_TOOL_PATH, "empty.blend"))
                && File.Exists(Path.Combine(CONV_TOOL_PATH, "vrmconv.py"))
                && File.Exists(BLENDER_ADDON_PATH);
        }

        public static async Task<string?> ConvertVRMtoGLB(string path)
        {
            if (!IsReadyToConvert()) return null;
            var id = Guid.NewGuid().ToString().Replace("-", "");
            var tmpPath = Path.Combine(Utils.MOD_WORKING_DIRECTORY, id + ".vrm");
            try
            {
                File.Copy(path, tmpPath);
            }
            catch
            {
                return null;
            }
            var outPath = Path.Combine(Utils.MOD_WORKING_DIRECTORY, id + ".glb");

            using var proc = new Process();
            proc.StartInfo.FileName = BLENDER_EXE_PATH;
            proc.StartInfo.Arguments = $"\"{Path.Combine(CONV_TOOL_PATH, "empty.blend")}\" --python \"{Path.Combine(CONV_TOOL_PATH, "vrmconv.py")}\" --background -- --input \"{tmpPath}\" --output \"{outPath}\" --addonfile \"{BLENDER_ADDON_PATH}\"";
            proc.StartInfo.WorkingDirectory = Utils.MOD_WORKING_DIRECTORY;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
            await Task.Run(() =>
            {
                proc.WaitForExit();
            });

            return File.Exists(outPath) ? outPath : null;
        }

        public static string? RenameVRMtoGLB(string path)
        {
            var outPath = Path.Combine(Utils.MOD_WORKING_DIRECTORY, Guid.NewGuid().ToString().Replace("-", "") + ".glb");
            try
            {
                File.Copy(path, outPath, true);
            }
            catch
            {
                return null;
            }
            return outPath;
        }
    }
}
