using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
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

        private sealed class CardView
        {
            public GameObject Root;
            public RectTransform Rect;
            public Button Button;
            public Image Background;
            public Text Title;
            public Text Desc;
            public int CardIndex;
        }

        private sealed class PlayedCardView
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image Background;
            public Text Title;
            public Text Desc;
        }

        private Button _btnPlay;
        private Button _btnPass;
        private Button _btnDefend;
        private Button _btnHelp;
        private Button _btnHelpClose;
        private Button _cardTemplateButton;

        private Text _txtPlayLabel;
        private Text _txtPassLabel;
        private Text _txtDefendLabel;
        private Text _txtHelpLabel;
        private Text _txtHelpCloseLabel;

        private Text _txtEnemyInfo;
        private Text _txtPlayerInfo;
        private Text _txtActionHint;
        private Text _txtSelectedCard;
        private Text _txtOtherPlayers;
        private Text _txtBattleLog;
        private Text _txtHelpContent;
        private Text _txtStageFx;
        private Text _txtRoundInfo;

        private RectTransform _rectHandArea;
        private RectTransform _rectPlayedCardsArea;
        private ScrollRect _scrollBattleLog;
        private GameObject _playedCardTemplate;

        private GameObject _helpMask;
        private bool _actionProcessing;
        private bool _navigatingResult;
        private bool _playingFxQueue;
        private bool _pendingAutoScroll;
        private bool _templateMissingLogged;
        private bool _playedCardTemplateMissingLogged;
        private bool _playingActionFxQueue;

        private readonly List<int> _selectedCardIndices = new List<int>();
        private readonly List<CardView> _cardViews = new List<CardView>();
        private readonly List<PlayedCardView> _playedCardViews = new List<PlayedCardView>();
        private readonly List<string> _visiblePlayedCards = new List<string>();
        private readonly Queue<string> _pendingFxQueue = new Queue<string>();
        private readonly Queue<RegicideActionBroadcastPayload> _pendingActionFxQueue = new Queue<RegicideActionBroadcastPayload>();

        private int _selectedNextPlayerIndex = -1;
        private int _knownLogCount;
        private int _lastRenderedLogCount = -1;
        private int _lastRenderedActionCount = -1;
        private int _playedCardsVersion;
        private string _knownSessionId = string.Empty;
        private string _feedback = string.Empty;
        private string _lastRenderedFeedback = string.Empty;
        private long _lastQueuedActionSequence;

        protected override void ScriptGenerator()
        {
            _btnPlay = FindComponentByName<Button>("m_btnPlayFirst");
            _btnPass = FindComponentByName<Button>("m_btnPass");
            _btnDefend = FindComponentByName<Button>("m_btnDefend");
            _btnHelp = FindComponentByName<Button>("m_btnHelp");
            _btnHelpClose = FindComponentByName<Button>("m_btnHelpClose");
            _cardTemplateButton = FindComponentByName<Button>("m_btnCardTemplate");

            _txtPlayLabel = FindComponentByName<Text>("m_txtPlayLabel") ?? _btnPlay?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtPassLabel = FindComponentByName<Text>("m_txtPassLabel") ?? _btnPass?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtDefendLabel = FindComponentByName<Text>("m_txtDefendLabel") ?? _btnDefend?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtHelpLabel = FindComponentByName<Text>("m_txtHelpLabel") ?? _btnHelp?.transform.Find("m_txtLabel")?.GetComponent<Text>();
            _txtHelpCloseLabel = FindComponentByName<Text>("m_txtHelpCloseLabel") ?? _btnHelpClose?.transform.Find("m_txtLabel")?.GetComponent<Text>();

            _txtEnemyInfo = FindComponentByName<Text>("m_txtEnemyInfo");
            _txtPlayerInfo = FindComponentByName<Text>("m_txtPlayerInfo");
            _txtActionHint = FindComponentByName<Text>("m_txtActionHint");
            _txtSelectedCard = FindComponentByName<Text>("m_txtSelectedCard");
            _txtOtherPlayers = FindComponentByName<Text>("m_txtOtherPlayers");
            _txtBattleLog = FindComponentByName<Text>("m_txtBattle");
            _txtHelpContent = FindComponentByName<Text>("m_txtHelpContent");
            _txtStageFx = FindComponentByName<Text>("m_txtStageFx");
            _txtRoundInfo = FindComponentByName<Text>("m_txtRoundInfo");

            _rectHandArea = FindComponentByName<RectTransform>("m_rectHandArea");
            _rectPlayedCardsArea = FindComponentByName<RectTransform>("m_rectPlayedCardsArea");
            _scrollBattleLog = FindComponentByName<ScrollRect>("m_scrollBattleLog");
            _playedCardTemplate = FindTransformByName("m_goPlayedCardTemplate")?.gameObject;

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
            if (_btnHelp != null) _btnHelp.onClick.AddListener(OnHelpClicked);
            if (_btnHelpClose != null) _btnHelpClose.onClick.AddListener(OnHelpCloseClicked);
        }

protected override void OnCreate()
        {
            if (_txtPlayLabel != null) _txtPlayLabel.text = "出牌";
            if (_txtPassLabel != null) _txtPassLabel.text = "跳过";
            if (_txtDefendLabel != null) _txtDefendLabel.text = "辅助操作";
            if (_txtHelpLabel != null) _txtHelpLabel.text = "?";
            if (_txtHelpCloseLabel != null) _txtHelpCloseLabel.text = "关闭说明";
            if (_helpMask != null) _helpMask.SetActive(false);
            if (_cardTemplateButton != null) _cardTemplateButton.gameObject.SetActive(false);

            _knownSessionId = string.Empty;
            _knownLogCount = 0;
            _lastRenderedLogCount = -1;
            _lastRenderedActionCount = -1;
            _feedback = string.Empty;
            _lastRenderedFeedback = string.Empty;
            _playingFxQueue = false;
            _pendingAutoScroll = false;
            _pendingFxQueue.Clear();
            _pendingActionFxQueue.Clear();
            _visiblePlayedCards.Clear();
            _navigatingResult = false;
            _playingActionFxQueue = false;
            _selectedCardIndices.Clear();
            _selectedNextPlayerIndex = -1;
            _playedCardsVersion = 0;
            _lastQueuedActionSequence = 0;
            HideStageFx();
            RenderPlayedCardsPanel();
            RefreshBattleView();
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

            if (payload.ServerSequence > _lastQueuedActionSequence)
            {
                _lastQueuedActionSequence = payload.ServerSequence;
                _pendingActionFxQueue.Enqueue(payload);
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
                _feedback = string.IsNullOrEmpty(actions.Message) ? "当前选牌组合无法出牌。" : actions.Message;
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

            UpdateFeedbackFromState(state);
            RenderEnemyPanel(state);
            RenderPlayerPanel(state, player, actions);
            RenderOtherPlayersPanel(state);
            RenderPlayedCardsPanel();
            RenderCardPanel(player, actions);
            RenderBattleLog(state);
            GameModule.RegicideBattlePresentation.SyncBattleState(state, GameModule.RegicideBattle.PublicStateSnapshot, GameModule.RegicideBattle.LocalPlayerId);
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

            if (_txtEnemyInfo == null) return;
            if (state == null || state.CurrentEnemy == null)
            {
                _txtEnemyInfo.text = "等待战斗初始化...";
                return;
            }

            RegicideEnemyState enemy = state.CurrentEnemy;
            string immunity = state.IsEnemyImmunityDisabledByJester ? "无" : GetSuitDisplay(enemy.Suit);
            _txtEnemyInfo.text =
                $"{enemy.Name}\n" +
                $"攻击：{Mathf.Max(0, enemy.Attack)}   生命：{Mathf.Max(0, enemy.Health)}   花色：{GetSuitDisplay(enemy.Suit)}\n" +
                $"花色免疫：{immunity}";
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
                    _txtActionHint.text = $"提示：{actions.Message}";
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
            if (_txtPassLabel != null) _txtPassLabel.text = actions != null && actions.IsAwaitingDiscard ? "跳过(承伤中)" : "跳过";
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
                $"攻击值：{actions.CurrentSelectionAttackValue}    出牌合法：{(actions.IsCurrentSelectionPlayable ? "是" : "否")}";

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
            if (_txtOtherPlayers == null)
            {
                return;
            }

            StringBuilder builder = new StringBuilder(320);
            builder.AppendLine("队友状态：");
            bool hasAny = false;
            string localPlayerId = GameModule.RegicideBattle.LocalPlayerId;
            RegicidePublicStateSnapshotPayload snapshot = GameModule.RegicideBattle.PublicStateSnapshot;

            if (snapshot?.Players != null && snapshot.Players.Count > 0)
            {
                for (int i = 0; i < snapshot.Players.Count; i++)
                {
                    RegicidePublicPlayerState player = snapshot.Players[i];
                    if (player == null || string.IsNullOrEmpty(player.PlayerId))
                    {
                        continue;
                    }

                    bool isLocal = string.Equals(player.PlayerId, localPlayerId);
                    string localTag = isLocal ? "（你）" : string.Empty;
                    string onlineTag = player.IsOnline ? "在线" : "离线";
                    builder.Append($"[{Mathf.Max(1, player.SeatIndex + 1)}] {player.PlayerId}{localTag}  手牌x{Mathf.Max(0, player.HandCount)}  {FormatPublicPhase(player.Phase)}  {onlineTag}");
                    if (player.IsCurrentTurn) builder.Append("  当前行动");
                    else if (player.IsPendingDiscardTarget) builder.Append("  等待承伤");
                    builder.AppendLine();
                    hasAny = true;
                }
            }
            else if (state?.Players != null && state.Players.Count > 0)
            {
                for (int i = 0; i < state.Players.Count; i++)
                {
                    RegicidePlayerState player = state.Players[i];
                    if (player == null || string.IsNullOrEmpty(player.PlayerId))
                    {
                        continue;
                    }

                    bool isLocal = string.Equals(player.PlayerId, localPlayerId);
                    string localTag = isLocal ? "（你）" : string.Empty;
                    string phase = state.IsAwaitingDiscard && i == state.PendingDiscardTargetPlayerIndex
                        ? "Discarding"
                        : (i == state.CurrentPlayerIndex ? "Acting" : "WaitingTurn");
                    builder.Append($"[{i + 1}] {player.PlayerId}{localTag}  手牌x{player.Hand?.Count ?? 0}  {FormatPublicPhase(phase)}");
                    builder.AppendLine();
                    hasAny = true;
                }
            }

            if (!hasAny)
            {
                builder.Append("暂无其他玩家状态。");
            }

            _txtOtherPlayers.text = builder.ToString();
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
                    view.Desc.text = "鏈鎵撳嚭";
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

            float rowWidth = _rectHandArea != null ? _rectHandArea.rect.width - 80f : 1100f;
            float maxSpread = Mathf.Max(420f, Mathf.Min(rowWidth, 1020f));
            float spread = count <= 1 ? 0f : maxSpread;
            float angleRange = Mathf.Lerp(16f, 46f, Mathf.Clamp01((count - 1) / 7f));
            float baseY = 10f;
            float lift = Mathf.Lerp(16f, 56f, Mathf.Clamp01(count / 10f));

            for (int i = 0; i < count; i++)
            {
                CardView view = _cardViews[i];
                RegicideCard card = player.Hand[i];
                bool playable = actions != null && actions.PlayableCardIndices.Contains(i);
                bool selected = _selectedCardIndices.Contains(i);
                float t = count <= 1 ? 0f : i / (count - 1f);
                float centered = t - 0.5f;
                float x = spread * centered;
                float arc = 1f - Mathf.Abs(centered) * 2f;
                float y = baseY + arc * lift + (selected ? 34f : 0f);
                float angle = -centered * 2f * angleRange;

                view.CardIndex = i;
                view.Root.SetActive(true);
                SetRect(view.Rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(CardWidth, CardHeight));
                view.Rect.localEulerAngles = new Vector3(0f, 0f, angle);
                if (view.Button != null) view.Button.interactable = playable && !_actionProcessing && GameModule.RegicideBattle.IsMyTurn;
                if (view.Background != null) view.Background.color = BuildCardColor(card.Suit, selected, playable);
                if (view.Title != null) view.Title.text = $"{GetCardDisplay(card)}\n点数 {card.AttackValue}";
                if (view.Desc != null) view.Desc.text = DescribeCardEffect(card);
            }
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
                        .Append(" 牌 " ).Append(FormatActionCards(action.PublicCards));

                    if (!string.IsNullOrEmpty(action.Summary))
                    {
                        builder.Append(" -> " ).Append(action.Summary);
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
                    builder.Append("- " ).Append(state.BattleLog[i]).Append('\n');
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

            if (_btnPlay != null) _btnPlay.interactable = canInteract && !actions.IsAwaitingDiscard && actions.IsCurrentSelectionPlayable;
            if (_btnPass != null) _btnPass.interactable = canInteract && actions.CanPass;

            if (_btnDefend != null)
            {
                if (actions.IsAwaitingDiscard)
                {
                    _btnDefend.interactable = canInteract && actions.CanDefend;
                }
                else
                {
                    bool canAux = false;
                    if (actions.IsCurrentSelectionJester && actions.SelectableNextPlayerIndices.Count > 1) canAux = true;
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
                    CardIndex = cardSlot,
                };

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
                if (_cardViews[i].Root != null) _cardViews[i].Root.SetActive(active);
            }
        }

        private void OnCardSelected(int cardIndex)
        {
            if (_actionProcessing || !GameModule.RegicideBattle.IsMyTurn) return;
            if (_selectedCardIndices.Contains(cardIndex)) _selectedCardIndices.Remove(cardIndex);
            else _selectedCardIndices.Add(cardIndex);
            _selectedCardIndices.Sort();
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
                _pendingActionFxQueue.Clear();
                _visiblePlayedCards.Clear();
                _playedCardsVersion++;
                HideStageFx();
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
                while (_pendingActionFxQueue.Count > 0)
                {
                    RegicideActionBroadcastPayload payload = _pendingActionFxQueue.Dequeue();
                    if (payload == null) continue;

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

        private async UniTask AnimatePlayerAttackSequenceAsync(RegicideActionBroadcastPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            await AnimatePlayerDashAsync(payload.ActorPlayerId);
            await AnimateEnemyHitAsync();

            bool enemyDefeated = payload.EnemyHealthBefore > 0 && payload.EnemyHealthAfter == 0;
            if (enemyDefeated)
            {
                await AnimateEnemyDeathAsync();
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

private string BuildHelpContent()
        {
            StringBuilder builder = new StringBuilder(640);
            builder.AppendLine("目标：击败全部敌人。");
            builder.AppendLine("出牌：可单出，也可同点数组合（总点数 <= 10）。");
            builder.AppendLine("A（1点）：可单出，或与另一张牌配对，额外+1并结算双花色。");
            builder.AppendLine("小丑：只能单独打出，取消敌人花色免疫，本回合跳过伤害与承伤，并指定下一位行动者。");
            builder.AppendLine("敌人未死会反击：当前玩家必须弃牌承伤，弃牌总点数至少等于敌人攻击，否则全队失败。");
            builder.AppendLine();
            builder.AppendLine("花色效果：");
            builder.AppendLine(" - 黑桃：敌人攻击 -X");
            builder.AppendLine(" - 红心：回收弃牌到牌库（最多 X 张）");
            builder.AppendLine(" - 方块：摸 X 张牌");
            builder.AppendLine(" - 梅花：本次伤害翻倍");
            builder.AppendLine(" - 同花色敌人免疫对应花色能力（不免疫基础伤害）");
            builder.AppendLine();
            builder.AppendLine("多人提示：");
            builder.AppendLine(" - 左侧可查看队友公开状态（不显示手牌明细）");
            builder.AppendLine(" - 右侧日志可查看他人出牌与结算摘要");
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
            if (card == null) return "无效果。";
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
