#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Saber.GAS.Examples.QuickStart.Editor
{
    [CustomEditor(typeof(GasActorRuntimeDisplay))]
    public sealed class GasActorRuntimeDisplayEditor : UnityEditor.Editor
    {
        private SerializedProperty _runtimeProvider;
        private SerializedProperty _actorId;
        private SerializedProperty _title;
        private SerializedProperty _autoFindProviderInScene;
        private SerializedProperty _autoRefreshInPlayMode;
        private SerializedProperty _refreshInterval;
        private SerializedProperty _maxEntriesPerSection;
        private SerializedProperty _section;

        private void OnEnable()
        {
            _runtimeProvider = serializedObject.FindProperty("_runtimeProvider");
            _actorId = serializedObject.FindProperty("_actorId");
            _title = serializedObject.FindProperty("_title");
            _autoFindProviderInScene = serializedObject.FindProperty("_autoFindProviderInScene");
            _autoRefreshInPlayMode = serializedObject.FindProperty("_autoRefreshInPlayMode");
            _refreshInterval = serializedObject.FindProperty("_refreshInterval");
            _maxEntriesPerSection = serializedObject.FindProperty("_maxEntriesPerSection");
            _section = serializedObject.FindProperty("_section");
        }

        public override void OnInspectorGUI()
        {
            var view = (GasActorRuntimeDisplay)target;

            serializedObject.Update();

            EditorGUILayout.LabelField("Binding", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_runtimeProvider);
            EditorGUILayout.PropertyField(_actorId);
            EditorGUILayout.PropertyField(_title);
            EditorGUILayout.PropertyField(_autoFindProviderInScene);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Refresh", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_autoRefreshInPlayMode);
            EditorGUILayout.PropertyField(_refreshInterval);
            EditorGUILayout.PropertyField(_maxEntriesPerSection);

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Section Switch", EditorStyles.boldLabel);
            DrawSectionButtons(view);

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Refresh Now"))
            {
                Undo.RecordObject(view, "Refresh GAS Actor Runtime");
                view.RefreshInspectorContent();
                EditorUtility.SetDirty(view);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(view.InspectorStatus);
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Inspector Output", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextArea(view.InspectorText, GUILayout.MinHeight(260f));
            }

            if (Application.isPlaying && view.AutoRefreshInPlayMode)
            {
                Repaint();
            }
        }

        private void DrawSectionButtons(GasActorRuntimeDisplay view)
        {
            var labels = new[]
            {
                "Overview",
                "AttributeSet",
                "Ability",
                "Effect",
                "Trigger",
            };

            var selected = GUILayout.Toolbar((int)view.Section, labels);
            if (selected != (int)view.Section)
            {
                Undo.RecordObject(view, "Switch GAS Runtime Section");
                view.SetSection((GasActorRuntimeDisplay.InspectorSection)selected);
                _section.intValue = selected;
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(view);
            }
        }
    }
}
#endif
