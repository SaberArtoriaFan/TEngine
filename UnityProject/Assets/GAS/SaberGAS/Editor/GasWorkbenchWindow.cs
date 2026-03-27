using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saber.GAS.Editor
{
    public sealed class GasWorkbenchWindow : EditorWindow
    {
        private readonly GasWorkbenchState _state = new GasWorkbenchState();

        private ObjectField _catalogField;
        private ScrollView _navigationPane;
        private Label _graphTitle;
        private Label _graphDescription;
        private GasGraphView _graphView;
        private VisualElement _previewPanel;
        private Label _previewSummary;
        private ScrollView _previewList;
        private IMGUIContainer _inspectorContainer;
        private UnityEditor.Editor _cachedInspector;
        private GasPreviewReport _previewReport;
        private Vector2 _inspectorScrollPosition;
        private ToolbarButton _clearFocusButton;

        [MenuItem("Saber.GAS/Workbench")]
        public static void OpenFromMenu()
        {
            var selectedCatalog = Selection.activeObject as CombatDefinitionCatalogAsset;
            Open(selectedCatalog);
        }

        public static void Open(CombatDefinitionCatalogAsset catalog)
        {
            var window = GetWindow<GasWorkbenchWindow>();
            window.titleContent = new GUIContent("GAS Workbench");
            window.minSize = new Vector2(1200f, 700f);
            window.Show();
            window.Focus();
            window.BindCatalog(catalog);
        }

        private void OnEnable()
        {
            _state.StateChanged += RefreshViews;
            Selection.selectionChanged += HandleGlobalSelectionChanged;
        }

        private void OnDisable()
        {
            _state.StateChanged -= RefreshViews;
            Selection.selectionChanged -= HandleGlobalSelectionChanged;
            DestroyCachedInspector();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.style.paddingLeft = 6f;
            rootVisualElement.style.paddingRight = 6f;
            rootVisualElement.style.paddingTop = 6f;
            rootVisualElement.style.paddingBottom = 6f;

            rootVisualElement.Add(CreateToolbar());
            rootVisualElement.Add(CreateWorkspaceLayout());
            RefreshViews();
        }

        private VisualElement CreateToolbar()
        {
            var toolbar = new Toolbar();

            _catalogField = new ObjectField("Root Config")
            {
                objectType = typeof(CombatDefinitionCatalogAsset),
                allowSceneObjects = false,
            };
            _catalogField.style.minWidth = 320f;
            _catalogField.RegisterValueChangedCallback(evt =>
            {
                BindCatalog(evt.newValue as CombatDefinitionCatalogAsset);
            });
            toolbar.Add(_catalogField);

            var refreshButton = new ToolbarButton(() => _state.Refresh())
            {
                text = "刷新",
            };
            toolbar.Add(refreshButton);

            var previewButton = new ToolbarButton(RunPreview)
            {
                text = "运行预览",
            };
            toolbar.Add(previewButton);

            _clearFocusButton = new ToolbarButton(() =>
            {
                _state.FocusGraph(null);
                _state.Select(_state.SelectedObject ?? _state.Catalog);
            })
            {
                text = "返回全图",
            };
            toolbar.Add(_clearFocusButton);

            var selectButton = new ToolbarButton(() =>
            {
                if (_state.SelectedObject != null)
                {
                    Selection.activeObject = _state.SelectedObject;
                    EditorGUIUtility.PingObject(_state.SelectedObject);
                }
            })
            {
                text = "定位选中资源",
            };
            toolbar.Add(selectButton);

            return toolbar;
        }

        private VisualElement CreateWorkspaceLayout()
        {
            var rootSplit = new TwoPaneSplitView(0, 280, TwoPaneSplitViewOrientation.Horizontal);
            rootSplit.style.flexGrow = 1f;

            _navigationPane = new ScrollView(ScrollViewMode.Vertical);
            _navigationPane.style.flexGrow = 1f;
            _navigationPane.style.marginRight = 6f;
            rootSplit.Add(_navigationPane);

            var contentSplit = new TwoPaneSplitView(1, 420, TwoPaneSplitViewOrientation.Horizontal);
            contentSplit.style.flexGrow = 1f;
            rootSplit.Add(contentSplit);

            var graphPane = new VisualElement();
            graphPane.style.flexGrow = 1f;
            graphPane.style.paddingLeft = 12f;
            graphPane.style.paddingRight = 12f;
            graphPane.style.paddingTop = 12f;
            graphPane.style.paddingBottom = 12f;
            graphPane.style.backgroundColor = new Color(0.12f, 0.13f, 0.15f, 1f);

            _graphTitle = new Label();
            _graphTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _graphTitle.style.fontSize = 18;
            _graphTitle.style.marginBottom = 8f;
            graphPane.Add(_graphTitle);

            _graphDescription = new Label();
            _graphDescription.style.whiteSpace = WhiteSpace.Normal;
            _graphDescription.style.color = new Color(0.82f, 0.84f, 0.88f, 1f);
            graphPane.Add(_graphDescription);

            var graphHint = new HelpBox(
                "图面板已支持高频 SO 引用关系的浏览与连线写回。复杂标量参数仍放在右侧 Inspector 里编辑；单击节点只选中，双击节点才进入局部视图。",
                HelpBoxMessageType.Info);
            graphHint.style.marginTop = 12f;
            graphPane.Add(graphHint);

            _graphView = new GasGraphView(_state);
            _graphView.style.marginTop = 12f;
            graphPane.Add(_graphView);

            _previewPanel = new VisualElement();
            _previewPanel.style.marginTop = 12f;
            _previewPanel.style.paddingTop = 8f;
            _previewPanel.style.paddingBottom = 8f;
            _previewPanel.style.paddingLeft = 8f;
            _previewPanel.style.paddingRight = 8f;
            _previewPanel.style.backgroundColor = new Color(0.1f, 0.11f, 0.13f, 1f);

            _previewSummary = new Label("预览未运行。");
            _previewSummary.style.whiteSpace = WhiteSpace.Normal;
            _previewSummary.style.unityFontStyleAndWeight = FontStyle.Bold;
            _previewPanel.Add(_previewSummary);

            _previewList = new ScrollView(ScrollViewMode.Vertical);
            _previewList.style.maxHeight = 220f;
            _previewList.style.marginTop = 8f;
            _previewPanel.Add(_previewList);
            graphPane.Add(_previewPanel);

            contentSplit.Add(graphPane);

            _inspectorContainer = new IMGUIContainer(DrawInspectorScrollView);
            _inspectorContainer.style.flexGrow = 1f;
            _inspectorContainer.style.paddingLeft = 10f;
            _inspectorContainer.style.paddingRight = 6f;
            _inspectorContainer.style.paddingTop = 8f;
            _inspectorContainer.style.paddingBottom = 8f;
            contentSplit.Add(_inspectorContainer);

            return rootSplit;
        }

        private void BindCatalog(CombatDefinitionCatalogAsset catalog)
        {
            if (_state.Catalog != catalog)
            {
                _previewReport = null;
            }

            _state.BindCatalog(catalog);
        }

        private void HandleGlobalSelectionChanged()
        {
            var activeObject = Selection.activeObject;
            if (activeObject == null)
            {
                return;
            }

            if (activeObject is CombatDefinitionCatalogAsset selectedCatalog)
            {
                _state.BindCatalog(selectedCatalog);
                return;
            }

            if (_state.Catalog != null && GasEditorUtility.BelongsToCatalog(_state.Catalog, activeObject))
            {
                _state.Select(activeObject);
            }
        }

        private void RefreshViews()
        {
            if (_catalogField != null && _catalogField.value != _state.Catalog)
            {
                _catalogField.SetValueWithoutNotify(_state.Catalog);
            }

            if (_clearFocusButton != null)
            {
                _clearFocusButton.SetEnabled(_state.FocusedGraphObject != null);
            }

            RebuildNavigation();
            RefreshGraphPlaceholder();
            _graphView?.Rebuild();
            RefreshPreviewPanel();
            _inspectorContainer?.MarkDirtyRepaint();
        }

        private void RebuildNavigation()
        {
            if (_navigationPane == null)
            {
                return;
            }

            _navigationPane.Clear();

            if (_state.Catalog == null)
            {
                _navigationPane.Add(new HelpBox(
                    "请选择一个 CombatDefinitionCatalogAsset 作为 GAS Workbench 的根配置。",
                    HelpBoxMessageType.Info));
                return;
            }

            _navigationPane.Add(CreateSectionHeader("概览"));
            _navigationPane.Add(CreateSelectionButton(
                string.Format("Root Config: {0}", _state.Catalog.name),
                _state.Catalog));

            _navigationPane.Add(CreateSectionHeader("子配置"));
            AddAssetGroup<AbilityDefinitionAsset>("Abilities");
            AddAssetGroup<EffectDefinitionAsset>("Effects");
            AddAssetGroup<TriggerDefinitionAsset>("Triggers");
            AddAssetGroup<CombatActorTemplateAsset>("Actor Templates");

            var initialActorsInfo = new HelpBox(
                string.Format("Initial Actors: {0}", _state.GetInitialActorCount()),
                HelpBoxMessageType.None);
            initialActorsInfo.style.marginTop = 8f;
            _navigationPane.Add(initialActorsInfo);
        }

        private void AddAssetGroup<T>(string title) where T : UnityEngine.Object
        {
            var assets = _state.GetSubAssets<T>();
            var foldout = new Foldout
            {
                text = string.Format("{0} ({1})", title, assets.Count),
                value = true,
            };

            if (assets.Count == 0)
            {
                foldout.Add(new Label("暂无资源"));
            }

            for (var i = 0; i < assets.Count; i++)
            {
                foldout.Add(CreateSelectionButton(assets[i].name, assets[i]));
            }

            _navigationPane.Add(foldout);
        }

        private VisualElement CreateSectionHeader(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginTop = 8f;
            label.style.marginBottom = 4f;
            return label;
        }

        private Button CreateSelectionButton(string label, UnityEngine.Object target)
        {
            var button = new Button(() =>
            {
                _state.Select(target, target == _state.Catalog);
                if (target != null)
                {
                    Selection.activeObject = target;
                }
            })
            {
                text = label,
            };
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.marginBottom = 2f;
            return button;
        }

        private void RefreshGraphPlaceholder()
        {
            if (_graphTitle == null || _graphDescription == null)
            {
                return;
            }

            var selectedObject = _state.SelectedObject ?? _state.Catalog;
            var focusedObject = _state.FocusedGraphObject;
            if (selectedObject == null)
            {
                _graphTitle.text = "未选择根配置";
                _graphDescription.text = "请先在工具栏里指定一个 CombatDefinitionCatalogAsset。";
                return;
            }

            if (focusedObject != null)
            {
                _graphTitle.text = string.Format("局部视图: {0}", focusedObject.name);
                _graphDescription.text = "当前是局部视图。单击节点只会选中并在右侧编辑；双击节点才会切到该节点的局部关系图。点击“返回全图”或选中 Root Config 可以回到完整视图。";
                return;
            }

            _graphTitle.text = string.Format("当前选中: {0}", selectedObject.name);
            _graphDescription.text = BuildSelectionSummary(selectedObject);
        }

        private static string BuildSelectionSummary(UnityEngine.Object selectedObject)
        {
            return selectedObject switch
            {
                CombatDefinitionCatalogAsset catalog => string.Format(
                    "根配置资产。图面板会把同一 .asset 里的 Ability、Effect、Trigger、ActorTemplate 关系投影出来。单击节点选中，双击节点进入局部视图。当前文件名: {0}",
                    catalog.name),
                AbilityDefinitionAsset => "Ability 节点支持连接 Effects、PeriodicEffects、EndEffects 和 Triggers。",
                EffectDefinitionAsset => "Effect 节点支持连接 RemovedTargetEffects 和 Triggers。",
                TriggerDefinitionAsset => "Trigger 节点支持连接 ApplyEffect、ActivateAbility、CancelAbility 这三类高频动作目标。",
                CombatActorTemplateAsset => "ActorTemplate 节点支持连接 GrantedAbilities 和 ActorTriggers。",
                _ => "该对象暂未配置专属图摘要。"
            };
        }

        private void RunPreview()
        {
            _previewReport = GasPreviewService.Build(_state.Catalog);
            RefreshPreviewPanel();
        }

        private void RefreshPreviewPanel()
        {
            if (_previewPanel == null || _previewSummary == null || _previewList == null)
            {
                return;
            }

            _previewList.Clear();

            if (_state.Catalog == null)
            {
                _previewSummary.text = "预览未运行。请先选择一个根配置。";
                return;
            }

            if (_previewReport == null)
            {
                _previewSummary.text = "预览未运行。点击工具栏里的“运行预览”生成当前 Catalog 的构建快照。";
                return;
            }

            if (!_previewReport.Success)
            {
                _previewSummary.text = string.Format("预览失败: {0}", _previewReport.ErrorMessage);
                if (_previewReport.ErrorAsset != null)
                {
                    var focusButton = new Button(() =>
                    {
                        _state.Select(_previewReport.ErrorAsset);
                        Selection.activeObject = _previewReport.ErrorAsset;
                        EditorGUIUtility.PingObject(_previewReport.ErrorAsset);
                    })
                    {
                        text = string.Format("定位出错资产: {0}", _previewReport.ErrorAsset.name),
                    };
                    _previewList.Add(focusButton);
                }
                return;
            }

            _previewSummary.text = "预览成功：下面展示当前 Catalog 初始化后会构建出的 definitions 与初始 Actor。";
            AddPreviewGroup("Abilities", _previewReport.Abilities);
            AddPreviewGroup("Effects", _previewReport.Effects);
            AddPreviewGroup("Triggers", _previewReport.Triggers);
            AddPreviewGroup("Initial Actors", _previewReport.InitialActors);
        }

        private void AddPreviewGroup(string title, List<string> lines)
        {
            var foldout = new Foldout
            {
                text = string.Format("{0} ({1})", title, lines.Count),
                value = false,
            };

            if (lines.Count == 0)
            {
                foldout.Add(new Label("暂无。"));
            }
            else
            {
                for (var i = 0; i < lines.Count; i++)
                {
                    var label = new Label(lines[i]);
                    label.style.whiteSpace = WhiteSpace.Normal;
                    foldout.Add(label);
                }
            }

            _previewList.Add(foldout);
        }

        private void DrawInspectorScrollView()
        {
            _inspectorScrollPosition = EditorGUILayout.BeginScrollView(_inspectorScrollPosition);
            try
            {
                DrawInspector();
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawInspector()
        {
            var selectedObject = _state.SelectedObject ?? _state.Catalog;
            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox("请选择一个根配置或子配置资源。", MessageType.Info);
                return;
            }

            UnityEditor.Editor.CreateCachedEditor(selectedObject, null, ref _cachedInspector);
            if (_cachedInspector == null)
            {
                EditorGUILayout.HelpBox("无法创建 Inspector。", MessageType.Warning);
                return;
            }

            var changedBefore = GUI.changed;
            GUI.changed = false;
            _cachedInspector.OnInspectorGUI();
            if (GUI.changed)
            {
                _state.Refresh();
            }

            GUI.changed |= changedBefore;
        }

        private void DestroyCachedInspector()
        {
            if (_cachedInspector == null)
            {
                return;
            }

            DestroyImmediate(_cachedInspector);
            _cachedInspector = null;
        }
    }
}
