using Saber.GAS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Editor
{
    [CustomPropertyDrawer(typeof(ResourceStateAuthoringData))]
    internal sealed class ResourceStateAuthoringDataDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GasResourceDrawerLayout.GetFoldoutHeight(4);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GasResourceDrawerLayout.DrawFoldoutLayout(position, property, label, bodyRect =>
            {
                GasResourceEditorUtility.DrawResourceIdPopup(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_resourceId"), new GUIContent("资源 Id"));
                EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_current"), new GUIContent("当前值"), true);
                EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_max"), new GUIContent("最大值"), true);
                EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_regenPerTick"), new GUIContent("每 Tick 回复"), true);
            });
        }
    }

    [CustomPropertyDrawer(typeof(ResourceCostAuthoringData))]
    internal sealed class ResourceCostAuthoringDataDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GasResourceDrawerLayout.GetFoldoutHeight(2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GasResourceDrawerLayout.DrawFoldoutLayout(position, property, label, bodyRect =>
            {
                GasResourceEditorUtility.DrawResourceIdPopup(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_resourceId"), new GUIContent("资源 Id"));
                EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_amount"), new GUIContent("消耗量"), true);
            });
        }
    }

    [CustomPropertyDrawer(typeof(ResourceDeltaAuthoringData))]
    internal sealed class ResourceDeltaAuthoringDataDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return GasResourceDrawerLayout.GetFoldoutHeight(2);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GasResourceDrawerLayout.DrawFoldoutLayout(position, property, label, bodyRect =>
            {
                GasResourceEditorUtility.DrawResourceIdPopup(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_resourceId"), new GUIContent("资源 Id"));
                EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_amount"), new GUIContent("变化量"), true);
            });
        }
    }

    [CustomPropertyDrawer(typeof(ShieldSemanticAuthoringData))]
    internal sealed class ShieldSemanticAuthoringDataDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            var height = EditorGUIUtility.singleLineHeight;
            height += GasResourceDrawerLayout.GetLineHeight();
            height += GasResourceDrawerLayout.GetLineHeight();
            height += GasResourceDrawerLayout.GetLineHeight();
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("_requiredImpactTags"), true) + EditorGUIUtility.standardVerticalSpacing;
            height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative("_blockedImpactTags"), true) + EditorGUIUtility.standardVerticalSpacing;
            height += GasResourceDrawerLayout.GetLineHeight();
            height += GasResourceDrawerLayout.GetLineHeight();
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel += 1;
            var bodyRect = new Rect(
                position.x,
                position.y + GasResourceDrawerLayout.GetLineHeight(),
                position.width,
                position.height - GasResourceDrawerLayout.GetLineHeight());

            EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_name"), new GUIContent("名称"));
            GasResourceEditorUtility.DrawResourceIdPopup(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_protectedResourceId"), new GUIContent("保护资源 Id"));
            EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_capacity"), new GUIContent("容量"), true);

            GasResourceDrawerLayout.DrawPropertyWithAutoHeight(ref bodyRect, property.FindPropertyRelative("_requiredImpactTags"), "所需 Impact 标签");
            GasResourceDrawerLayout.DrawPropertyWithAutoHeight(ref bodyRect, property.FindPropertyRelative("_blockedImpactTags"), "阻断 Impact 标签");

            EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_refreshCapacityOnReapply"), new GUIContent("重复施加时刷新容量"));
            EditorGUI.PropertyField(GasResourceDrawerLayout.NextLine(ref bodyRect), property.FindPropertyRelative("_removeSourceEffectWhenDepleted"), new GUIContent("耗尽时移除来源 Effect"));

            EditorGUI.indentLevel -= 1;
            EditorGUI.EndProperty();
        }
    }

    internal static class GasResourceDrawerLayout
    {
        public static float GetLineHeight()
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }

        public static float GetFoldoutHeight(int lines)
        {
            return EditorGUIUtility.singleLineHeight + lines * GetLineHeight();
        }

        public static Rect NextLine(ref Rect bodyRect)
        {
            var line = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, EditorGUIUtility.singleLineHeight);
            bodyRect.y += GetLineHeight();
            return line;
        }

        public static void DrawFoldoutLayout(Rect position, SerializedProperty property, GUIContent label, System.Action<Rect> drawBody)
        {
            EditorGUI.BeginProperty(position, label, property);

            var headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel += 1;
                var bodyRect = new Rect(position.x, position.y + GetLineHeight(), position.width, position.height - GetLineHeight());
                drawBody?.Invoke(bodyRect);
                EditorGUI.indentLevel -= 1;
            }

            EditorGUI.EndProperty();
        }

        public static void DrawPropertyWithAutoHeight(ref Rect bodyRect, SerializedProperty property, string label)
        {
            var height = EditorGUI.GetPropertyHeight(property, true);
            var rect = new Rect(bodyRect.x, bodyRect.y, bodyRect.width, height);
            EditorGUI.PropertyField(rect, property, new GUIContent(label), true);
            bodyRect.y += height + EditorGUIUtility.standardVerticalSpacing;
        }
    }

}
