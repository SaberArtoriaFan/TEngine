using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    internal delegate void GasModuleDrawer(
        SerializedProperty property,
        CombatDefinitionCatalogAsset catalog,
        UnityEngine.Object ownerAsset);

    internal static class GasModuleDrawerRegistry
    {
        private static readonly Dictionary<Type, GasModuleDrawer> Drawers = new Dictionary<Type, GasModuleDrawer>();

        public static void Register<TModule>(GasModuleDrawer drawer)
            where TModule : CombatAuthoringModule
        {
            Register(typeof(TModule), drawer);
        }

        public static void Register(Type moduleType, GasModuleDrawer drawer)
        {
            if (moduleType == null || drawer == null)
            {
                return;
            }

            Drawers[moduleType] = drawer;
        }

        public static bool TryDraw(
            SerializedProperty property,
            CombatDefinitionCatalogAsset catalog,
            UnityEngine.Object ownerAsset)
        {
            var value = property?.managedReferenceValue;
            if (value == null)
            {
                return false;
            }

            var valueType = value.GetType();
            if (!Drawers.TryGetValue(valueType, out var drawer))
            {
                return false;
            }

            drawer(property, catalog, ownerAsset);
            return true;
        }
    }
}
