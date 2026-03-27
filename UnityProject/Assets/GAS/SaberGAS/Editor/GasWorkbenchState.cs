using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal sealed class GasWorkbenchState
    {
        private readonly List<UnityEngine.Object> _emptyObjects = new List<UnityEngine.Object>();

        public event Action StateChanged;

        public CombatDefinitionCatalogAsset Catalog { get; private set; }

        public UnityEngine.Object SelectedObject { get; private set; }

        public UnityEngine.Object FocusedGraphObject { get; private set; }

        public int Version { get; private set; }

        public void BindCatalog(CombatDefinitionCatalogAsset catalog)
        {
            Catalog = catalog;
            SelectedObject = catalog;
            FocusedGraphObject = null;
            Touch();
        }

        public void Select(UnityEngine.Object value, bool clearGraphFocus = false)
        {
            var changed = false;

            if (SelectedObject != value)
            {
                SelectedObject = value;
                changed = true;
            }

            if (clearGraphFocus && FocusedGraphObject != null)
            {
                FocusedGraphObject = null;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            Touch();
        }

        public void FocusGraph(UnityEngine.Object value)
        {
            if (FocusedGraphObject == value)
            {
                return;
            }

            FocusedGraphObject = value;
            Touch();
        }

        public void Refresh()
        {
            Touch();
        }

        public void MarkDirtyAndRefresh(UnityEngine.Object value)
        {
            if (value != null)
            {
                EditorUtility.SetDirty(value);
            }

            if (Catalog != null)
            {
                EditorUtility.SetDirty(Catalog);
            }

            AssetDatabase.SaveAssets();
            Touch();
        }

        public IReadOnlyList<UnityEngine.Object> GetSubAssets<T>() where T : UnityEngine.Object
        {
            if (Catalog == null)
            {
                return _emptyObjects;
            }

            var assetPath = AssetDatabase.GetAssetPath(Catalog);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return _emptyObjects;
            }

            var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var results = new List<UnityEngine.Object>();
            for (var i = 0; i < assets.Length; i++)
            {
                var asset = assets[i];
                if (asset == null || asset == Catalog || asset is not T)
                {
                    continue;
                }

                results.Add(asset);
            }

            results.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
            return results;
        }

        public int GetInitialActorCount()
        {
            if (Catalog == null)
            {
                return 0;
            }

            var serializedObject = new SerializedObject(Catalog);
            var initialActors = serializedObject.FindProperty("_initialActors");
            return initialActors == null ? 0 : initialActors.arraySize;
        }

        private void Touch()
        {
            Version += 1;
            StateChanged?.Invoke();
        }
    }
}
