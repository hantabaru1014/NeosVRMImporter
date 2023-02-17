﻿using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NeosVRMImporter
{
    public class NeosVRMImporter : NeosMod
    {
        public override string Name => "NeosVRMImporter";
        public override string Author => "hantabaru1014";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/hantabaru1014/NeosVRMImporter";

        private static readonly string CACHE_PATH = Path.Combine(Engine.Current.CachePath, "Cache");

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony("dev.baru.neos.NeosVRMImporter");
            harmony.PatchAll();
            Engine.Current.RunPostInit(PatchAssetHelper);
        }

        private static void PatchAssetHelper()
        {
            Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions").Value[AssetClass.Model].Add("vrm");
        }

        private static bool IsVRMFile(string path)
        {
            return Path.GetExtension(path).ToLower() == ".vrm";
        }

        private static bool TryVRMToGLB(string vrmPath, out string glbPath)
        {
            var hash = Utils.GenerateMD5(vrmPath);
            var newPath = Path.Combine(CACHE_PATH, $"{hash}.glb");
            glbPath = newPath;
            try
            {
                File.Copy(vrmPath, newPath, true);
            }
            catch (Exception ex)
            {
                Error($"Failed to convert vrm to glb: {ex.Message}");
                return false;
            }
            Msg($"Converted: {newPath}");
            return true;
        }

        [HarmonyPatch(typeof(ModelImporter))]
        class ModelImporter_Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(ModelImporter.ImportModelAsync))]
            static bool ImportModelAsync_Prefix(string file, Slot targetSlot, ModelImportSettings settings, Slot assetsSlot, IProgressIndicator progressIndicator, ref Task __result)
            {
                if (!IsVRMFile(file))
                {
                    return true;
                }
                Msg($"Importing VRM: {file}");
                var tcs = new TaskCompletionSource<bool>();
                progressIndicator.UpdateProgress(0, "Converting vrm to glb", "");
                Task.Run(() =>
                {
                    if (TryVRMToGLB(file, out var glbPath))
                    {
                        var wrapperMethod = AccessTools.Method(typeof(ModelImporter), "ImportModelWrapper");
                        targetSlot.RunSynchronously(() =>
                        {
                            targetSlot.StartCoroutine((IEnumerator<Context>)wrapperMethod.Invoke(null, new object[] { glbPath, targetSlot, settings, assetsSlot, progressIndicator, tcs }));
                        }, true);
                    }
                    else
                    {
                        progressIndicator.ProgressFail("Failed to convert vrm to glb");
                        tcs.SetResult(true);
                    }
                });
                __result = tcs.Task;
                return false;
            }
        }
    }
}