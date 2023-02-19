using BaseX;
using CodeX;
using FrooxEngine;
using HarmonyLib;
using NeosModLoader;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NeosVRMImporter
{
    public class NeosVRMImporter : NeosMod
    {
        public override string Name => "NeosVRMImporter";
        public override string Author => "hantabaru1014";
        public override string Version => "1.0.0";
        public override string Link => "https://github.com/hantabaru1014/NeosVRMImporter";

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> RenameOnlyConvertKey =
            new ModConfigurationKey<bool>("RenameOnlyConvert", "Convert only by changing the extension from vrm to glb (for VRM1.0)", () => false);
        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> ImportTexturesKey =
            new ModConfigurationKey<bool>("ImportTextures", "Import all textures included in VRM separately from the model", () => false);

        private static ModConfiguration? _config;

        public override void OnEngineInit()
        {
            _config = GetConfiguration();
            Harmony harmony = new Harmony("dev.baru.neos.NeosVRMImporter");
            harmony.PatchAll();
            Engine.Current.RunPostInit(PatchAssetHelper);
            _ = VRM2GLB.UpdateTools();
            Msg($"Blender Path: {VRM2GLB.BLENDER_EXE_PATH}");
        }

        private static void PatchAssetHelper()
        {
            Traverse.Create(typeof(AssetHelper)).Field<Dictionary<AssetClass, List<string>>>("associatedExtensions").Value[AssetClass.Model].Add("vrm");
        }

        private static bool IsVRMFile(string path)
        {
            return Path.GetExtension(path).ToLower() == ".vrm";
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
                Task.Run(async () =>
                {
                    var glbPath = _config?.GetValue(RenameOnlyConvertKey) ?? false ? VRM2GLB.RenameVRMtoGLB(file) : await VRM2GLB.ConvertVRMtoGLB(file);
                    if (glbPath != null)
                    {
                        Msg($"Converted: {glbPath}");
                        var wrapperMethod = AccessTools.Method(typeof(ModelImporter), "ImportModelWrapper");
                        targetSlot.RunSynchronously(() =>
                        {
                            targetSlot.StartCoroutine((IEnumerator<Context>)wrapperMethod.Invoke(null, new object[] { glbPath, targetSlot, settings, assetsSlot, progressIndicator, tcs }));
                        }, true);
                        var texDirPath = glbPath.Substring(0, glbPath.Length - 4) + ".vrm.textures";
                        if (_config?.GetValue(ImportTexturesKey) ?? false && Directory.Exists(texDirPath))
                        {
                            Msg($"Found textures to import: {texDirPath}");
                            var texPaths = Directory.GetFiles(texDirPath).Where(f => AssetHelper.IdentifyClass(f) == AssetClass.Texture);
                            UniversalImporter.Import(AssetClass.Texture, texPaths, targetSlot.World, targetSlot.GlobalPosition, targetSlot.GlobalRotation, true);
                        }
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