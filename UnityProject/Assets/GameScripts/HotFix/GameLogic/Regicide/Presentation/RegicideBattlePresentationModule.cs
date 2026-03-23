using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using GameProto.Regicide;
using TEngine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameLogic.Regicide
{
    /// <summary>
    /// 独立 2D 战斗场景表现层。
    /// 仅负责角色/敌人占位与动画，不参与规则结算。
    /// </summary>
    public sealed class RegicideBattlePresentationModule : Singleton<RegicideBattlePresentationModule>
    {
        public const string BattleSceneLocation = "RegicideBattle2DScene";

        private const string SceneName = "RegicideBattle2DScene";
        private const string RootName = "RegicideBattle2DRoot";
        private const string PlayerRootName = "PlayerActorRoot";
        private const string EnemyRootName = "EnemyActorRoot";
        private const string EnemyActorName = "EnemyActor";
        private const string PlayerActionPointName = "PlayerActionPoint";
        private const string BossAttackPointName = "BossAttackPoint";
        private const string EnemyHitPointName = "EnemyHitPoint";
        private const string BattleCameraName = "RegicideBattleCamera";
        private const string LightBanditControllerPath = "Assets/Bandits - Pixel Art/Animations/Light Bandit/LightBandit_AnimController.controller";
        private const string HeavyBanditControllerPath = "Assets/Bandits - Pixel Art/Animations/Heavy Bandit/HeavyBandit_AnimController.overrideController";
        private const int CombatIdleAnimState = 1;
        private const float EnemyDefeatHoldSeconds = 0.45f;

        private const int MaxPlayerSlots = 4;

        private static Sprite _placeholderSprite;
        private static Texture2D _placeholderTexture;

        private readonly Dictionary<string, int> _playerSlotLookup = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Transform[] _slotRoots = new Transform[MaxPlayerSlots];
        private readonly Transform[] _actorRoots = new Transform[MaxPlayerSlots];
        private readonly SpriteRenderer[] _actorRenderers = new SpriteRenderer[MaxPlayerSlots];
        private readonly RegicideActorAnimationDriver[] _actorAnimDrivers = new RegicideActorAnimationDriver[MaxPlayerSlots];
        private readonly Vector3[] _slotIdleWorld = new Vector3[MaxPlayerSlots];

        private Scene _scene;
        private string _loadedLocation = string.Empty;
        private bool _sceneLoaded;
        private bool _isLoading;

        private Transform _root;
        private Transform _playerRoot;
        private Transform _enemyRoot;
        private Transform _enemyActor;
        private SpriteRenderer _enemyRenderer;
        private RegicideActorAnimationDriver _enemyAnimDriver;
        private Transform _playerActionPoint;
        private Transform _bossAttackPoint;
        private Transform _enemyHitPoint;
        private Camera _battleCamera;

        private Vector3 _enemyIdleWorld;
        private bool _enemyDeathLatched;
        private float _enemyDefeatHoldUntil;

        protected override void OnInit()
        {
        }

        protected override void OnRelease()
        {
            _sceneLoaded = false;
            _isLoading = false;
            _loadedLocation = string.Empty;
            ClearBindings();
            ReleasePlaceholderSprite();
        }

        public async UniTask<bool> EnsureSceneLoadedAsync()
        {
            if (_sceneLoaded && IsBindingsValid())
            {
                return true;
            }

            while (_isLoading)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (_sceneLoaded && IsBindingsValid())
                {
                    return true;
                }
            }

            _isLoading = true;
            try
            {
                _scene = await LoadBattleSceneAsync();
                _sceneLoaded = true;
                CacheBindings();
                EnsurePlaceholderVisuals();
                CaptureIdleTransforms();
                ConfigureBattleAnimators();
                return IsBindingsValid();
            }
            catch (Exception exception)
            {
                Log.Error($"Regicide battle presentation load failed: {exception}");
                _sceneLoaded = false;
                ClearBindings();
                return false;
            }
            finally
            {
                _isLoading = false;
            }
        }

        public async UniTask ReleaseSceneAsync()
        {
            while (_isLoading)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (!_sceneLoaded)
            {
                ClearBindings();
                return;
            }

            try
            {
                string unloadLocation = string.IsNullOrEmpty(_loadedLocation) ? BattleSceneLocation : _loadedLocation;
                await GameModule.Scene.UnloadAsync(unloadLocation);
            }
            catch (Exception exception)
            {
                Log.Warning($"Regicide battle presentation unload failed: {exception.Message}");
            }

            _sceneLoaded = false;
            _loadedLocation = string.Empty;
            ClearBindings();
        }

        public void SyncBattleState(RegicideBattleState state, RegicidePublicStateSnapshotPayload publicSnapshot, string localPlayerId)
        {
            if (!_sceneLoaded || !IsBindingsValid())
            {
                return;
            }

            List<string> orderedPlayers = BuildOrderedPlayers(state, publicSnapshot, localPlayerId);
            RebuildPlayerSlots(orderedPlayers);
            RefreshEnemyVisual(state);
            EnsureFacingDirection();
        }

        public async UniTask PlayPlayerAttackAsync(string playerId)
        {
            if (!TryResolveSlot(playerId, out int slotIndex, out Transform slotTransform))
            {
                return;
            }

            TriggerPlayerAttack(slotIndex);
            Vector3 idle = _slotIdleWorld[slotIndex];
            Vector3 attack = _playerActionPoint != null ? _playerActionPoint.position : idle + new Vector3(2f, 0f, 0f);
            attack.y = idle.y;

            await TweenWorldPositionAsync(slotTransform, attack, 0.16f, DG.Tweening.Ease.OutCubic);
            await UniTask.Delay(70);
            await TweenWorldPositionAsync(slotTransform, idle, 0.14f, DG.Tweening.Ease.InCubic);
            slotTransform.position = idle;
            SetPlayerIdle(slotIndex);
        }

        public async UniTask PlayEnemyHitAsync()
        {
            if (_enemyActor == null)
            {
                return;
            }

            TriggerEnemyDamage();
            Vector3 origin = _enemyActor.position;
            Vector3 scale = _enemyActor.localScale;
            await ShakeWorldPositionAsync(_enemyActor, 0.16f, 0.18f);
            await TweenScaleAsync(_enemyActor, scale * 1.04f, 0.07f, DG.Tweening.Ease.OutQuad);
            await TweenScaleAsync(_enemyActor, scale, 0.08f, DG.Tweening.Ease.InQuad);
            _enemyActor.position = origin;
            _enemyActor.localScale = scale;
            if (!_enemyDeathLatched)
            {
                SetEnemyIdle();
            }
        }

        public async UniTask PlayEnemyDefeatAsync()
        {
            if (_enemyActor == null)
            {
                return;
            }

            _enemyDeathLatched = true;
            _enemyDefeatHoldUntil = Time.unscaledTime + EnemyDefeatHoldSeconds;
            TriggerEnemyDeath();
            Vector3 origin = _enemyActor.position;
            Vector3 scale = _enemyActor.localScale;
            Vector3 down = origin + new Vector3(0f, -0.45f, 0f);

            await UniTask.WhenAll(
                TweenWorldPositionAsync(_enemyActor, down, 0.22f, DG.Tweening.Ease.InQuad),
                TweenScaleAsync(_enemyActor, scale * 0.9f, 0.2f, DG.Tweening.Ease.InQuad));

            await UniTask.Delay(120);
            _enemyActor.position = origin;
            _enemyActor.localScale = scale;
        }

        public async UniTask PlayEnemyCounterAsync()
        {
            if (_enemyActor == null)
            {
                return;
            }

            TriggerEnemyAttack();
            Vector3 idle = _enemyIdleWorld;
            Vector3 attack = _bossAttackPoint != null ? _bossAttackPoint.position : idle + new Vector3(-2.2f, 0f, 0f);

            await TweenWorldPositionAsync(_enemyActor, attack, 0.2f, DG.Tweening.Ease.OutCubic);
            await UniTask.Delay(90);
            await TweenWorldPositionAsync(_enemyActor, idle, 0.18f, DG.Tweening.Ease.InCubic);
            _enemyActor.position = idle;
            if (!_enemyDeathLatched)
            {
                SetEnemyIdle();
            }
        }

        public async UniTask PlayPlayerHitAsync(string playerId)
        {
            if (!TryResolveSlot(playerId, out int slotIndex, out Transform slotTransform))
            {
                return;
            }

            TriggerPlayerDamage(slotIndex);
            Vector3 idle = _slotIdleWorld[slotIndex];
            await ShakeWorldPositionAsync(slotTransform, 0.12f, 0.14f);
            slotTransform.position = idle;
            SetPlayerIdle(slotIndex);
        }

        public bool TryGetPlayerHeadWorldPosition(string playerId, out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (!_sceneLoaded || !IsBindingsValid() || string.IsNullOrEmpty(playerId))
            {
                return false;
            }

            if (!_playerSlotLookup.TryGetValue(playerId, out int slotIndex))
            {
                return false;
            }

            if (slotIndex < 0 || slotIndex >= MaxPlayerSlots)
            {
                return false;
            }

            Transform actor = _actorRoots[slotIndex] != null ? _actorRoots[slotIndex] : _slotRoots[slotIndex];
            if (actor == null || !actor.gameObject.activeInHierarchy)
            {
                return false;
            }

            SpriteRenderer renderer = _actorRenderers[slotIndex];
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                worldPosition = new Vector3(bounds.center.x, bounds.max.y + 0.24f, bounds.center.z);
            }
            else
            {
                worldPosition = actor.position + new Vector3(0f, 1.1f, 0f);
            }

            return true;
        }

        public bool TryGetEnemyHeadWorldPosition(out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;
            if (!_sceneLoaded || !IsBindingsValid() || _enemyActor == null || !_enemyActor.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (_enemyRenderer != null)
            {
                Bounds bounds = _enemyRenderer.bounds;
                worldPosition = new Vector3(bounds.center.x, bounds.max.y + 0.28f, bounds.center.z);
            }
            else
            {
                worldPosition = _enemyActor.position + new Vector3(0f, 1.4f, 0f);
            }

            return true;
        }

        public bool TryGetBattleCamera(out Camera camera)
        {
            camera = _battleCamera;
            return camera != null && camera.gameObject.activeInHierarchy && camera.enabled;
        }

        private void CacheBindings()
        {
            _scene = SceneManager.GetSceneByName(SceneName);
            if (!_scene.IsValid() || !_scene.isLoaded)
            {
                return;
            }

            _root = FindRootByName(_scene, RootName);
            if (_root == null)
            {
                return;
            }

            _playerRoot = FindChildRecursive(_root, PlayerRootName);
            _enemyRoot = FindChildRecursive(_root, EnemyRootName);
            _playerActionPoint = FindChildRecursive(_root, PlayerActionPointName);
            _bossAttackPoint = FindChildRecursive(_root, BossAttackPointName);
            _enemyHitPoint = FindChildRecursive(_root, EnemyHitPointName);
            Transform cameraTransform = FindChildRecursive(_root, BattleCameraName);
            _battleCamera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
            _enemyActor = FindChildRecursive(_root, EnemyActorName);
            _enemyRenderer = _enemyActor != null ? _enemyActor.GetComponent<SpriteRenderer>() : null;
            _enemyAnimDriver = RegicideActorAnimationDriver.Create(
                _enemyActor != null ? _enemyActor.GetComponent<Animator>() : null,
                actorName: "EnemyActor",
                expectedControllerPath: HeavyBanditControllerPath);

            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                string slotName = $"PlayerSlot_{i}";
                Transform slot = FindChildRecursive(_root, slotName);
                Transform actor = FindChildRecursive(_root, $"PlayerActor_{i}");

                _slotRoots[i] = slot;
                _actorRoots[i] = actor;
                _actorRenderers[i] = actor != null ? actor.GetComponent<SpriteRenderer>() : null;
                _actorAnimDrivers[i] = RegicideActorAnimationDriver.Create(
                    actor != null ? actor.GetComponent<Animator>() : null,
                    actorName: $"PlayerActor_{i}",
                    expectedControllerPath: LightBanditControllerPath);
            }
        }

        private void EnsurePlaceholderVisuals()
        {
            Sprite placeholder = GetOrCreatePlaceholderSprite();
            if (placeholder == null)
            {
                return;
            }

            if (_enemyRenderer != null && _enemyRenderer.sprite == null)
            {
                _enemyRenderer.sprite = placeholder;
            }

            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                SpriteRenderer renderer = _actorRenderers[i];
                if (renderer != null && renderer.sprite == null)
                {
                    renderer.sprite = placeholder;
                }
            }
        }

        private void CaptureIdleTransforms()
        {
            _enemyIdleWorld = _enemyActor != null ? _enemyActor.position : Vector3.zero;
            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                _slotIdleWorld[i] = _slotRoots[i] != null ? _slotRoots[i].position : Vector3.zero;
            }
        }

        private void RebuildPlayerSlots(List<string> orderedPlayers)
        {
            _playerSlotLookup.Clear();
            int activeCount = orderedPlayers != null ? Mathf.Min(MaxPlayerSlots, orderedPlayers.Count) : 0;

            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                bool active = i < activeCount;
                Transform slot = _slotRoots[i];
                if (slot != null)
                {
                    slot.gameObject.SetActive(active);
                }

                Transform actor = _actorRoots[i];
                if (actor != null)
                {
                    actor.gameObject.SetActive(active);
                }

                if (active)
                {
                    string playerId = orderedPlayers[i];
                    if (!string.IsNullOrEmpty(playerId))
                    {
                        _playerSlotLookup[playerId] = i;
                    }

                    SetPlayerIdle(i);
                }
            }
        }

        private void RefreshEnemyVisual(RegicideBattleState state)
        {
            if (_enemyActor == null)
            {
                return;
            }

            RegicideEnemyState enemy = state != null ? state.CurrentEnemy : null;
            bool active = enemy != null && (state == null || !state.IsGameOver);
            _enemyActor.gameObject.SetActive(active || enemy != null);

            if (enemy == null || _enemyRenderer == null)
            {
                _enemyDeathLatched = false;
                _enemyDefeatHoldUntil = 0f;
                return;
            }

            bool holdDefeatPose = _enemyDeathLatched && Time.unscaledTime < _enemyDefeatHoldUntil;
            if (holdDefeatPose && !enemy.Defeated)
            {
                _enemyRenderer.color = BuildEnemyColor(enemy.Suit, defeated: true);
                return;
            }

            if (!enemy.Defeated && _enemyDeathLatched)
            {
                _enemyDeathLatched = false;
                _enemyDefeatHoldUntil = 0f;
                SetEnemyIdle();
            }

            _enemyRenderer.color = BuildEnemyColor(enemy.Suit, enemy.Defeated);
        }

        private void ConfigureBattleAnimators()
        {
            EnsureFacingDirection();
            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                SetPlayerIdle(i);
            }

            if (_enemyActor != null && _enemyActor.gameObject.activeInHierarchy)
            {
                SetEnemyIdle();
            }
        }

        private void EnsureFacingDirection()
        {
            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                Transform actor = _actorRoots[i];
                if (actor == null)
                {
                    continue;
                }

                Vector3 scale = actor.localScale;
                scale.x = -Mathf.Abs(scale.x);
                actor.localScale = scale;
            }

            if (_enemyActor != null)
            {
                Vector3 enemyScale = _enemyActor.localScale;
                enemyScale.x = Mathf.Abs(enemyScale.x);
                _enemyActor.localScale = enemyScale;
            }
        }

        private void SetPlayerIdle(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayerSlots)
            {
                return;
            }

            _actorAnimDrivers[slotIndex]?.SetIdle(CombatIdleAnimState);
        }

        private void SetEnemyIdle()
        {
            if (_enemyDeathLatched)
            {
                return;
            }

            _enemyAnimDriver?.SetIdle(CombatIdleAnimState);
        }

        private void TriggerPlayerAttack(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayerSlots)
            {
                return;
            }

            _actorAnimDrivers[slotIndex]?.TriggerAttack(CombatIdleAnimState);
        }

        private void TriggerPlayerDamage(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MaxPlayerSlots)
            {
                return;
            }

            _actorAnimDrivers[slotIndex]?.TriggerDamage(CombatIdleAnimState);
        }

        private void TriggerEnemyAttack()
        {
            _enemyAnimDriver?.TriggerAttack(CombatIdleAnimState);
        }

        private void TriggerEnemyDamage()
        {
            if (_enemyDeathLatched)
            {
                return;
            }

            _enemyAnimDriver?.TriggerDamage(CombatIdleAnimState);
        }

        private void TriggerEnemyDeath()
        {
            _enemyAnimDriver?.TriggerDeath();
        }

        private bool TryResolveSlot(string playerId, out int slotIndex, out Transform slotTransform)
        {
            slotIndex = -1;
            slotTransform = null;

            if (!string.IsNullOrEmpty(playerId) && _playerSlotLookup.TryGetValue(playerId, out int mapped) && mapped >= 0 && mapped < MaxPlayerSlots)
            {
                slotIndex = mapped;
                slotTransform = _slotRoots[mapped];
            }

            if (slotTransform != null)
            {
                return true;
            }

            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                Transform slot = _slotRoots[i];
                if (slot != null && slot.gameObject.activeInHierarchy)
                {
                    slotIndex = i;
                    slotTransform = slot;
                    return true;
                }
            }

            return false;
        }

        private static List<string> BuildOrderedPlayers(RegicideBattleState state, RegicidePublicStateSnapshotPayload publicSnapshot, string localPlayerId)
        {
            List<string> ordered = new List<string>(MaxPlayerSlots);

            if (publicSnapshot != null && publicSnapshot.Players != null)
            {
                for (int i = 0; i < publicSnapshot.Players.Count; i++)
                {
                    RegicidePublicPlayerState player = publicSnapshot.Players[i];
                    if (player == null || string.IsNullOrEmpty(player.PlayerId) || ordered.Contains(player.PlayerId))
                    {
                        continue;
                    }

                    ordered.Add(player.PlayerId);
                }
            }

            if (state != null && state.Players != null)
            {
                for (int i = 0; i < state.Players.Count; i++)
                {
                    RegicidePlayerState player = state.Players[i];
                    if (player == null || string.IsNullOrEmpty(player.PlayerId) || ordered.Contains(player.PlayerId))
                    {
                        continue;
                    }

                    ordered.Add(player.PlayerId);
                }
            }

            if (!string.IsNullOrEmpty(localPlayerId) && ordered.Contains(localPlayerId))
            {
                ordered.Remove(localPlayerId);
                ordered.Insert(0, localPlayerId);
            }

            return ordered;
        }

        private static Color BuildEnemyColor(RegicideSuit suit, bool defeated)
        {
            if (defeated)
            {
                return new Color(0.58f, 0.58f, 0.58f, 0.88f);
            }

            return Color.white;
        }

        private bool IsBindingsValid()
        {
            return _root != null && _playerRoot != null && _enemyRoot != null && _enemyActor != null;
        }

        private async UniTask<Scene> LoadBattleSceneAsync()
        {
            string[] candidateLocations =
            {
                BattleSceneLocation,
                "Assets/AssetRaw/Scenes/RegicideBattle2DScene.unity",
            };

            Exception lastException = null;
            for (int i = 0; i < candidateLocations.Length; i++)
            {
                string location = candidateLocations[i];
                try
                {
                    Scene scene = await GameModule.Scene.LoadSceneAsync(location, LoadSceneMode.Additive, gcCollect: false);
                    _loadedLocation = location;
                    return scene;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            throw new Exception("Unable to load RegicideBattle2DScene with all fallback locations.", lastException);
        }

        private void ClearBindings()
        {
            _scene = default;
            _root = null;
            _playerRoot = null;
            _enemyRoot = null;
            _enemyActor = null;
            _enemyRenderer = null;
            _enemyAnimDriver = null;
            _playerActionPoint = null;
            _bossAttackPoint = null;
            _enemyHitPoint = null;
            _battleCamera = null;
            _enemyDeathLatched = false;
            _enemyDefeatHoldUntil = 0f;
            _playerSlotLookup.Clear();

            for (int i = 0; i < MaxPlayerSlots; i++)
            {
                _slotRoots[i] = null;
                _actorRoots[i] = null;
                _actorRenderers[i] = null;
                _actorAnimDrivers[i] = null;
                _slotIdleWorld[i] = Vector3.zero;
            }

            _enemyIdleWorld = Vector3.zero;
        }

        private static Transform FindRootByName(Scene scene, string rootName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root != null && string.Equals(root.name, rootName, StringComparison.Ordinal))
                {
                    return root.transform;
                }
            }

            return null;
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (string.Equals(root.name, name, StringComparison.Ordinal))
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                Transform found = FindChildRecursive(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Sprite GetOrCreatePlaceholderSprite()
        {
            if (_placeholderSprite != null)
            {
                return _placeholderSprite;
            }

            _placeholderTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "RegicidePlaceholderTexture",
            };

            Color[] colors = new Color[16];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }

            _placeholderTexture.SetPixels(colors);
            _placeholderTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);

            _placeholderSprite = Sprite.Create(_placeholderTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            _placeholderSprite.name = "RegicidePlaceholderSprite";
            return _placeholderSprite;
        }

        private static void ReleasePlaceholderSprite()
        {
            if (_placeholderSprite != null)
            {
                UnityEngine.Object.Destroy(_placeholderSprite);
                _placeholderSprite = null;
            }

            if (_placeholderTexture != null)
            {
                UnityEngine.Object.Destroy(_placeholderTexture);
                _placeholderTexture = null;
            }
        }

        private static async UniTask TweenWorldPositionAsync(Transform transform, Vector3 target, float duration, DG.Tweening.Ease ease)
        {
            if (transform == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                transform.position = target;
                return;
            }

            bool completed = false;
            Tween tween = DOTween.To(() => transform.position, value => transform.position = value, target, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .OnComplete(() => completed = true)
                .OnKill(() => completed = true);

            while (!completed)
            {
                if (transform == null || tween == null || !tween.active)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private static async UniTask TweenScaleAsync(Transform transform, Vector3 target, float duration, DG.Tweening.Ease ease)
        {
            if (transform == null)
            {
                return;
            }

            if (duration <= 0f)
            {
                transform.localScale = target;
                return;
            }

            bool completed = false;
            Tween tween = DOTween.To(() => transform.localScale, value => transform.localScale = value, target, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .OnComplete(() => completed = true)
                .OnKill(() => completed = true);

            while (!completed)
            {
                if (transform == null || tween == null || !tween.active)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
        }

        private static async UniTask ShakeWorldPositionAsync(Transform transform, float duration, float amplitude)
        {
            if (transform == null)
            {
                return;
            }

            Vector3 origin = transform.position;
            bool completed = false;
            Tween tween = transform.DOShakePosition(duration, new Vector3(amplitude, amplitude * 0.3f, 0f), vibrato: 12, randomness: 70f)
                .SetEase(DG.Tweening.Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => completed = true)
                .OnKill(() => completed = true);

            while (!completed)
            {
                if (transform == null || tween == null || !tween.active)
                {
                    break;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (transform != null)
            {
                transform.position = origin;
            }
        }
    }
}
