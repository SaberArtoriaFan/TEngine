using System.Collections.Generic;
using System.IO;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal static class GasModuleAssetUtility
    {
        [MenuItem("Saber.GAS/Normalize Module Assets")]
        public static void NormalizeModuleAssets()
        {
            var assetPaths = CollectGasAssetPaths();
            var changedCount = 0;
            changedCount += NormalizeAllGasAssetFiles();
            changedCount += NormalizeAssets<AbilityDefinitionAsset>(static asset => asset.EnsureModulesInitialized());
            changedCount += NormalizeAssets<EffectDefinitionAsset>(static asset => asset.EnsureModulesInitialized());
            changedCount += NormalizeAssets<TriggerDefinitionAsset>(static asset => asset.EnsureModulesInitialized());

            AssetDatabase.SaveAssets();
            var reserializedCount = ForceReserializeGasAssets(assetPaths);
            AssetDatabase.Refresh();

            Debug.Log(string.Format(
                "Saber.GAS: module asset normalization finished. Changed {0} assets and reserialized {1} asset files.",
                changedCount,
                reserializedCount));
        }

        [MenuItem("Saber.GAS/Force Reserialize GAS Assets")]
        public static void ForceReserializeGasAssetsMenu()
        {
            var reserializedCount = ForceReserializeGasAssets(CollectGasAssetPaths());
            AssetDatabase.Refresh();
            Debug.Log(string.Format("Saber.GAS: force reserialized {0} asset files.", reserializedCount));
        }

        private static int NormalizeAllGasAssetFiles()
        {
            var changedCount = 0;
            var rootPath = Path.Combine(Application.dataPath, "GAS");
            if (!Directory.Exists(rootPath))
            {
                return 0;
            }

            var files = Directory.GetFiles(rootPath, "*.asset", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                var path = "Assets" + files[i].Substring(Application.dataPath.Length).Replace('\\', '/');
                var assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(path);
                for (var j = 0; j < assetsAtPath.Length; j++)
                {
                    switch (assetsAtPath[j])
                    {
                        case AbilityDefinitionAsset ability when ability.EnsureModulesInitialized():
                            changedCount += 1;
                            EditorUtility.SetDirty(ability);
                            break;
                        case EffectDefinitionAsset effect when effect.EnsureModulesInitialized():
                            changedCount += 1;
                            EditorUtility.SetDirty(effect);
                            break;
                        case TriggerDefinitionAsset trigger when trigger.EnsureModulesInitialized():
                            changedCount += 1;
                            EditorUtility.SetDirty(trigger);
                            break;
                    }
                }
            }

            return changedCount;
        }

        private static int NormalizeAssets<TAsset>(System.Func<TAsset, bool> normalize)
            where TAsset : ScriptableObject
        {
            var changedCount = 0;
            var guids = AssetDatabase.FindAssets("t:" + typeof(TAsset).Name);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                var assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(path);
                for (var j = 0; j < assetsAtPath.Length; j++)
                {
                    if (assetsAtPath[j] is not TAsset asset)
                    {
                        continue;
                    }

                    if (!normalize(asset))
                    {
                        continue;
                    }

                    changedCount += 1;
                    EditorUtility.SetDirty(asset);
                }
            }

            return changedCount;
        }

        private static List<string> CollectGasAssetPaths()
        {
            var assetPaths = new List<string>();
            var rootPath = Path.Combine(Application.dataPath, "GAS");
            if (!Directory.Exists(rootPath))
            {
                return assetPaths;
            }

            var files = Directory.GetFiles(rootPath, "*.asset", SearchOption.AllDirectories);
            for (var i = 0; i < files.Length; i++)
            {
                assetPaths.Add("Assets" + files[i].Substring(Application.dataPath.Length).Replace('\\', '/'));
            }

            return assetPaths;
        }

        private static int ForceReserializeGasAssets(IReadOnlyList<string> assetPaths)
        {
            if (assetPaths == null || assetPaths.Count == 0)
            {
                return 0;
            }

            AssetDatabase.ForceReserializeAssets(assetPaths, ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata);
            return assetPaths.Count;
        }
    }
}
