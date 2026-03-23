using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameLogic.Regicide;
using GameProto.Regicide;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    [Window(UILayer.UI, location: "RegicideBattleUI")]
    public sealed class RegicideBattleUI : UIWindow
    {
        private const float CardWidth = 170f;
        private const float CardHeight = 220f;
        private const float CardMaxSpacing = 180f;
        private const float CardMinSpacing = 98f;
        private const float UiSafePadding = 16f;

        private sealed class CardView
        {
            public GameObject Root;
            public RectTransform Rect;
            public Button Button;
            public Image Background;
            public Text Title;
            public Text Desc;
            public CanvasGroup CanvasGroup;
            public RegicideHandCardInteractProxy InteractProxy;
            public int CardIndex;
            public bool IsDragging;
            public Vector2 LayoutAnchoredPos;
            public float LayoutAngle;
            public Tween MoveTween;
            public Tween RotateTween;
            public Tween ScaleTween;
            public bool LayoutInitialized;
        }

        private sealed class PlayedCardView
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
            public Text Title;
            public Text Desc;
        }

        private sealed class PlayerHeadLabelView
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
            public Text Name;
            public Text Hand;
            public Image Avatar;
            public Text Phase;
            public string PlayerId = string.Empty;
        }

        private sealed class PreviewCardView
        {
            public GameObject Root;
            public RectTransform Rect;
            public Button Button;
            public Image Background;
            public Text Label;
            public RegicidePreviewCardInteractProxy InteractProxy;
            public int SlotIndex;
            public int HandCardIndex;
        }

        private Button _btnPlay;
        private Button _btnPass;
        private Button _btnDefend;
        private Button _btnHelp;
        private Button _btnHelpClose;
        private Button _btnPreviewConfirm;
        private Button _cardTemplateButton;
        private Button _previewCardTemplateButton;

        private Text _txtPlayLabel;
        private Text _txtPassLabel;
        private Text _txtDefendLabel;
        private Text _txtHelpLabel;
        private Text _txtHelpCloseLabel;
        private Text _txtPreviewConfirmLabel;

        private Text _txtEnemyInfo;
        private Text _txtPlayerInfo;
        private Text _txtActionHint;
        private Text _txtSelectedCard;
        private Text _txtOtherPlayers;
        private Text _txtBattleLog;
        private Text _txtHelpContent;
        private Text _txtStageFx;
        private Text _txtRoundInfo;
        private Text _txtPreviewCardTemplateLabel;
        private Text _txtDiscardPile;
        private Text _txtDrawPile;

        private RectTransform _rectHandArea;
        private RectTransform _rectPlayedCardsArea;
        private ScrollRect _scrollBattleLog;
        private GameObject _playedCardTemplate;
        private GameObject _playerHeadTemplate;
        private RectTransform _rectRootCanvas;
        private RectTransform _rectActorInfoLayer;
        private RectTransform _rectPlayPreview;
        private RectTransform _rectPreviewCardsRoot;
        private Transform _rectEnemyAreaRoot;
        private Transform _rectInfoAreaRoot;
        private Transform _rectPlayerAreaRoot;
        private Transform _rectLogAreaRoot;
        private Image _imgPlayPreview;
        private Text _txtPlayPreviewHint;

        private GameObject _helpMask;
        private bool _actionProcessing;
        private bool _navigatingResult;
        private bool _playingFxQueue;
        private bool _pendingAutoScroll;
        private bool _templateMissingLogged;
        private bool _playedCardTemplateMissingLogged;
        private bool _playingActionFxQueue;
        private bool _logPanelVisible;
        private bool _isDraggingCard;
        private bool _dragPointerInPreview;
        private bool _enemyTransitionLocked;

        private readonly List<int> _selectedCardIndices = new List<int>();
        private readonly List<CardView> _cardViews = new List<CardView>();
        private readonly List<PlayedCardView> _playedCardViews = new List<PlayedCardView>();
        private readonly List<PlayerHeadLabelView> _playerHeadLabels = new List<PlayerHeadLabelView>();
        private readonly List<PreviewCardView> _previewCardViews = new List<PreviewCardView>();
        private readonly List<string> _visiblePlayedCards = new List<string>();
        private readonly Queue<string> _pendingFxQueue = new Queue<string>();
        private readonly List<RegicideActionBroadcastPayload> _pendingActionFxList = new List<RegicideActionBroadcastPayload>();
        private readonly HashSet<long> _pendingActionFxSeqSet = new HashSet<long>();
        private readonly Dictionary<string, RegicidePublicPlayerState> _publicPlayerLookup = new Dictionary<string, RegicidePublicPlayerState>(StringComparer.Ordinal);

        private int _selectedNextPlayerIndex = -1;
        private int _hoverCardIndex = -1;
        private int _draggingCardIndex = -1;
        private int _knownLogCount;
        private int _lastRenderedLogCount = -1;
        private int _lastRenderedActionCount = -1;
        private int _playedCardsVersion;
        private string _knownSessionId = string.Empty;
        private string _feedback = string.Empty;
        private string _lastRenderedFeedback = string.Empty;
        private long _lastPlayedActionSequence;
        private Vector2 _dragPointerOffset;

        protected override void ScriptGenerator()
        {
            _btnPlay = FindComponentByName<Button>("m_btnPlayFirst");
            _btnPass = FindComponentByName<Button>("m_btnPass");
            _btnDefend = FindComponentByName<Button>("m_btnDefend");
            _btnHelp = FindComponentByName<Button>("m_btnHelp");
            _btnHelpClose = FindComponentByName<Button>("m_btnHelpClose");
            _btnPreviewConfirm = FindComponentByName<Button>("m_btnPreviewConfirm");
            _cardTemplateButton = FindComponentByName<Button>("m_btnCardTemplate");
            _previewCardTemplateButton = FindComponentByName<Button>("m_btnPreviewCardTemplate");

            _txtPlayLabel = FindComponentByName<Text>("m_txtPlayLabel") ?? _btnPlay?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtPassLabel = FindComponentByName<Text>("m_txtPassLabel") ?? _btnPass?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtDefendLabel = FindComponentByName<Text>("m_txtDefendLabel") ?? _btnDefend?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtHelpLabel = FindComponentByName<Text>("m_txtHelpLabel") ?? _btnHelp?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtHelpCloseLabel = FindComponentByName<Text>("m_txtHelpCloseLabel") ?? _btnHelpClose?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtPreviewConfirmLabel = FindComponentByName<Text>("m_txtPreviewConfirmLabel") ?? _btnPreviewConfirm?.transform.Find("m_txtLabel")?.GetComponent<Text>();

            _txtEnemyInfo = FindComponentByName<Text>("m_txtEnemyInfo");
            _txtPlayerInfo = FindComponentByName<Text>("m_txtPlayerInfo");
            _txtActionHint = FindComponentByName<Text>("m_txtActionHint");
            _txtSelectedCard = FindComponentByName<Text>("m_txtSelectedCard");
            _txtOtherPlayers = FindComponentByName<Text>("m_txtOtherPlayers");
            _txtBattleLog = FindComponentByName<Text>("m_txtBattle");
            _txtHelpContent = FindComponentByName<Text>("m_txtHelpContent");
            _txtStageFx = FindComponentByName<Text>("m_txtStageFx");
            _txtRoundInfo = FindComponentByName<Text>("m_txtRoundInfo");
            _txtPreviewCardTemplateLabel = FindComponentByName<Text>("m_txtPreviewCardLabel");
            _txtDiscardPile = FindComponentByName<Text>("m_txtDiscardPile");
            _txtDrawPile = FindComponentByName<Text>("m_txtDrawPile");

            _rectHandArea = FindComponentByName<RectTransform>("m_rectHandArea");
            _rectPlayedCardsArea = FindComponentByName<RectTransform>("m_rectPlayedCardsArea");
            _rectPlayPreview = FindComponentByName<RectTransform>("m_rectPlayPreview");
            _rectPreviewCardsRoot = FindComponentByName<RectTransform>("m_rectPreviewCardsRoot");
            _scrollBattleLog = FindComponentByName<ScrollRect>("m_scrollBattleLog");
            _playedCardTemplate = FindTransformByName("m_goPlayedCardTemplate")?.gameObject;
            _playerHeadTemplate = FindTransformByName("m_goPlayerSlotTemplate")?.gameObject;
            _rectRootCanvas = rectTransform;
            _imgPlayPreview = _rectPlayPreview != null ? _rectPlayPreview.GetComponent<Image>() : null;
            _txtPlayPreviewHint = FindComponentByName<Text>("m_txtPlayPreviewHint");
            _rectActorInfoLayer = FindComponentByName<RectTransform>("m_rectActorInfoLayer");
            _rectEnemyAreaRoot = FindTransformByName("m_rectEnemyArea");
            _rectInfoAreaRoot = FindTransformByName("m_rectInfoArea");
            _rectPlayerAreaRoot = FindTransformByName("m_rectPlayerArea");
            _rectLogAreaRoot = FindTransformByName("m_rectLogArea");

            Transform helpMask = FindTransformByName("m_goHelpMask");
            _helpMask = helpMask != null ? helpMask.gameObject : null;

            if (_scrollBattleLog != null)
            {
                RectTransform viewport = FindComponentByName<RectTransform>("m_viewport");
                if (viewport != null && _scrollBattleLog.viewport == null)
                {
                    _scrollBattleLog.viewport = viewport;
                }

                if (_txtBattleLog != null && _scrollBattleLog.content == null)
                {
                    _scrollBattleLog.content = _txtBattleLog.rectTransform;
                }

                _scrollBattleLog.horizontal = false;
                _scrollBattleLog.vertical = true;
            }

            if (_cardTemplateButton != null)
            {
                _cardTemplateButton.gameObject.SetActive(false);
            }

            if (_previewCardTemplateButton != null)
            {
                _previewCardTemplateButton.gameObject.SetActive(false);
            }

            if (_playedCardTemplate != null)
            {
                _playedCardTemplate.SetActive(false);
            }

            if (_helpMask != null)
            {
                _helpMask.SetActive(false);
            }

            if (_txtStageFx != null)
            {
                _txtStageFx.gameObject.SetActive(false);
            }
        }

        protected override void RegisterEvent()
        {
            AddUIEvent<RegicideStateSnapshotPayload>(RegicideEventIds.BattleSnapshotUpdated, OnBattleSnapshotUpdated);
            AddUIEvent<RegicidePublicStateSnapshotPayload>(RegicideEventIds.PublicStateSnapshotUpdated, OnPublicStateSnapshotUpdated);
            AddUIEvent<RegicideActionBroadcastPayload>(RegicideEventIds.ActionBroadcastReceived, OnActionBroadcastReceived);
            AddUIEvent<RegicideErrorPayload>(RegicideEventIds.BattleErrorReceived, OnBattleError);

            if (_btnPlay != null) _btnPlay.onClick.AddListener(OnPlayClicked);
            if (_btnPass != null) _btnPass.onClick.AddListener(OnPassClicked);
            if (_btnDefend != null) _btnDefend.onClick.AddListener(OnDefendClicked);
            if (_btnPreviewConfirm != null) _btnPreviewConfirm.onClick.AddListener(OnPreviewConfirmClicked);
            if (_btnHelp != null) _btnHelp.onClick.AddListener(OnLogToggleClicked);
            if (_btnHelpClose != null) _btnHelpClose.onClick.AddListener(OnHelpCloseClicked);
        }

protected override void OnCreate()
        {
            if (_txtPlayLabel != null) _txtPlayLabel.text = "出牌";
            if (_txtPassLabel != null) _txtPassLabel.text = "跳过回合";
            if (_txtDefendLabel != null) _txtDefendLabel.text = "辅助操作";
            if (_txtHelpLabel != null) _txtHelpLabel.text = "日志";
            if (_txtHelpCloseLabel != null) _txtHelpCloseLabel.text = "关闭";
            if (_helpMask != null) _helpMask.SetActive(false);
            if (_cardTemplateButton != null) _cardTemplateButton.gameObject.SetActive(false);
            if (_txtPreviewConfirmLabel != null) _txtPreviewConfirmLabel.text = "确认出牌";
            if (_btnPreviewConfirm != null)
            {
                _btnPreviewConfirm.gameObject.SetActive(false);
                _btnPreviewConfirm.interactable = false;
            }
            SetLogPanelVisible(false);
            _hoverCardIndex = -1;
            _draggingCardIndex = -1;
            _isDraggingCard = false;
            _dragPointerInPreview = false;
            UpdatePlayPreviewVisual(false, false, false, "左键点击手牌进行选中/取消");

            _knownSessionId = string.Empty;
            _knownLogCount = 0;
            _lastRenderedLogCount = -1;
            _lastRenderedActionCount = -1;
            _feedback = string.Empty;
            _lastRenderedFeedback = string.Empty;
            _playingFxQueue = false;
            _pendingAutoScroll = false;
            _pendingFxQueue.Clear();
            _pendingActionFxList.Clear();
            _pendingActionFxSeqSet.Clear();
            _visiblePlayedCards.Clear();
            _navigatingResult = false;
            _playingActionFxQueue = false;
            _selectedCardIndices.Clear();
            _selectedNextPlayerIndex = -1;
            _playedCardsVersion = 0;
            _lastPlayedActionSequence = 0;
            _enemyTransitionLocked = false;
            ApplyHudRuntimeStyle();
            HideStageFx();
            RenderPlayedCardsPanel();
            RefreshBattleView();
        }

        private void ApplyHudRuntimeStyle()
        {
            Transform backdrop = FindTransformByName("m_goBackdrop");
            if (backdrop != null)
            {
                backdrop.gameObject.SetActive(false);
            }

            // Ensure HUD overlay doesn't fully cover the 2D battle scene even with old prefab data.
            ApplyImageStyle(rectTransform != null ? rectTransform.GetComponent<Image>() : null, 0f, false);
            ApplyImageStyle(FindComponentByName<Image>("m_rectEnemyArea"), 0.22f, null);
            ApplyImageStyle(FindComponentByName<Image>("m_rectInfoArea"), 0.18f, null);
            ApplyImageStyle(FindComponentByName<Image>("m_rectPlayerArea"), 0.18f, null);
            ApplyImageStyle(FindComponentByName<Image>("m_rectLogArea"), 0.22f, null);
            ApplyImageStyle(_rectActorInfoLayer != null ? _rectActorInfoLayer.GetComponent<Image>() : null, 0f, false);
            ApplyImageStyle(FindComponentByName<Image>("m_rectBottomArea"), 0f, false);
            ApplyImageStyle(FindComponentByName<Image>("m_rectActionArea"), 0.12f, null);
            ApplyImageStyle(FindComponentByName<Image>("m_rectHandArea"), 0.08f, null);
            ApplyImageStyle(FindComponentByName<Image>("m_rectRoundArea"), 0.2f, null);
            ApplyImageStyle(FindComponentByName<Image>("m_rectPlayedCardsArea"), 0.1f, null);
            ApplyImageStyle(_imgPlayPreview, 0.32f, false);

            if (_rectPlayPreview != null)
            {
                _rectPlayPreview.gameObject.SetActive(false);
            }

            if (_txtPlayPreviewHint != null)
            {
                _txtPlayPreviewHint.alignment = TextAnchor.MiddleCenter;
                _txtPlayPreviewHint.text = "左键选牌组合，再点击出牌按钮";
            }

            if (_previewCardTemplateButton != null)
            {
                _previewCardTemplateButton.gameObject.SetActive(false);
            }

            if (_rectActorInfoLayer != null)
            {
                if (_txtEnemyInfo != null)
                {
                    _txtEnemyInfo.gameObject.SetActive(true);
                }

                if (_txtOtherPlayers != null)
                {
                    _txtOtherPlayers.gameObject.SetActive(true);
                }
            }

            EnsurePlayerHeadLabels();

            if (_rectEnemyAreaRoot != null)
            {
                _rectEnemyAreaRoot.gameObject.SetActive(false);
            }

            if (_rectInfoAreaRoot != null)
            {
                _rectInfoAreaRoot.gameObject.SetActive(false);
            }

            if (_txtDiscardPile != null)
            {
                _txtDiscardPile.raycastTarget = false;
            }

            if (_txtDrawPile != null)
            {
                _txtDrawPile.raycastTarget = false;
            }
        }

        private static void ApplyImageStyle(Image image, float alpha, bool? raycastTarget)
        {
            if (image == null)
            {
                return;
            }

            Color color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;

            if (raycastTarget.HasValue)
            {
                image.raycastTarget = raycastTarget.Value;
            }
        }

        private void EnsurePlayerHeadLabels()
        {
            if (_playerHeadLabels.Count > 0 || _playerHeadTemplate == null)
            {
                return;
            }

            if (_rectActorInfoLayer == null)
            {
                return;
            }

            _playerHeadTemplate.SetActive(false);
            for (int i = 0; i < 4; i++)
            {
                GameObject go = UnityEngine.Object.Instantiate(_playerHeadTemplate, _rectActorInfoLayer, false);
                go.name = $"m_goPlayerHead_{i}";
                go.SetActive(false);

                PlayerHeadLabelView view = new PlayerHeadLabelView
                {
                    Root = go,
                    Rect = go.GetComponent<RectTransform>(),
                    Background = go.GetComponent<Image>(),
                    Avatar = go.transform.Find("m_imgPlayerAvatar")?.GetComponent<Image>(),
                    Name = go.transform.Find("m_txtPlayerSlotName")?.GetComponent<Text>(),
                    Phase = go.transform.Find("m_txtPlayerSlotPhase")?.GetComponent<Text>(),
                    Hand = go.transform.Find("m_txtPlayerSlotHand")?.GetComponent<Text>(),
                };

                if (view.Avatar != null) view.Avatar.gameObject.SetActive(false);
                if (view.Phase != null) view.Phase.gameObject.SetActive(false);
                if (view.Background != null) view.Background.color = new Color(0.05f, 0.08f, 0.12f, 0.66f);

                _playerHeadLabels.Add(view);
            }
        }

        protected override void OnRefresh() => RefreshBattleView();
        private void OnBattleSnapshotUpdated(RegicideStateSnapshotPayload _) => RefreshBattleView();
        private void OnPublicStateSnapshotUpdated(RegicidePublicStateSnapshotPayload _) => RefreshBattleView();

        private void OnActionBroadcastReceived(RegicideActionBroadcastPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(payload.Summary) && !string.Equals(payload.ActorPlayerId, GameModule.RegicideBattle.LocalPlayerId))
            {
                _feedback = payload.Summary;
            }

            bool queued = TryQueueActionFxPayload(payload);
            if (queued && payload.ActionType == RegicideActionBroadcastType.PlayCard && payload.EnemyDefeated)
            {
                _enemyTransitionLocked = true;
            }

            if (queued)
            {
                PlayActionFxQueueAsync().Forget();
            }

            RefreshBattleView();
        }

        private void OnBattleError(RegicideErrorPayload payload)
        {
            if (payload != null && !string.IsNullOrEmpty(payload.Message))
            {
                _feedback = $"操作失败：{payload.Message}";
            }
            RefreshBattleView();
        }

        private void OnPlayClicked() => PlayCardAsync().Forget();
        private void OnPassClicked() => PassAsync().Forget();

        private void OnDefendClicked()
        {
            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (actions.IsAwaitingDiscard)
            {
                ConfirmDiscardAsync().Forget();
                return;
            }

            if (actions.IsCurrentSelectionJester && actions.SelectableNextPlayerIndices.Count > 1)
            {
                CycleJesterNextPlayer();
                return;
            }

            ClearSelection();
        }

        private void OnPreviewConfirmClicked()
        {
            if (_actionProcessing || !GameModule.RegicideBattle.IsMyTurn || _selectedCardIndices.Count <= 0)
            {
                return;
            }

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (actions == null)
            {
                return;
            }

            if (actions.IsAwaitingDiscard)
            {
                ConfirmDiscardAsync().Forget();
                return;
            }

            PlayCardAsync().Forget();
        }

        internal void OnPreviewCardRightClicked(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _selectedCardIndices.Count)
            {
                return;
            }

            int handCardIndex = _selectedCardIndices[slotIndex];
            _selectedCardIndices.RemoveAt(slotIndex);
            _feedback = $"已将 {handCardIndex + 1} 号待定牌退回手牌区。";
            RefreshBattleView();
        }

        private void OnLogToggleClicked()
        {
            SetLogPanelVisible(!_logPanelVisible);
        }

        private void SetLogPanelVisible(bool visible)
        {
            _logPanelVisible = visible;

            if (_rectInfoAreaRoot != null)
            {
                _rectInfoAreaRoot.gameObject.SetActive(visible);
                if (visible)
                {
                    _rectInfoAreaRoot.SetAsLastSibling();
                }
            }

            if (_rectLogAreaRoot != null)
            {
                _rectLogAreaRoot.gameObject.SetActive(visible);
            }

            if (_rectPlayerAreaRoot != null)
            {
                _rectPlayerAreaRoot.gameObject.SetActive(false);
            }

            if (visible)
            {
                AutoScrollLogToBottomAsync().Forget();
            }
        }

        internal void OnCardPointerEnter(int cardIndex)
        {
            if (_isDraggingCard || _actionProcessing || !GameModule.RegicideBattle.IsMyTurn)
            {
                return;
            }

            _hoverCardIndex = cardIndex;
            RefreshBattleView();
        }

        internal void OnCardPointerExit(int cardIndex)
        {
            if (_isDraggingCard)
            {
                return;
            }

            if (_hoverCardIndex == cardIndex)
            {
                _hoverCardIndex = -1;
                RefreshBattleView();
            }
        }

        internal void OnCardBeginDrag(int cardIndex, Vector2 screenPosition, Camera eventCamera)
        {
            if (!CanStartCardDrag(cardIndex))
            {
                return;
            }

            CardView view = GetCardView(cardIndex);
            if (view == null || view.Rect == null || _rectHandArea == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectHandArea, screenPosition, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            _isDraggingCard = true;
            _draggingCardIndex = cardIndex;
            _hoverCardIndex = -1;
            _dragPointerInPreview = false;
            _dragPointerOffset = view.Rect.anchoredPosition - localPoint;
            view.IsDragging = true;
            view.Rect.SetAsLastSibling();
            if (view.CanvasGroup != null)
            {
                view.CanvasGroup.blocksRaycasts = false;
            }

            AnimateCardScale(view, 1.12f, 0.1f);
            UpdatePlayPreviewVisual(true, false, false, "拖拽到待定区后松开");
        }

        internal void OnCardDrag(int cardIndex, Vector2 screenPosition, Camera eventCamera)
        {
            if (!_isDraggingCard || _draggingCardIndex != cardIndex)
            {
                return;
            }

            CardView view = GetCardView(cardIndex);
            if (view == null || view.Rect == null || _rectHandArea == null)
            {
                return;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectHandArea, screenPosition, eventCamera, out Vector2 localPoint))
            {
                view.Rect.anchoredPosition = localPoint + _dragPointerOffset;
            }

            bool pointerInPreview = IsPointerInPlayPreview(screenPosition, eventCamera);
            if (pointerInPreview != _dragPointerInPreview)
            {
                _dragPointerInPreview = pointerInPreview;
                RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
                bool canSubmit = actions != null && actions.IsCurrentSelectionPlayable;
                string hint = pointerInPreview
                    ? (canSubmit ? "松开后加入待定区，可直接确认" : "松开后加入待定区")
                    : "拖拽到待定区后松开";
                UpdatePlayPreviewVisual(true, pointerInPreview, canSubmit, hint);
            }
        }

        internal void OnCardEndDrag(int cardIndex, Vector2 screenPosition, Camera eventCamera)
        {
            if (!_isDraggingCard || _draggingCardIndex != cardIndex)
            {
                return;
            }

            CardView view = GetCardView(cardIndex);
            if (view != null)
            {
                view.IsDragging = false;
                if (view.CanvasGroup != null)
                {
                    view.CanvasGroup.blocksRaycasts = true;
                }
            }

            bool pointerInPreview = IsPointerInPlayPreview(screenPosition, eventCamera);
            ResetDraggingCardState(false);

            if (pointerInPreview)
            {
                HandleDropInPlayPreview(cardIndex);
            }
            else
            {
                _feedback = "已取消拖拽出牌。";
                RefreshBattleView();
            }
        }

        private bool CanStartCardDrag(int cardIndex)
        {
            if (_actionProcessing || !GameModule.RegicideBattle.IsMyTurn)
            {
                return false;
            }

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (actions == null)
            {
                return false;
            }

            if (actions.IsAwaitingDiscard)
            {
                return true;
            }

            return actions.PlayableCardIndices.Contains(cardIndex);
        }

        private CardView GetCardView(int cardIndex)
        {
            if (cardIndex < 0 || cardIndex >= _cardViews.Count)
            {
                return null;
            }

            return _cardViews[cardIndex];
        }

        private bool IsPointerInPlayPreview(Vector2 screenPosition, Camera eventCamera)
        {
            return _rectPlayPreview != null && _rectPlayPreview.gameObject.activeInHierarchy &&
                   RectTransformUtility.RectangleContainsScreenPoint(_rectPlayPreview, screenPosition, eventCamera);
        }

        private void HandleDropInPlayPreview(int cardIndex)
        {
            if (!_selectedCardIndices.Contains(cardIndex))
            {
                _selectedCardIndices.Add(cardIndex);
                _selectedCardIndices.Sort();
            }

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (actions == null)
            {
                RefreshBattleView();
                return;
            }

            if (actions.IsAwaitingDiscard)
            {
                _feedback = actions.IsCurrentSelectionPlayable
                    ? "已加入待弃牌区，点击确认弃牌。"
                    : (string.IsNullOrEmpty(actions.Message) ? "当前弃牌点数不足，可继续补牌。" : actions.Message);
                RefreshBattleView();
                return;
            }

            if (actions.IsCurrentSelectionPlayable)
            {
                _feedback = "已加入待出牌区，点击确认出牌。";
                RefreshBattleView();
                return;
            }

            _feedback = string.IsNullOrEmpty(actions.Message) ? "已加入待出牌区，可继续组合。" : actions.Message;
            RefreshBattleView();
        }

        private void UpdatePlayPreviewVisual(bool isDragging, bool pointerInPreview, bool canSubmit, string hint)
        {
            if (_imgPlayPreview != null)
            {
                Color color;
                if (isDragging && pointerInPreview)
                {
                    color = canSubmit ? new Color(0.18f, 0.46f, 0.22f, 0.5f) : new Color(0.45f, 0.28f, 0.16f, 0.46f);
                }
                else if (isDragging)
                {
                    color = new Color(0.14f, 0.26f, 0.38f, 0.38f);
                }
                else
                {
                    color = new Color(0.14f, 0.22f, 0.30f, 0.32f);
                }

                _imgPlayPreview.color = color;
            }

            if (_txtPlayPreviewHint != null)
            {
                _txtPlayPreviewHint.text = hint;
            }
        }

        private void RefreshPlayPreviewState(RegicidePlayerState player, RegicideAvailableActionSnapshot actions)
        {
            if (_rectPlayPreview != null)
            {
                _rectPlayPreview.gameObject.SetActive(false);
            }

            if (_btnPreviewConfirm != null)
            {
                _btnPreviewConfirm.gameObject.SetActive(false);
                _btnPreviewConfirm.interactable = false;
            }

            if (_txtPreviewConfirmLabel != null)
            {
                _txtPreviewConfirmLabel.text = "确认出牌";
            }
        }

        private void RenderPreviewCards(RegicidePlayerState player, RegicideAvailableActionSnapshot actions)
        {
            if (_rectPreviewCardsRoot == null || _previewCardTemplateButton == null)
            {
                return;
            }

            int handCount = player?.Hand?.Count ?? 0;
            for (int i = _selectedCardIndices.Count - 1; i >= 0; i--)
            {
                int selected = _selectedCardIndices[i];
                if (selected < 0 || selected >= handCount)
                {
                    _selectedCardIndices.RemoveAt(i);
                }
            }

            int count = _selectedCardIndices.Count;
            EnsurePreviewCardViewCount(count);
            if (_previewCardViews.Count < count)
            {
                count = _previewCardViews.Count;
            }

            if (count <= 0)
            {
                return;
            }

            const float cardWidth = 132f;
            const float spacing = 12f;
            float totalWidth = cardWidth * count + spacing * (count - 1);
            float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                PreviewCardView view = _previewCardViews[i];
                if (view == null || view.Rect == null || view.Root == null)
                {
                    continue;
                }

                int handIndex = _selectedCardIndices[i];
                RegicideCard card = player != null && player.Hand != null && handIndex >= 0 && handIndex < player.Hand.Count
                    ? player.Hand[handIndex]
                    : null;
                bool playable = actions != null && actions.IsCurrentSelectionPlayable;

                view.Root.SetActive(true);
                view.SlotIndex = i;
                view.HandCardIndex = handIndex;
                SetRect(view.Rect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (cardWidth + spacing), 0f),
                    new Vector2(cardWidth, 74f));

                if (view.InteractProxy != null)
                {
                    view.InteractProxy.Bind(this, i);
                }

                if (view.Label != null)
                {
                    view.Label.text = card == null ? $"牌#{handIndex + 1}" : GetCardDisplay(card);
                }

                if (view.Background != null)
                {
                    view.Background.color = playable
                        ? new Color(0.21f, 0.38f, 0.24f, 0.95f)
                        : new Color(0.22f, 0.27f, 0.35f, 0.95f);
                }

                if (view.Button != null)
                {
                    view.Button.interactable = true;
                }
            }
        }

        private void EnsurePreviewCardViewCount(int count)
        {
            if (_rectPreviewCardsRoot == null || _previewCardTemplateButton == null)
            {
                return;
            }

            while (_previewCardViews.Count < count)
            {
                int slot = _previewCardViews.Count;
                GameObject go = UnityEngine.Object.Instantiate(_previewCardTemplateButton.gameObject, _rectPreviewCardsRoot, false);
                go.name = $"m_btnPreviewCard_{slot}";
                go.SetActive(true);

                PreviewCardView view = new PreviewCardView
                {
                    Root = go,
                    Rect = go.GetComponent<RectTransform>(),
                    Button = go.GetComponent<Button>(),
                    Background = go.GetComponent<Image>(),
                    Label = go.transform.Find("m_txtPreviewCardLabel")?.GetComponent<Text>() ?? go.transform.Find("m_txtLabel")?.GetComponent<Text>(),
                    InteractProxy = go.GetComponent<RegicidePreviewCardInteractProxy>() ?? go.AddComponent<RegicidePreviewCardInteractProxy>(),
                    SlotIndex = slot,
                    HandCardIndex = -1,
                };

                if (view.InteractProxy != null)
                {
                    view.InteractProxy.Bind(this, slot);
                }

                _previewCardViews.Add(view);
            }

            for (int i = 0; i < _previewCardViews.Count; i++)
            {
                bool active = i < count;
                PreviewCardView view = _previewCardViews[i];
                if (view == null || view.Root == null)
                {
                    continue;
                }

                if (view.InteractProxy != null)
                {
                    view.InteractProxy.Bind(this, i);
                }

                view.Root.SetActive(active);
            }
        }

        private void ResetDraggingCardState(bool resetPosition)
        {
            if (_draggingCardIndex >= 0 && _draggingCardIndex < _cardViews.Count)
            {
                CardView view = _cardViews[_draggingCardIndex];
                if (view != null)
                {
                    view.IsDragging = false;
                    if (view.CanvasGroup != null)
                    {
                        view.CanvasGroup.blocksRaycasts = true;
                    }

                    if (resetPosition && view.Rect != null)
                    {
                        view.Rect.anchoredPosition = view.LayoutAnchoredPos;
                        view.Rect.localEulerAngles = new Vector3(0f, 0f, view.LayoutAngle);
                    }
                }
            }

            _isDraggingCard = false;
            _draggingCardIndex = -1;
            _dragPointerInPreview = false;
        }

        private void OnHelpClicked()
        {
            if (_helpMask == null) return;
            if (_txtHelpContent != null) _txtHelpContent.text = BuildHelpContent();
            _helpMask.transform.SetAsLastSibling();
            _helpMask.SetActive(true);
        }

        private void OnHelpCloseClicked()
        {
            if (_helpMask != null) _helpMask.SetActive(false);
        }

        private async UniTaskVoid PlayCardAsync()
        {
            if (_actionProcessing) return;
            RegicideBattleState state = GameModule.RegicideBattle.State;
            RegicidePlayerState player = GameModule.RegicideBattle.GetLocalPlayerState();
            if (state == null || player == null) return;
            List<string> playedCards = BuildCardDisplayList(player, _selectedCardIndices);

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (!actions.IsCurrentSelectionPlayable)
            {
                _feedback = string.IsNullOrEmpty(actions.Message) ? "当前牌组组合无法出牌。" : actions.Message;
                RefreshBattleView();
                return;
            }

            _actionProcessing = true;
            RefreshActionButtons(false);
            bool ok = await GameModule.RegicideBattle.PlayCardAsync(_selectedCardIndices, _selectedNextPlayerIndex);
            _actionProcessing = false;

            if (!ok)
            {
                _feedback = "出牌失败，请查看提示。";
            }
            else
            {
                _selectedCardIndices.Clear();
                _selectedNextPlayerIndex = -1;
                if (state.Players != null && state.Players.Count <= 1)
                {
                    ShowPlayedCards(playedCards);
                    AnimatePlayerDashAsync(GameModule.RegicideBattle.LocalPlayerId).Forget();
                }
            }

            RefreshBattleView();
        }

        private async UniTaskVoid PassAsync()
        {
            if (_actionProcessing) return;
            RegicideBattleState state = GameModule.RegicideBattle.State;
            if (state == null) return;

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (!actions.CanPass)
            {
                _feedback = string.IsNullOrEmpty(actions.Message) ? "当前无法跳过。" : actions.Message;
                RefreshBattleView();
                return;
            }

            _actionProcessing = true;
            RefreshActionButtons(false);
            bool ok = await GameModule.RegicideBattle.PassAsync();
            _actionProcessing = false;

            if (!ok)
            {
                _feedback = "跳过失败，请查看提示。";
            }
            else
            {
                _selectedCardIndices.Clear();
                _selectedNextPlayerIndex = -1;
            }

            RefreshBattleView();
        }

        private async UniTaskVoid ConfirmDiscardAsync()
        {
            if (_actionProcessing) return;
            RegicideBattleState state = GameModule.RegicideBattle.State;
            if (state == null) return;
            RegicidePlayerState localPlayer = GameModule.RegicideBattle.GetLocalPlayerState();
            List<string> discardedCards = BuildCardDisplayList(localPlayer, _selectedCardIndices);

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (!actions.IsAwaitingDiscard) return;
            if (!actions.IsCurrentSelectionPlayable)
            {
                _feedback = string.IsNullOrEmpty(actions.Message) ? "当前选牌不足以承伤。" : actions.Message;
                RefreshBattleView();
                return;
            }

            _actionProcessing = true;
            RefreshActionButtons(false);
            bool ok = await GameModule.RegicideBattle.DiscardForDamageAsync(_selectedCardIndices);
            _actionProcessing = false;

            if (!ok)
            {
                _feedback = "弃牌承伤失败，请查看提示。";
            }
            else
            {
                _selectedCardIndices.Clear();
                if (state.Players != null && state.Players.Count <= 1)
                {
                    ShowPlayedCards(discardedCards);
                    AnimateBossCounterAsync().Forget();
                }
            }

            RefreshBattleView();
        }

        private void ClearSelection()
        {
            if (_actionProcessing) return;
            _selectedCardIndices.Clear();
            _selectedNextPlayerIndex = -1;
            RefreshBattleView();
        }

        private void CycleJesterNextPlayer()
        {
            if (_actionProcessing) return;
            RegicideBattleState state = GameModule.RegicideBattle.State;
            if (state == null) return;

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (!actions.IsCurrentSelectionJester || actions.SelectableNextPlayerIndices.Count <= 1)
            {
                _feedback = "当前无需切换接手玩家。";
                RefreshBattleView();
                return;
            }

            List<int> players = actions.SelectableNextPlayerIndices;
            int currentPos = players.IndexOf(_selectedNextPlayerIndex);
            if (currentPos < 0) currentPos = players.IndexOf(actions.SuggestedNextPlayerIndex);
            int nextPos = currentPos < 0 ? 0 : (currentPos + 1) % players.Count;
            _selectedNextPlayerIndex = players[nextPos];
            RefreshBattleView();
        }

private void RefreshBattleView()
        {
            RegicideBattleState state = GameModule.RegicideBattle.State;
            RegicidePlayerState player = GameModule.RegicideBattle.GetLocalPlayerState();
            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (_selectedNextPlayerIndex < 0 || !actions.SelectableNextPlayerIndices.Contains(_selectedNextPlayerIndex))
            {
                _selectedNextPlayerIndex = actions.SuggestedNextPlayerIndex;
            }

            if (_isDraggingCard && (_actionProcessing || !GameModule.RegicideBattle.IsMyTurn))
            {
                ResetDraggingCardState(true);
            }

            UpdateFeedbackFromState(state);
            if (!_enemyTransitionLocked)
            {
                GameModule.RegicideBattlePresentation.SyncBattleState(state, GameModule.RegicideBattle.PublicStateSnapshot, GameModule.RegicideBattle.LocalPlayerId);
                RenderEnemyPanel(state);
            }
            RenderPlayerPanel(state, player, actions);
            RenderOtherPlayersPanel(state);
            RefreshPlayPreviewState(player, actions);
            RenderPlayedCardsPanel();
            RenderCardPanel(player, actions);
            RenderPileInfo(state);
            RenderBattleLog(state);
            RefreshActionButtons(GameModule.RegicideBattle.IsMyTurn);

            if (state != null && state.IsGameOver && !_navigatingResult)
            {
                _navigatingResult = true;
                NavigateToResultAsync().Forget();
            }
        }

        private void RenderEnemyPanel(RegicideBattleState state)
        {
            if (_txtRoundInfo != null)
            {
                _txtRoundInfo.text = state == null ? "战斗准备中..." : $"第 {Mathf.Max(1, state.Round)} 回合";
            }

            if (_txtEnemyInfo == null)
            {
                return;
            }

            if (state == null || state.CurrentEnemy == null)
            {
                _txtEnemyInfo.text = "等待敌人信息...";
                return;
            }

            RegicideEnemyState enemy = state.CurrentEnemy;
            string immunity = state.IsEnemyImmunityDisabledByJester ? "无（小丑已禁用）" : GetSuitDisplay(enemy.Suit);
            _txtEnemyInfo.text =
                $"{enemy.Name}  {GetSuitDisplay(enemy.Suit)}\n" +
                $"生命 {Mathf.Max(0, enemy.Health)}    攻击 {Mathf.Max(0, enemy.Attack)}\n" +
                $"花色免疫：{immunity}";

            RectTransform enemyRect = _txtEnemyInfo.rectTransform;
            if (GameModule.RegicideBattlePresentation.TryGetEnemyHeadWorldPosition(out Vector3 worldPos) &&
                TryWorldToLayerWorldPoint(worldPos + new Vector3(0f, 0.1f, 0f), out Vector3 mappedWorld))
            {
                enemyRect.position = mappedWorld;
            }
            else
            {
                enemyRect.anchoredPosition = ClampAnchoredToLayer(enemyRect, enemyRect.anchoredPosition, UiSafePadding);
            }
        }

        private void RenderPlayerPanel(RegicideBattleState state, RegicidePlayerState player, RegicideAvailableActionSnapshot actions)
        {
            if (_txtPlayerInfo != null)
            {
                if (state == null || player == null)
                {
                    _txtPlayerInfo.text = "玩家信息未就绪。";
                }
                else
                {
                    _txtPlayerInfo.text =
                        $"{player.PlayerId}（你）\n" +
                        $"手牌：{player.Hand.Count}/{Mathf.Max(1, state.HandLimitPerPlayer)}";
                }
            }

            if (_txtActionHint != null)
            {
                if (state == null)
                {
                    _txtActionHint.text = "提示：等待状态同步。";
                }
                else if (!GameModule.RegicideBattle.IsMyTurn && !state.IsGameOver)
                {
                    string currentTurnPlayer = GetCurrentTurnPlayerDisplay(state);
                    _txtActionHint.text = string.IsNullOrEmpty(currentTurnPlayer) ? "等待其他玩家行动..." : $"等待 {currentTurnPlayer} 行动中...";
                }
                else if (actions != null && actions.IsAwaitingDiscard)
                {
                    _txtActionHint.text = $"提示：敌人反击 {actions.PendingDiscardRequiredValue}，请选择弃牌承伤。";
                }
                else
                {
                    bool hasSelection = _selectedCardIndices.Count > 0;
                    if (hasSelection)
                    {
                        string reason = actions != null && !string.IsNullOrEmpty(actions.Message)
                            ? actions.Message
                            : "当前组合不可出牌，请调整后再试。";
                        _txtActionHint.text = $"提示：{reason}";
                    }
                    else
                    {
                        _txtActionHint.text = "提示：请选择要出的牌，或点击“跳过回合”。";
                    }
                }
            }

            RenderSelectionPanel(state, player, actions);

            if (_txtDefendLabel != null)
            {
                if (actions != null && actions.IsAwaitingDiscard)
                {
                    _txtDefendLabel.text = actions.IsCurrentSelectionPlayable
                        ? $"确认弃牌({actions.CurrentSelectionAttackValue}/{actions.PendingDiscardRequiredValue})"
                        : $"弃牌不足({actions.CurrentSelectionAttackValue}/{actions.PendingDiscardRequiredValue})";
                }
                else if (actions != null && actions.IsCurrentSelectionJester && state != null)
                {
                    int nextIndex = _selectedNextPlayerIndex >= 0 ? _selectedNextPlayerIndex : actions.SuggestedNextPlayerIndex;
                    string nextPlayer = nextIndex >= 0 && nextIndex < state.Players.Count ? state.Players[nextIndex].PlayerId : "-";
                    _txtDefendLabel.text = $"切换接手({nextPlayer})";
                }
                else if (_selectedCardIndices.Count > 0)
                {
                    _txtDefendLabel.text = "清空选择";
                }
                else
                {
                    _txtDefendLabel.text = "辅助操作";
                }
            }

            if (_txtPlayLabel != null) _txtPlayLabel.text = actions != null && actions.IsAwaitingDiscard ? "出牌(承伤中)" : "出牌";
            if (_txtPassLabel != null) _txtPassLabel.text = actions != null && actions.IsAwaitingDiscard ? "跳过(承伤中)" : "跳过回合";
        }

        private void RenderSelectionPanel(RegicideBattleState state, RegicidePlayerState player, RegicideAvailableActionSnapshot actions)
        {
            if (_txtSelectedCard == null)
            {
                return;
            }

            if (actions != null && actions.IsAwaitingDiscard)
            {
                if (player == null || _selectedCardIndices.Count <= 0)
                {
                    _txtSelectedCard.text = $"承伤弃牌：未选择（需 {actions.PendingDiscardRequiredValue} 点）";
                    return;
                }

                StringBuilder discardBuilder = new StringBuilder(128);
                int picked = 0;
                for (int i = 0; i < _selectedCardIndices.Count; i++)
                {
                    int index = _selectedCardIndices[i];
                    if (index < 0 || index >= player.Hand.Count)
                    {
                        continue;
                    }

                    if (picked > 0) discardBuilder.Append(" + " );
                    discardBuilder.Append(GetCardDisplay(player.Hand[index]));
                    picked++;
                }

                _txtSelectedCard.text = picked <= 0
                    ? $"承伤弃牌：未选择（需 {actions.PendingDiscardRequiredValue} 点）"
                    : $"承伤弃牌：{discardBuilder}\n总点数：{actions.CurrentSelectionAttackValue}/{actions.PendingDiscardRequiredValue}";
                return;
            }

            if (player == null || _selectedCardIndices.Count <= 0)
            {
                _txtSelectedCard.text = "已选组合：无";
                return;
            }

            StringBuilder selectedBuilder = new StringBuilder(128);
            int cardCount = 0;
            for (int i = 0; i < _selectedCardIndices.Count; i++)
            {
                int index = _selectedCardIndices[i];
                if (index < 0 || index >= player.Hand.Count)
                {
                    continue;
                }

                if (cardCount > 0) selectedBuilder.Append(" + " );
                selectedBuilder.Append(GetCardDisplay(player.Hand[index]));
                cardCount++;
            }

            if (cardCount <= 0)
            {
                _txtSelectedCard.text = "已选组合：无";
                return;
            }

            _txtSelectedCard.text =
                $"已选组合：{selectedBuilder}\n" +
                $"攻击值：{actions.CurrentSelectionAttackValue}    可出牌：{(actions.IsCurrentSelectionPlayable ? "是" : "否")}";

            if (actions.IsCurrentSelectionJester && state != null && actions.SelectableNextPlayerIndices.Count > 0)
            {
                int nextIndex = _selectedNextPlayerIndex >= 0 ? _selectedNextPlayerIndex : actions.SuggestedNextPlayerIndex;
                if (nextIndex < 0 || nextIndex >= state.Players.Count)
                {
                    nextIndex = actions.SuggestedNextPlayerIndex;
                }

                string nextPlayer = nextIndex >= 0 && nextIndex < state.Players.Count ? state.Players[nextIndex].PlayerId : "-";
                _txtSelectedCard.text += $"\n小丑接手玩家：{nextPlayer}";
            }
        }

        private void RenderOtherPlayersPanel(RegicideBattleState state)
        {
            UpdatePublicPlayerLookup();
            RenderPlayerHeadLabels(state);
            RenderTurnOrder(state);
        }

        private void RenderPileInfo(RegicideBattleState state)
        {
            int discardCount = state?.DiscardPile?.Count ?? 0;
            int drawCount = state?.DrawPile?.Count ?? 0;

            if (_txtDiscardPile != null)
            {
                _txtDiscardPile.text = $"弃牌堆 {Mathf.Max(0, discardCount)} 张";
            }

            if (_txtDrawPile != null)
            {
                _txtDrawPile.text = $"牌堆 {Mathf.Max(0, drawCount)} 张";
            }
        }

        private void UpdatePublicPlayerLookup()
        {
            _publicPlayerLookup.Clear();
            RegicidePublicStateSnapshotPayload snapshot = GameModule.RegicideBattle.PublicStateSnapshot;
            if (snapshot?.Players == null)
            {
                return;
            }

            for (int i = 0; i < snapshot.Players.Count; i++)
            {
                RegicidePublicPlayerState player = snapshot.Players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    continue;
                }

                if (!_publicPlayerLookup.ContainsKey(player.PlayerId))
                {
                    _publicPlayerLookup.Add(player.PlayerId, player);
                }
            }
        }

        private void RenderPlayerHeadLabels(RegicideBattleState state)
        {
            EnsurePlayerHeadLabels();
            if (_playerHeadLabels.Count <= 0)
            {
                return;
            }

            for (int i = 0; i < _playerHeadLabels.Count; i++)
            {
                if (_playerHeadLabels[i]?.Root != null)
                {
                    _playerHeadLabels[i].Root.SetActive(false);
                }
            }

            if (state?.Players == null || state.Players.Count <= 0)
            {
                return;
            }

            int displayCount = Mathf.Min(_playerHeadLabels.Count, state.Players.Count);
            for (int i = 0; i < displayCount; i++)
            {
                RegicidePlayerState player = state.Players[i];
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    continue;
                }

                PlayerHeadLabelView view = _playerHeadLabels[i];
                if (view?.Root == null || view.Rect == null)
                {
                    continue;
                }

                view.Root.SetActive(true);
                view.PlayerId = player.PlayerId;
                if (GameModule.RegicideBattlePresentation.TryGetPlayerHeadWorldPosition(player.PlayerId, out Vector3 worldPos) &&
                    TryWorldToLayerWorldPoint(worldPos, out Vector3 mappedWorld))
                {
                    view.Rect.position = mappedWorld;
                }
                else
                {
                    view.Rect.anchoredPosition = ClampAnchoredToLayer(view.Rect, view.Rect.anchoredPosition, UiSafePadding);
                }

                bool isCurrent = i == state.CurrentPlayerIndex;
                int handCount = TryGetHandCount(player.PlayerId, player.Hand != null ? player.Hand.Count : 0);
                if (view.Name != null)
                {
                    view.Name.text = isCurrent ? $"> {player.PlayerId}" : player.PlayerId;
                }

                if (view.Hand != null)
                {
                    view.Hand.text = $"手牌 x{Mathf.Max(0, handCount)}";
                }

                if (view.Background != null)
                {
                    view.Background.color = isCurrent
                        ? new Color(0.22f, 0.33f, 0.2f, 0.78f)
                        : new Color(0.05f, 0.08f, 0.12f, 0.66f);
                }
            }
        }

        private void RenderTurnOrder(RegicideBattleState state)
        {
            if (_txtOtherPlayers == null)
            {
                return;
            }

            RectTransform turnOrderRect = _txtOtherPlayers.rectTransform;
            turnOrderRect.anchoredPosition = ClampAnchoredToLayer(turnOrderRect, turnOrderRect.anchoredPosition, UiSafePadding);

            if (state?.Players == null || state.Players.Count <= 0)
            {
                _txtOtherPlayers.text = "行动顺序：等待玩家...";
                return;
            }

            int playerCount = state.Players.Count;
            int current = Mathf.Clamp(state.CurrentPlayerIndex, 0, Mathf.Max(0, playerCount - 1));

            StringBuilder builder = new StringBuilder(256);
            builder.AppendLine("行动顺序");
            for (int step = 0; step < playerCount; step++)
            {
                int index = (current + step) % playerCount;
                RegicidePlayerState player = state.Players[index];
                if (player == null || string.IsNullOrEmpty(player.PlayerId))
                {
                    continue;
                }

                int handCount = TryGetHandCount(player.PlayerId, player.Hand != null ? player.Hand.Count : 0);
                builder.Append(step == 0 ? ">" : "  ");
                builder.Append(player.PlayerId).Append("  手牌x").Append(Mathf.Max(0, handCount));
                if (state.IsAwaitingDiscard && index == state.PendingDiscardTargetPlayerIndex)
                {
                    builder.Append("  (承伤)");
                }

                builder.Append('\n');
            }

            _txtOtherPlayers.text = builder.ToString();
        }

        private int TryGetHandCount(string playerId, int fallback)
        {
            if (!string.IsNullOrEmpty(playerId) && _publicPlayerLookup.TryGetValue(playerId, out RegicidePublicPlayerState publicPlayer))
            {
                return Mathf.Max(0, publicPlayer.HandCount);
            }

            return Mathf.Max(0, fallback);
        }

        private bool TryWorldToCanvasPoint(Vector3 worldPos, out Vector2 anchored)
        {
            anchored = Vector2.zero;
            RectTransform target = _rectActorInfoLayer != null ? _rectActorInfoLayer : _rectRootCanvas;
            if (target == null)
            {
                return false;
            }

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null)
            {
                return false;
            }

            Vector3 viewport = worldCamera.WorldToViewportPoint(worldPos);
            if (viewport.z <= 0f)
            {
                return false;
            }

            Rect pixelRect = worldCamera.pixelRect;
            Vector3 screenPoint3 = new Vector3(
                pixelRect.x + viewport.x * pixelRect.width,
                pixelRect.y + viewport.y * pixelRect.height,
                viewport.z);
            if (screenPoint3.z <= 0f)
            {
                return false;
            }

            Vector2 screenPoint = new Vector2(screenPoint3.x, screenPoint3.y);
            Camera uiEventCamera = ResolveUiEventCamera(target);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screenPoint, uiEventCamera, out anchored);
        }

        private bool TryWorldToLayerWorldPoint(Vector3 worldPos, out Vector3 mappedWorld)
        {
            mappedWorld = Vector3.zero;
            RectTransform target = _rectActorInfoLayer != null ? _rectActorInfoLayer : _rectRootCanvas;
            if (target == null)
            {
                return false;
            }

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null)
            {
                return false;
            }

            Vector3 viewport = worldCamera.WorldToViewportPoint(worldPos);
            if (viewport.z <= 0f)
            {
                return false;
            }

            Rect pixelRect = worldCamera.pixelRect;
            Vector2 screenPoint = new Vector2(
                pixelRect.x + viewport.x * pixelRect.width,
                pixelRect.y + viewport.y * pixelRect.height);
            Camera uiEventCamera = ResolveUiEventCamera(target);
            return RectTransformUtility.ScreenPointToWorldPointInRectangle(target, screenPoint, uiEventCamera, out mappedWorld);
        }

        private Vector2 ClampAnchoredToLayer(RectTransform uiRect, Vector2 anchored, float padding)
        {
            if (uiRect == null)
            {
                return anchored;
            }

            RectTransform target = _rectActorInfoLayer != null ? _rectActorInfoLayer : _rectRootCanvas;
            if (target == null)
            {
                return anchored;
            }

            Rect rect = target.rect;
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return anchored;
            }

            Vector2 size = uiRect.sizeDelta;
            Vector2 pivot = uiRect.pivot;
            float halfWidth = rect.width * 0.5f;
            float halfHeight = rect.height * 0.5f;
            float minX = -halfWidth + size.x * pivot.x + padding;
            float maxX = halfWidth - size.x * (1f - pivot.x) - padding;
            float minY = -halfHeight + size.y * pivot.y + padding;
            float maxY = halfHeight - size.y * (1f - pivot.y) - padding;

            if (minX > maxX)
            {
                float middle = (minX + maxX) * 0.5f;
                minX = middle;
                maxX = middle;
            }

            if (minY > maxY)
            {
                float middle = (minY + maxY) * 0.5f;
                minY = middle;
                maxY = middle;
            }

            anchored.x = Mathf.Clamp(anchored.x, minX, maxX);
            anchored.y = Mathf.Clamp(anchored.y, minY, maxY);
            return anchored;
        }

        private Camera ResolveUiEventCamera(RectTransform target = null)
        {
            RectTransform pivot = target != null ? target : _rectRootCanvas;
            if (pivot == null)
            {
                return null;
            }

            Canvas canvas = pivot.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return null;
            }

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (canvas.worldCamera != null)
            {
                return canvas.worldCamera;
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                return ResolveWorldCamera();
            }

            return ResolveWorldCamera();
        }

        private Camera ResolveWorldCamera()
        {
            if (GameModule.RegicideBattlePresentation.TryGetBattleCamera(out Camera battleCamera) &&
                battleCamera != null &&
                battleCamera.enabled &&
                battleCamera.gameObject.activeInHierarchy)
            {
                return battleCamera;
            }

            if (Camera.main != null)
            {
                return Camera.main;
            }

            Camera[] all = Camera.allCameras;
            if (all == null || all.Length <= 0)
            {
                return null;
            }

            for (int i = 0; i < all.Length; i++)
            {
                Camera camera = all[i];
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return camera;
                }
            }

            return all[0];
        }

private void RenderPlayedCardsPanel()
        {
            int count = _visiblePlayedCards.Count;
            EnsurePlayedCardViewCount(count);
            if (_playedCardViews.Count < count)
            {
                count = _playedCardViews.Count;
            }

            if (count <= 0)
            {
                return;
            }

            float cardWidth = 156f;
            float spacing = 24f;
            float totalWidth = cardWidth * count + spacing * (count - 1);
            float startX = -totalWidth * 0.5f + cardWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                PlayedCardView view = _playedCardViews[i];
                if (view == null || view.Root == null || view.Rect == null) continue;

                string cardText = _visiblePlayedCards[i];
                view.Root.SetActive(true);
                SetRect(view.Rect,
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f),
                    new Vector2(startX + i * (cardWidth + spacing), 0f),
                    new Vector2(156f, 210f));

                if (view.Title != null)
                {
                    view.Title.text = cardText;
                }

                if (view.Desc != null)
                {
                    view.Desc.text = "已打出";
                }

                if (view.Background != null)
                {
                    view.Background.color = new Color(0.2f + 0.06f * (i % 3), 0.24f, 0.32f, 0.94f);
                }
            }
        }

        private void EnsurePlayedCardViewCount(int count)
        {
            if (_rectPlayedCardsArea == null || _playedCardTemplate == null)
            {
                if (!_playedCardTemplateMissingLogged)
                {
                    Debug.LogWarning("RegicideBattleUI: 明牌模板未绑定，已跳过明牌展示。");
                    _playedCardTemplateMissingLogged = true;
                }
                return;
            }

            _playedCardTemplateMissingLogged = false;
            while (_playedCardViews.Count < count)
            {
                int slot = _playedCardViews.Count;
                GameObject cardGo = UnityEngine.Object.Instantiate(_playedCardTemplate, _rectPlayedCardsArea, false);
                cardGo.name = $"m_goPlayedCard_{slot}";
                cardGo.SetActive(true);

                PlayedCardView view = new PlayedCardView
                {
                    Root = cardGo,
                    Rect = cardGo.GetComponent<RectTransform>(),
                    Background = cardGo.GetComponent<Image>(),
                    Title = cardGo.transform.Find("m_txtPlayedCardTitle")?.GetComponent<Text>(),
                    Desc = cardGo.transform.Find("m_txtPlayedCardDesc")?.GetComponent<Text>(),
                };

                _playedCardViews.Add(view);
            }

            for (int i = 0; i < _playedCardViews.Count; i++)
            {
                if (_playedCardViews[i].Root != null)
                {
                    _playedCardViews[i].Root.SetActive(i < count);
                }
            }
        }

        private void ShowPlayedCards(IList<string> cards)
        {
            _visiblePlayedCards.Clear();
            if (cards != null)
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (!string.IsNullOrEmpty(cards[i]))
                    {
                        _visiblePlayedCards.Add(cards[i]);
                    }
                }
            }

            _playedCardsVersion++;
            RenderPlayedCardsPanel();
            HidePlayedCardsLaterAsync(_playedCardsVersion).Forget();
        }

        private async UniTaskVoid HidePlayedCardsLaterAsync(int version)
        {
            await UniTask.Delay(1400);
            if (version != _playedCardsVersion)
            {
                return;
            }

            _visiblePlayedCards.Clear();
            RenderPlayedCardsPanel();
        }

        private static List<string> BuildCardDisplayList(RegicidePlayerState player, IList<int> cardIndices)
        {
            List<string> result = new List<string>();
            if (player?.Hand == null || cardIndices == null || cardIndices.Count <= 0)
            {
                return result;
            }

            for (int i = 0; i < cardIndices.Count; i++)
            {
                int index = cardIndices[i];
                if (index < 0 || index >= player.Hand.Count)
                {
                    continue;
                }

                result.Add(GetCardDisplay(player.Hand[index]));
            }

            return result;
        }

        private string GetCurrentTurnPlayerDisplay(RegicideBattleState state)
        {
            RegicidePublicStateSnapshotPayload snapshot = GameModule.RegicideBattle.PublicStateSnapshot;
            if (snapshot != null && snapshot.Players != null)
            {
                for (int i = 0; i < snapshot.Players.Count; i++)
                {
                    RegicidePublicPlayerState player = snapshot.Players[i];
                    if (player != null && player.IsCurrentTurn)
                    {
                        return player.PlayerId;
                    }
                }
            }

            if (state != null && state.CurrentPlayerIndex >= 0 && state.CurrentPlayerIndex < state.Players.Count)
            {
                return state.Players[state.CurrentPlayerIndex]?.PlayerId ?? string.Empty;
            }

            return string.Empty;
        }

        private void RenderCardPanel(RegicidePlayerState player, RegicideAvailableActionSnapshot actions)
        {
            int count = player != null && player.Hand != null ? player.Hand.Count : 0;
            EnsureCardViewCount(count);
            if (_cardViews.Count < count) count = _cardViews.Count;

            if (count <= 0)
            {
                _selectedCardIndices.Clear();
                return;
            }

            for (int i = _selectedCardIndices.Count - 1; i >= 0; i--)
            {
                int selectedIndex = _selectedCardIndices[i];
                if (selectedIndex < 0 || selectedIndex >= count)
                {
                    _selectedCardIndices.RemoveAt(i);
                }
            }

            if (_selectedCardIndices.Count <= 0 && actions != null && actions.SelectedCardIndices.Count > 0)
            {
                _selectedCardIndices.AddRange(actions.SelectedCardIndices);
            }

            float rowWidth = _rectHandArea != null ? _rectHandArea.rect.width - 140f : 1000f;
            float maxSpread = Mathf.Max(230f, Mathf.Min(rowWidth * 0.5f, 620f));
            float spread = count <= 1 ? 0f : maxSpread;
            float angleRange = Mathf.Lerp(7f, 18f, Mathf.Clamp01((count - 1) / 7f));
            float baseY = 8f;
            float lift = Mathf.Lerp(9f, 28f, Mathf.Clamp01(count / 10f));

            for (int i = 0; i < count; i++)
            {
                CardView view = _cardViews[i];
                RegicideCard card = player.Hand[i];
                bool playable = actions != null && actions.PlayableCardIndices.Contains(i);
                bool canSelect = actions != null && actions.IsAwaitingDiscard ? true : playable;
                bool selected = _selectedCardIndices.Contains(i);
                bool hovered = !_isDraggingCard && _hoverCardIndex == i && !_actionProcessing && GameModule.RegicideBattle.IsMyTurn;
                float t = count <= 1 ? 0f : i / (count - 1f);
                float centered = t - 0.5f;
                float x = spread * centered;
                float arc = 1f - Mathf.Abs(centered) * 2f;
                float y = baseY + arc * lift + (selected ? 34f : 0f) + (hovered ? 22f : 0f);
                float angle = -centered * 2f * angleRange;
                float targetScale = view.IsDragging ? 1.12f : (selected ? 1.08f : (hovered ? 1.05f : 1f));
                Vector2 targetPos = new Vector2(x, y);
                float targetAngle = hovered ? 0f : angle;

                view.CardIndex = i;
                view.Root.SetActive(true);
                Vector2 previousPos = view.LayoutAnchoredPos;
                float previousAngle = view.LayoutAngle;
                view.LayoutAnchoredPos = targetPos;
                view.LayoutAngle = targetAngle;
                if (!view.IsDragging)
                {
                    if (view.Rect != null)
                    {
                        view.Rect.anchorMin = new Vector2(0.5f, 0f);
                        view.Rect.anchorMax = new Vector2(0.5f, 0f);
                        view.Rect.pivot = new Vector2(0.5f, 0f);
                        view.Rect.sizeDelta = new Vector2(CardWidth, CardHeight);
                    }

                    if (!view.LayoutInitialized)
                    {
                        if (view.Rect != null)
                        {
                            view.Rect.anchoredPosition = targetPos;
                            view.Rect.localEulerAngles = new Vector3(0f, 0f, targetAngle);
                        }

                        view.LayoutInitialized = true;
                    }
                    else if (Vector2.SqrMagnitude(previousPos - targetPos) > 0.04f || Mathf.Abs(Mathf.DeltaAngle(previousAngle, targetAngle)) > 0.1f)
                    {
                        AnimateCardLayout(view, targetPos, targetAngle);
                    }
                }
                AnimateCardScale(view, targetScale, 0.12f);
                if (view.Button != null) view.Button.interactable = canSelect && !_actionProcessing && GameModule.RegicideBattle.IsMyTurn;
                if (view.Background != null) view.Background.color = BuildCardColor(card.Suit, selected, canSelect);
                if (view.Title != null) view.Title.text = $"{GetCardDisplay(card)}\n点数 {card.AttackValue}";
                if (view.Desc != null) view.Desc.text = DescribeCardEffect(card);
                if (view.Rect != null && (selected || hovered || view.IsDragging))
                {
                    view.Rect.SetAsLastSibling();
                }
            }
        }

        private void AnimateCardLayout(CardView view, Vector2 targetPos, float targetAngle)
        {
            if (view == null || view.Rect == null)
            {
                return;
            }

            view.MoveTween?.Kill();
            view.RotateTween?.Kill();
            view.MoveTween = DOTween
                .To(() => view.Rect.anchoredPosition, value => view.Rect.anchoredPosition = value, targetPos, 0.1f)
                .SetEase(DG.Tweening.Ease.OutQuad)
                .SetUpdate(true);

            float startZ = NormalizeAngle180(view.Rect.localEulerAngles.z);
            float endZ = startZ + Mathf.DeltaAngle(startZ, targetAngle);
            view.RotateTween = DOTween
                .To(() => startZ, z =>
                {
                    startZ = z;
                    Vector3 euler = view.Rect.localEulerAngles;
                    euler.z = z;
                    view.Rect.localEulerAngles = euler;
                }, endZ, 0.1f)
                .SetEase(DG.Tweening.Ease.OutQuad)
                .SetUpdate(true);
        }

        private static float NormalizeAngle180(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            if (angle < -180f) angle += 360f;
            return angle;
        }

        private void AnimateCardScale(CardView view, float scale, float duration)
        {
            if (view == null || view.Rect == null)
            {
                return;
            }

            view.ScaleTween?.Kill();
            view.ScaleTween = DOTween
                .To(() => view.Rect.localScale, value => view.Rect.localScale = value, Vector3.one * scale, duration)
                .SetEase(DG.Tweening.Ease.OutQuad)
                .SetUpdate(true);
        }

        private void RenderBattleLog(RegicideBattleState state)
        {
            if (_txtBattleLog == null) return;

            StringBuilder builder = new StringBuilder(1024);
            if (!string.IsNullOrEmpty(_feedback))
            {
                builder.Append("反馈：").Append(_feedback).Append('\n').Append('\n');
            }

            int actionCount = 0;
            IReadOnlyList<RegicideActionBroadcastPayload> actions = GameModule.RegicideBattle.ActionBroadcasts;
            if (actions != null && actions.Count > 0)
            {
                actionCount = actions.Count;
                builder.Append("多人动作广播：").Append('\n');
                for (int i = 0; i < actions.Count; i++)
                {
                    RegicideActionBroadcastPayload action = actions[i];
                    if (action == null) continue;

                    builder.Append("- #").Append(action.ServerSequence)
                        .Append(' ').Append(string.IsNullOrEmpty(action.ActorPlayerId) ? "未知玩家" : action.ActorPlayerId)
                        .Append(' ').Append(FormatActionType(action.ActionType))
                        .Append(" 牌 ").Append(FormatActionCards(action.PublicCards));

                    if (!string.IsNullOrEmpty(action.Summary))
                    {
                        builder.Append(" -> ").Append(action.Summary);
                    }

                    builder.Append('\n');
                }

                builder.Append('\n');
            }

            builder.Append("战斗日志：").Append('\n');
            int logCount = state?.BattleLog?.Count ?? 0;
            if (logCount <= 0)
            {
                builder.Append("暂无战斗日志。");
            }
            else
            {
                for (int i = 0; i < logCount; i++)
                {
                    builder.Append("- ").Append(state.BattleLog[i]).Append('\n');
                }
            }

            _txtBattleLog.text = builder.ToString();

            bool shouldAutoScroll = logCount != _lastRenderedLogCount || actionCount != _lastRenderedActionCount || _feedback != _lastRenderedFeedback;
            _lastRenderedLogCount = logCount;
            _lastRenderedActionCount = actionCount;
            _lastRenderedFeedback = _feedback;
            if (shouldAutoScroll)
            {
                AutoScrollLogToBottomAsync().Forget();
            }
        }

        private static string FormatActionType(RegicideActionBroadcastType type)
        {
            switch (type)
            {
                case RegicideActionBroadcastType.PlayCard: return "出牌";
                case RegicideActionBroadcastType.Pass: return "跳过";
                case RegicideActionBroadcastType.DiscardForDamage: return "弃牌承伤";
                case RegicideActionBroadcastType.StartMatch: return "开局";
                default: return "动作";
            }
        }

        private static string FormatActionCards(IList<string> cards)
        {
            if (cards == null || cards.Count <= 0) return "-";
            StringBuilder builder = new StringBuilder(32);
            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0) builder.Append(" + ");
                builder.Append(cards[i]);
            }
            return builder.ToString();
        }

        private void RefreshActionButtons(bool myTurn)
        {
            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            bool canInteract = !_actionProcessing && myTurn;
            bool awaitingDiscard = actions != null && actions.IsAwaitingDiscard;
            bool hasSelection = _selectedCardIndices.Count > 0;
            bool canPlayCurrentSelection = actions != null && actions.IsCurrentSelectionPlayable;

            if (_btnPlay != null)
            {
                bool showPlay = !awaitingDiscard && hasSelection;
                bool playInteractable = canInteract && showPlay && hasSelection && canPlayCurrentSelection;
                _btnPlay.gameObject.SetActive(showPlay);
                _btnPlay.interactable = playInteractable;
                Image playImage = _btnPlay.GetComponent<Image>();
                if (playImage != null)
                {
                    playImage.color = playInteractable
                        ? new Color(0.18f, 0.45f, 0.23f, 0.95f)
                        : new Color(0.28f, 0.29f, 0.32f, 0.85f);
                }
            }

            if (_btnPass != null)
            {
                bool showPass = !awaitingDiscard && !hasSelection;
                bool passInteractable = canInteract && showPass && actions != null && actions.CanPass;
                _btnPass.gameObject.SetActive(showPass);
                _btnPass.interactable = passInteractable;
            }

            if (_btnDefend != null)
            {
                if (awaitingDiscard)
                {
                    _btnDefend.interactable = canInteract && actions != null && actions.CanDefend;
                }
                else
                {
                    bool canAux = false;
                    if (actions != null && actions.IsCurrentSelectionJester && actions.SelectableNextPlayerIndices.Count > 1) canAux = true;
                    else if (_selectedCardIndices.Count > 0) canAux = true;
                    _btnDefend.interactable = canInteract && canAux;
                }
            }
        }

private void EnsureCardViewCount(int count)
        {
            if (_rectHandArea == null || _cardTemplateButton == null)
            {
                if (!_templateMissingLogged)
                {
                    Debug.LogWarning("RegicideBattleUI: 手牌区域或模板节点未绑定，已跳过手牌渲染。");
                    _templateMissingLogged = true;
                }
                return;
            }

            _templateMissingLogged = false;

            while (_cardViews.Count < count)
            {
                int cardSlot = _cardViews.Count;
                GameObject cardGo = UnityEngine.Object.Instantiate(_cardTemplateButton.gameObject, _rectHandArea, false);
                cardGo.name = $"m_btnCard_{cardSlot}";
                cardGo.SetActive(true);

                CardView view = new CardView
                {
                    Root = cardGo,
                    Rect = cardGo.GetComponent<RectTransform>(),
                    Button = cardGo.GetComponent<Button>(),
                    Background = cardGo.GetComponent<Image>(),
                    Title = cardGo.transform.Find("m_txtCardTitle")?.GetComponent<Text>() ?? cardGo.transform.Find("m_txtLabel")?.GetComponent<Text>(),
                    Desc = cardGo.transform.Find("m_txtDesc")?.GetComponent<Text>(),
                    CanvasGroup = cardGo.GetComponent<CanvasGroup>() ?? cardGo.AddComponent<CanvasGroup>(),
                    InteractProxy = cardGo.GetComponent<RegicideHandCardInteractProxy>() ?? cardGo.AddComponent<RegicideHandCardInteractProxy>(),
                    CardIndex = cardSlot,
                };

                if (view.InteractProxy != null)
                {
                    view.InteractProxy.Bind(this, cardSlot);
                }

                if (view.Button != null)
                {
                    view.Button.onClick.RemoveAllListeners();
                    int captured = cardSlot;
                    view.Button.onClick.AddListener(() => OnCardSelected(captured));
                }

                _cardViews.Add(view);
            }

            for (int i = 0; i < _cardViews.Count; i++)
            {
                bool active = i < count;
                CardView view = _cardViews[i];
                if (view == null || view.Root == null)
                {
                    continue;
                }

                if (view.InteractProxy != null)
                {
                    view.InteractProxy.Bind(this, i);
                }

                if (!active)
                {
                    view.IsDragging = false;
                    view.MoveTween?.Kill();
                    view.RotateTween?.Kill();
                    view.ScaleTween?.Kill();
                    view.LayoutInitialized = false;
                }

                view.Root.SetActive(active);
            }
        }

        private void OnCardSelected(int cardIndex)
        {
            if (_isDraggingCard || _actionProcessing || !GameModule.RegicideBattle.IsMyTurn) return;
            bool removed = _selectedCardIndices.Contains(cardIndex);
            if (removed)
            {
                _selectedCardIndices.Remove(cardIndex);
            }
            else
            {
                _selectedCardIndices.Add(cardIndex);
            }

            _selectedCardIndices.Sort();

            RegicideAvailableActionSnapshot actions = GameModule.RegicideBattle.GetAvailableActionSnapshot(_selectedCardIndices, _selectedNextPlayerIndex);
            if (_selectedCardIndices.Count <= 0)
            {
                _feedback = "已取消选牌，可选择跳过回合。";
            }
            else if (actions != null && !actions.IsCurrentSelectionPlayable && !string.IsNullOrEmpty(actions.Message))
            {
                _feedback = actions.Message;
            }

            RefreshBattleView();
        }

        private void UpdateFeedbackFromState(RegicideBattleState state)
        {
            if (state == null) return;

            if (_knownSessionId != state.SessionId)
            {
                _knownSessionId = state.SessionId;
                _knownLogCount = 0;
                _lastRenderedLogCount = -1;
                _lastRenderedActionCount = -1;
                _feedback = string.Empty;
                _playingFxQueue = false;
                _pendingFxQueue.Clear();
                _pendingActionFxList.Clear();
                _pendingActionFxSeqSet.Clear();
                _lastPlayedActionSequence = 0;
                _visiblePlayedCards.Clear();
                _playedCardsVersion++;
                HideStageFx();
                _enemyTransitionLocked = false;
                ResetDraggingCardState(true);
                _hoverCardIndex = -1;
                _navigatingResult = false;
                RenderPlayedCardsPanel();
            }

            if (state.BattleLog == null) return;
            if (state.BattleLog.Count < _knownLogCount) _knownLogCount = 0;

            if (state.BattleLog.Count > _knownLogCount)
            {
                for (int i = _knownLogCount; i < state.BattleLog.Count; i++)
                {
                    string fxText = BuildStageFxText(state.BattleLog[i]);
                    if (!string.IsNullOrEmpty(fxText)) _pendingFxQueue.Enqueue(fxText);
                }

                _feedback = state.BattleLog[state.BattleLog.Count - 1];
                _knownLogCount = state.BattleLog.Count;
                PlayStageFxQueueAsync().Forget();
            }
        }

        private async UniTaskVoid PlayActionFxQueueAsync()
        {
            if (_playingActionFxQueue) return;
            _playingActionFxQueue = true;

            try
            {
                while (_pendingActionFxList.Count > 0)
                {
                    RegicideActionBroadcastPayload payload = _pendingActionFxList[0];
                    _pendingActionFxList.RemoveAt(0);
                    if (payload == null) continue;
                    if (payload.ServerSequence > 0)
                    {
                        _pendingActionFxSeqSet.Remove(payload.ServerSequence);
                        if (payload.ServerSequence <= _lastPlayedActionSequence)
                        {
                            continue;
                        }

                        _lastPlayedActionSequence = payload.ServerSequence;
                    }

                    if (payload.PublicCards != null && payload.PublicCards.Count > 0)
                    {
                        ShowPlayedCards(payload.PublicCards);
                    }

                    switch (payload.ActionType)
                    {
                        case RegicideActionBroadcastType.PlayCard:
                            await AnimatePlayerAttackSequenceAsync(payload);
                            break;
                        case RegicideActionBroadcastType.DiscardForDamage:
                            await AnimateBossCounterSequenceAsync(payload);
                            break;
                    }

                    await UniTask.Delay(60);
                }
            }
            finally
            {
                _playingActionFxQueue = false;
            }
        }

        private bool TryQueueActionFxPayload(RegicideActionBroadcastPayload payload)
        {
            if (payload == null)
            {
                return false;
            }

            long sequence = payload.ServerSequence;
            if (sequence <= 0)
            {
                _pendingActionFxList.Add(payload);
                return true;
            }

            if (sequence <= _lastPlayedActionSequence || _pendingActionFxSeqSet.Contains(sequence))
            {
                return false;
            }

            int insertIndex = _pendingActionFxList.Count;
            for (int i = 0; i < _pendingActionFxList.Count; i++)
            {
                RegicideActionBroadcastPayload queued = _pendingActionFxList[i];
                long queuedSeq = queued != null ? queued.ServerSequence : 0;
                if (queuedSeq <= 0)
                {
                    continue;
                }

                if (sequence < queuedSeq)
                {
                    insertIndex = i;
                    break;
                }
            }

            _pendingActionFxList.Insert(insertIndex, payload);
            _pendingActionFxSeqSet.Add(sequence);
            return true;
        }

        private async UniTask AnimatePlayerAttackSequenceAsync(RegicideActionBroadcastPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            await AnimatePlayerDashAsync(payload.ActorPlayerId);
            await AnimateEnemyHitAsync();

            bool enemyDefeated = payload.EnemyDefeated || (payload.EnemyHealthBefore > 0 && payload.EnemyHealthAfter == 0);
            if (enemyDefeated)
            {
                _enemyTransitionLocked = true;
                try
                {
                    await AnimateEnemyDeathAsync();
                    await UniTask.Delay(420);
                }
                finally
                {
                    _enemyTransitionLocked = false;
                }

                RefreshBattleView();
            }
        }

        private async UniTask AnimateBossCounterSequenceAsync(RegicideActionBroadcastPayload payload)
        {
            await AnimateBossCounterAsync();
            await AnimatePlayerHitAsync(payload?.ActorPlayerId);
        }

        private async UniTask AnimatePlayerDashAsync(string playerId)
        {
            await GameModule.RegicideBattlePresentation.PlayPlayerAttackAsync(playerId);
        }

        private async UniTask AnimateBossCounterAsync()
        {
            await GameModule.RegicideBattlePresentation.PlayEnemyCounterAsync();
        }

        private async UniTask AnimateEnemyHitAsync()
        {
            await GameModule.RegicideBattlePresentation.PlayEnemyHitAsync();
        }

        private async UniTask AnimateEnemyDeathAsync()
        {
            await GameModule.RegicideBattlePresentation.PlayEnemyDefeatAsync();
        }

        private async UniTask AnimatePlayerHitAsync(string playerId)
        {
            await GameModule.RegicideBattlePresentation.PlayPlayerHitAsync(playerId);
        }


        private string BuildStageFxText(string logLine)
        {
            if (string.IsNullOrEmpty(logLine)) return string.Empty;
            if (logLine.Contains("摸牌") || logLine.Contains("Draw")) return "补牌中...";
            if (logLine.Contains("反击") || logLine.Contains("Counter")) return "敌人反击...";
            if (logLine.Contains("敌人登场") || logLine.Contains("新的敌人") || logLine.Contains("Enemy")) return "敌人切换...";
            if (logLine.Contains("承伤弃牌") || logLine.Contains("Discard")) return "承伤结算中...";
            return string.Empty;
        }

        private async UniTaskVoid PlayStageFxQueueAsync()
        {
            if (_playingFxQueue) return;
            _playingFxQueue = true;
            while (_pendingFxQueue.Count > 0)
            {
                string message = _pendingFxQueue.Dequeue();
                ShowStageFx(message);
                await UniTask.Delay(360);
                HideStageFx();
                await UniTask.Delay(90);
            }
            _playingFxQueue = false;
        }

        private void ShowStageFx(string message)
        {
            if (_txtStageFx == null) return;
            _txtStageFx.text = message;
            _txtStageFx.gameObject.SetActive(true);
        }

        private void HideStageFx()
        {
            if (_txtStageFx == null) return;
            _txtStageFx.text = string.Empty;
            _txtStageFx.gameObject.SetActive(false);
        }

        private async UniTaskVoid AutoScrollLogToBottomAsync()
        {
            if (_pendingAutoScroll || _scrollBattleLog == null) return;
            _pendingAutoScroll = true;
            await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
            if (_scrollBattleLog != null)
            {
                Canvas.ForceUpdateCanvases();
                _scrollBattleLog.verticalNormalizedPosition = 0f;
            }
            _pendingAutoScroll = false;
        }

        private async UniTaskVoid NavigateToResultAsync()
        {
            await UniTask.DelayFrame(1);
            GameEvent.Send(RegicideEventIds.UiNavigateResult);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ResetDraggingCardState(false);
            _hoverCardIndex = -1;

            for (int i = 0; i < _cardViews.Count; i++)
            {
                CardView view = _cardViews[i];
                if (view == null)
                {
                    continue;
                }

                view.MoveTween?.Kill();
                view.RotateTween?.Kill();
                view.ScaleTween?.Kill();
            }
        }

        private string BuildHelpContent()
        {
            StringBuilder builder = new StringBuilder(640);
            builder.AppendLine("目标：击败全部敌人。");
            builder.AppendLine("出牌：可单出，也可同点数组合（总点数 <= 10）。");
            builder.AppendLine("A（1点）：可单出，或与另一张牌配对，本次攻击 +1 并结算额外花色。");
            builder.AppendLine("小丑：只能单独打出，取消敌人花色免疫，本回合跳过伤害与承伤，并指定下一位行动者。");
            builder.AppendLine("敌人未死会反击：当前玩家必须弃牌承伤，弃牌总点数需 >= 敌人当前攻击，否则全队失败。");
            builder.AppendLine();
            builder.AppendLine("花色效果：");
            builder.AppendLine(" - 黑桃：敌人攻击 -X");
            builder.AppendLine(" - 红心：回收弃牌到牌库（最多 X 张）");
            builder.AppendLine(" - 方块：摸 X 张牌");
            builder.AppendLine(" - 梅花：本次伤害翻倍");
            builder.AppendLine(" - 同花色敌人免疫对应花色能力（不免疫基础伤害）");
            builder.AppendLine();
            builder.AppendLine("多人提示：");
            builder.AppendLine(" - 可查看队友公开状态（看不到手牌明细）");
            builder.AppendLine(" - 日志可查看他人出牌与结算摘要");
            builder.AppendLine(" - 非你回合时，主动按钮会自动禁用");
            return builder.ToString();
        }

        private Transform FindTransformByName(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return null;
            Transform root = rectTransform != null ? rectTransform : transform;
            if (root == null) return null;

            Transform direct = root.Find(nodeName);
            if (direct != null) return direct;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform item = all[i];
                if (item != null && item.name == nodeName) return item;
            }
            return null;
        }

        private T FindComponentByName<T>(string nodeName) where T : Component
        {
            Transform target = FindTransformByName(nodeName);
            return target != null ? target.GetComponent<T>() : null;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = sizeDelta;
        }

        private static string GetCardDisplay(RegicideCard card)
        {
            if (card == null) return "空牌";
            return $"{GetSuitDisplay(card.Suit)}{GetRankDisplay(card.Rank)}";
        }

        private static string DescribeCardEffect(RegicideCard card)
        {
            if (card == null) return "无效。";
            if (card.IsJester) return "取消当前敌人花色免疫；本回合跳过伤害与承伤；指定下一位行动者。";

            switch (card.Suit)
            {
                case RegicideSuit.Spade: return $"点数 {card.AttackValue}：敌人攻击 -{card.AttackValue}";
                case RegicideSuit.Heart: return $"点数 {card.AttackValue}：回收弃牌到牌库（最多 {card.AttackValue} 张）";
                case RegicideSuit.Club: return $"点数 {card.AttackValue}：本次伤害翻倍";
                case RegicideSuit.Diamond: return $"点数 {card.AttackValue}：摸牌 {card.AttackValue} 张";
                default: return $"点数 {card.AttackValue}：基础伤害";
            }
        }

        private static string FormatPublicPhase(string phase)
        {
            switch (phase)
            {
                case "Ready": return "已准备";
                case "Waiting": return "等待中";
                case "Acting": return "行动中";
                case "WaitingTurn": return "等待回合";
                case "Discarding": return "承伤弃牌";
                case "WaitingDiscard": return "等待承伤";
                case "GameOver": return "对局结束";
                default: return string.IsNullOrEmpty(phase) ? "未知" : phase;
            }
        }

        private static string GetSuitDisplay(RegicideSuit suit)
        {
            switch (suit)
            {
                case RegicideSuit.Spade: return "黑桃";
                case RegicideSuit.Heart: return "红心";
                case RegicideSuit.Club: return "梅花";
                case RegicideSuit.Diamond: return "方块";
                case RegicideSuit.Joker: return "小丑";
                default: return "未知";
            }
        }

        private static string GetRankDisplay(int rank)
        {
            switch (rank)
            {
                case 0: return "Jester";
                case 1: return "A";
                case 11: return "J";
                case 12: return "Q";
                case 13: return "K";
                default: return rank.ToString();
            }
        }

        private static Color BuildCardColor(RegicideSuit suit, bool selected, bool playable)
        {
            Color baseColor;
            switch (suit)
            {
                case RegicideSuit.Spade: baseColor = new Color(0.2f, 0.22f, 0.25f, 0.95f); break;
                case RegicideSuit.Heart: baseColor = new Color(0.56f, 0.22f, 0.24f, 0.96f); break;
                case RegicideSuit.Club: baseColor = new Color(0.2f, 0.42f, 0.24f, 0.96f); break;
                case RegicideSuit.Diamond: baseColor = new Color(0.56f, 0.44f, 0.2f, 0.96f); break;
                case RegicideSuit.Joker: baseColor = new Color(0.65f, 0.52f, 0.2f, 0.96f); break;
                default: baseColor = new Color(0.34f, 0.26f, 0.56f, 0.96f); break;
            }

            if (!playable) baseColor = Color.Lerp(baseColor, new Color(0.4f, 0.4f, 0.4f, 0.96f), 0.55f);
            if (selected) baseColor = Color.Lerp(baseColor, new Color(0.95f, 0.95f, 0.95f, 1f), 0.28f);
            return baseColor;
        }
    }
}
