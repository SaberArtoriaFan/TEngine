using System.Collections;
using System.Collections.Generic;
using Herta;
using UnityEditor;
using UnityEngine;
//猫咪狂梦
[CustomPropertyDrawer(typeof(FP))]
public class FPDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var rawProp = property.FindPropertyRelative("RawValue");
        float value = rawProp.longValue / 65536f;

        // Tooltip 内容
        GUIContent content = new GUIContent(label.text, $"RawValue: {rawProp.longValue}");

        // 绘制
        float newValue = EditorGUI.FloatField(position, content, value);

        if (!Mathf.Approximately(value, newValue))
        {
            rawProp.longValue = FP.FromFloat_SAFE(newValue).RawValue;
            // 标记为脏（确保场景与资源正确保存）
            EditorUtility.SetDirty(property.serializedObject.targetObject);
        }


        EditorGUI.EndProperty();
    }

}



[CustomPropertyDrawer(typeof(FPVector2))]
public class FPVector2Drawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // 主标签
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // 计算两半区域
        float halfWidth = position.width / 2f;

        // X 字段
        Rect xRect = new Rect(position.x, position.y, halfWidth - 2, position.height);
        Rect xLabelRect = new Rect(xRect.x, xRect.y, 14, xRect.height); // 标签宽度 14
        Rect xFieldRect = new Rect(xRect.x + 16, xRect.y, xRect.width - 16, xRect.height);

        // Y 字段
        Rect yRect = new Rect(position.x + halfWidth, position.y, halfWidth - 2, position.height);
        Rect yLabelRect = new Rect(yRect.x, yRect.y, 14, yRect.height);
        Rect yFieldRect = new Rect(yRect.x + 16, yRect.y, yRect.width - 16, yRect.height);

        var xProp = property.FindPropertyRelative("X");
        var yProp = property.FindPropertyRelative("Y");

        if (xProp != null && yProp != null)
        {
            // 绘制前缀标签
            EditorGUI.LabelField(xLabelRect, "X");
            EditorGUI.LabelField(yLabelRect, "Y");

            // 绘制 FP
            EditorGUI.PropertyField(xFieldRect, xProp, GUIContent.none);
            EditorGUI.PropertyField(yFieldRect, yProp, GUIContent.none);
        }
        else
        {
            EditorGUI.LabelField(position, "Error: FPVector2 fields missing");
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight;
    }
}

