using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Projectiles;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;
using UnityEngine;

namespace Saber.GAS.Examples.QuickStart
{
    /// <summary>
    /// Projectile QuickStart: two actors periodically fire projectiles at each other.
    /// </summary>
    public sealed class GasQuickStartProjectileDuelRunner : MonoBehaviour
    {
        private static readonly ActorId ActorAId = new ActorId("Actor.Example.Projectile.A");
        private static readonly ActorId ActorBId = new ActorId("Actor.Example.Projectile.B");
        private static readonly TeamId TeamAId = new TeamId("Team.Example.Projectile.A");
        private static readonly TeamId TeamBId = new TeamId("Team.Example.Projectile.B");
        private static readonly AbilityId AbilityId = new AbilityId("Ability.Example.Projectile.Strike");
        private static readonly EffectId EffectId = new EffectId("Effect.Example.Projectile.Strike");
        private static readonly ResourceId HealthId = new ResourceId("Resource.Example.Projectile.Health");
        private static readonly GameplayTag VisualTag = new GameplayTag("projectile.visual.quickstart");

        [Header("Auto Run")]
        [SerializeField] private bool _autoStartOnPlay = true;
        [SerializeField] private bool _restartAfterFinished;

        [Header("Logic Tick")]
        [SerializeField] [Min(0.01f)] private float _secondsPerTick = 0.1f;
        [SerializeField] [Min(1)] private int _ticksBetweenShots = 10;
        [SerializeField] [Min(1)] private int _maxSimulationTicks = 800;

        [Header("Combat")]
        [SerializeField] [Min(1)] private int _startingHealth = 120;
        [SerializeField] [Min(1)] private int _projectileDamage = 12;
        [SerializeField] [Min(0f)] private float _attackRange = 100f;

        [Header("Projectile")]
        [SerializeField] private CombatProjectileTrackingMode _trackingMode = CombatProjectileTrackingMode.TrackActor;
        [SerializeField] [Min(0.01f)] private float _speedPerTick = 0.4f;
        [SerializeField] [Min(0f)] private float _hitRadius = 0.25f;
        [SerializeField] [Min(1)] private int _lifetimeTicks = 120;
        [SerializeField] private float _aimAngleOffsetDegrees;

        [Header("Presentation")]
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private bool _usePrimitiveWhenPrefabMissing = true;
        [SerializeField] [Min(0.05f)] private float _projectileScale = 0.3f;
        [SerializeField] [Min(0.01f)] private float _terminalVisualDuration = 0.2f;
        [SerializeField] private Color _colorA = new Color(0.2f, 0.7f, 1f, 1f);
        [SerializeField] private Color _colorB = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color _hitColor = new Color(1f, 0.95f, 0.2f, 1f);
        [SerializeField] private Color _expireColor = new Color(0.5f, 0.5f, 0.5f, 1f);

        [Header("Actor Anchors (Optional)")]
        [SerializeField] private Transform _actorAAnchor;
        [SerializeField] private Transform _actorBAnchor;
        [SerializeField] private bool _createAnchorsWhenMissing = true;

        [Header("Debug (Read Only)")]
        [SerializeField] private long _debugTick;
        [SerializeField] private int _debugHpA;
        [SerializeField] private int _debugHpB;
        [SerializeField] private bool _debugAliveA;
        [SerializeField] private bool _debugAliveB;
        [SerializeField] private int _debugActiveProjectileViews;
        [SerializeField] private int _debugSpawned;
        [SerializeField] private int _debugHit;
        [SerializeField] private int _debugExpired;

        private sealed class ViewBinding
        {
            public ProjectileInstanceId Id;
            public GameObject Go;
            public Renderer Renderer;
            public Animator Animator;
            public WorldPosition LastPosition;
            public bool Terminal;
            public float CleanupAt;
            public Color BaseColor;
        }

        private readonly Dictionary<ProjectileInstanceId, ViewBinding> _views = new Dictionary<ProjectileInstanceId, ViewBinding>();
        private readonly List<ProjectileInstanceId> _cleanup = new List<ProjectileInstanceId>();

        private CombatWorldState _worldState;
        private CombatRuntime _runtime;
        private float _tickTimer;
        private long _nextShotTick;
        private bool _running;
        private bool _previousRunInBackground;
        private bool _runInBackgroundOverridden;

        private void Start()
        {
            if (Application.isPlaying)
            {
                _previousRunInBackground = Application.runInBackground;
                if (!_previousRunInBackground)
                {
                    // Allow background simulation so MCP/automation can validate this scene reliably.
                    Application.runInBackground = true;
                    _runInBackgroundOverridden = true;
                }
            }

            if (_autoStartOnPlay && Application.isPlaying)
            {
                StartDuel();
            }
        }

        private void Update()
        {
            if (_running && _runtime != null)
            {
                var deltaTime = Time.deltaTime;
                if (deltaTime <= 0f)
                {
                    deltaTime = Time.unscaledDeltaTime;
                }

                if (deltaTime <= 0f)
                {
                    deltaTime = 1f / 60f;
                }

                _tickTimer += deltaTime;
                while (_tickTimer >= _secondsPerTick)
                {
                    _tickTimer -= _secondsPerTick;
                    StepOnceInternal();
                }
            }

            SyncProjectileViews();
            CleanupTerminalViews();
            UpdateDebug();
        }

        private void OnDestroy()
        {
            StopDuel();
            if (_runInBackgroundOverridden && Application.runInBackground)
            {
                Application.runInBackground = _previousRunInBackground;
                _runInBackgroundOverridden = false;
            }
        }

        [ContextMenu("Start Projectile Duel")]
        public void StartDuel()
        {
            StopDuel();
            NormalizeConfig();
            EnsureAnchors();

            _worldState = new CombatWorldState();
            var options = new CombatRuntimeOptions
            {
                EventSink = new UnityEventSink(OnCombatEvent),
                RelationResolver = new TeamResolver(),
            };
            EnsureProjectileModule(options);

            _runtime = new CombatRuntime(_worldState, options);
            _worldState.AddAbility(BuildAbility());
            CreateActor(ActorAId, TeamAId, GetPosition(_actorAAnchor, new Vector3(-3f, 0f, 0f)));
            CreateActor(ActorBId, TeamBId, GetPosition(_actorBAnchor, new Vector3(3f, 0f, 0f)));

            _debugSpawned = 0;
            _debugHit = 0;
            _debugExpired = 0;
            _tickTimer = 0f;
            _nextShotTick = 0;
            _running = true;
            UpdateDebug();
        }

        [ContextMenu("Stop Projectile Duel")]
        public void StopDuel()
        {
            _running = false;
            _tickTimer = 0f;
            _nextShotTick = 0;
            ClearViews();

            if (_runtime != null)
            {
                _runtime.Shutdown();
                _runtime = null;
            }

            _worldState = null;
            UpdateDebug();
        }

        [ContextMenu("Step Once")]
        public void StepOnce()
        {
            if (_running && _runtime != null)
            {
                StepOnceInternal();
            }
        }

        private void StepOnceInternal()
        {
            _runtime.Tick();
            if (_worldState.CurrentTick.Value >= _nextShotTick)
            {
                TryFire(ActorAId, ActorBId);
                TryFire(ActorBId, ActorAId);
                _nextShotTick = _worldState.CurrentTick.Value + _ticksBetweenShots;
            }

            TryMarkDead(ActorAId);
            TryMarkDead(ActorBId);

            if (HasEnded(out _))
            {
                _running = false;
                if (_restartAfterFinished)
                {
                    StartDuel();
                }
            }
        }

        private void NormalizeConfig()
        {
            if (_secondsPerTick <= 0f) _secondsPerTick = 0.1f;
            if (_ticksBetweenShots <= 0) _ticksBetweenShots = 1;
            if (_maxSimulationTicks <= 0) _maxSimulationTicks = 1;
            if (_startingHealth <= 0) _startingHealth = 1;
            if (_projectileDamage <= 0) _projectileDamage = 1;
            if (_speedPerTick <= 0f) _speedPerTick = 0.1f;
            if (_hitRadius < 0f) _hitRadius = 0f;
            if (_lifetimeTicks <= 0) _lifetimeTicks = 1;
        }

        private void EnsureAnchors()
        {
            if (!_createAnchorsWhenMissing)
            {
                return;
            }

            if (_actorAAnchor == null)
            {
                _actorAAnchor = CreateAnchor("ProjectileActorA_Anchor", new Vector3(-3f, 0f, 0f), _colorA);
            }

            if (_actorBAnchor == null)
            {
                _actorBAnchor = CreateAnchor("ProjectileActorB_Anchor", new Vector3(3f, 0f, 0f), _colorB);
            }
        }

        private static Transform CreateAnchor(string name, Vector3 position, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.position = position;
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return go.transform;
        }

        private void EnsureProjectileModule(CombatRuntimeOptions options)
        {
            var hasRule = false;
            var hasResolver = false;
            var registries = CombatAssemblyExtensionRegistryHub.Snapshot();
            for (var i = 0; i < registries.Count; i++)
            {
                var registry = registries[i];
                if (registry == null)
                {
                    continue;
                }

                var rules = registry.RuleModules;
                for (var j = 0; j < rules.Count; j++)
                {
                    if (rules[j] is CombatProjectileModule)
                    {
                        hasRule = true;
                    }
                }

                var resolvers = registry.ImpactResolvers;
                for (var j = 0; j < resolvers.Count; j++)
                {
                    if (resolvers[j] is CombatProjectileModule)
                    {
                        hasResolver = true;
                    }
                }
            }

            if (hasRule && hasResolver)
            {
                return;
            }

            var module = new CombatProjectileModule();
            if (!hasRule)
            {
                options.RuleModules.Add(module);
            }

            if (!hasResolver)
            {
                options.ImpactResolvers.Add(module);
            }
        }

        private AbilityDefinition BuildAbility()
        {
            var ability = new AbilityDefinition(AbilityId)
            {
                Name = "Example.ProjectileStrike",
                ActivationMode = AbilityActivationMode.Instant,
            };
            ability.Targeting.Kind = AbilityTargetKind.Actor;
            ability.Targeting.AllowedFlags = AbilityTargetFlags.Enemy | AbilityTargetFlags.Alive;
            ability.Targeting.MaxRange = _attackRange;

            var projectile = new CombatProjectileSpawnDefinition
            {
                Name = "QuickStartProjectile",
                TrackingMode = _trackingMode,
                SpeedPerTick = _speedPerTick,
                HitRadius = _hitRadius,
                MaxLifetimeTicks = _lifetimeTicks,
            };
            projectile.ImpactTags.Add(VisualTag);
            projectile.ImpactOperations.Add(new CombatImpactOperation
            {
                Type = CombatImpactOperationType.ResourceDelta,
                ResourceId = HealthId,
                Amount = -_projectileDamage,
            });

            var effect = new EffectDefinition(EffectId);
            effect.ImpactOperations.Add(new CombatImpactOperation
            {
                Type = CombatImpactOperationType.SpawnProjectile,
                Projectile = projectile,
            });

            ability.Effects.Add(effect);
            return ability;
        }

        private void CreateActor(ActorId actorId, TeamId teamId, WorldPosition position)
        {
            var actor = _runtime.AddActor(actorId);
            actor.TeamId = teamId;
            actor.Position = position;
            actor.IsAlive = true;
            actor.GrantAbility(AbilityId);

            var health = actor.Resources.GetOrCreate(HealthId, _startingHealth, _startingHealth);
            health.SetCurrent(_startingHealth);
            health.SetMax(_startingHealth);
        }

        private void TryFire(ActorId sourceId, ActorId targetId)
        {
            if (!TryGetActor(sourceId, out var source) ||
                !TryGetActor(targetId, out var target) ||
                !source.IsAlive ||
                !target.IsAlive)
            {
                return;
            }

            var targetData = new AbilityTargetData();
            targetData.TargetActorIds.Add(targetId);
            if (_trackingMode == CombatProjectileTrackingMode.FixedPoint)
            {
                targetData.TargetPoint = BuildFixedPointAim(source.Position, target.Position);
            }

            _runtime.TryActivate(new AbilityActivationRequest
            {
                SourceActorId = sourceId,
                AbilityId = AbilityId,
                TargetData = targetData,
                RequestTick = _worldState.CurrentTick,
            });
        }

        private WorldPosition BuildFixedPointAim(WorldPosition source, WorldPosition target)
        {
            var from = ToUnity(source);
            var to = ToUnity(target);
            var direction = to - from;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector3.right;
            }

            var rotated = Quaternion.AngleAxis(_aimAngleOffsetDegrees, Vector3.up) * direction;
            var result = from + rotated;
            return new WorldPosition(result.x, result.y, result.z);
        }

        private void TryMarkDead(ActorId actorId)
        {
            if (!TryGetActor(actorId, out var actor) || !actor.IsAlive)
            {
                return;
            }

            if (actor.Resources.GetOrCreate(HealthId).Current <= FP._0)
            {
                _runtime.SetActorAliveState(actorId, false);
            }
        }

        private bool HasEnded(out string reason)
        {
            reason = string.Empty;
            if (_worldState == null)
            {
                reason = "World missing.";
                return true;
            }

            if (_worldState.CurrentTick.Value >= _maxSimulationTicks)
            {
                reason = "Reached max ticks.";
                return true;
            }

            if (!TryGetActor(ActorAId, out var a) || !TryGetActor(ActorBId, out var b))
            {
                reason = "Actor missing.";
                return true;
            }

            return !a.IsAlive || !b.IsAlive;
        }

        private void OnCombatEvent(CombatEvent evt)
        {
            if (evt == null || !TryParseProjectileId(evt.Message, out var id))
            {
                return;
            }

            switch (evt.Kind)
            {
                case CombatEventKind.ProjectileSpawned:
                    _debugSpawned += 1;
                    OnProjectileSpawned(id);
                    break;
                case CombatEventKind.ProjectileHit:
                    _debugHit += 1;
                    MarkTerminal(id, true);
                    break;
                case CombatEventKind.ProjectileExpired:
                    _debugExpired += 1;
                    MarkTerminal(id, false);
                    break;
            }
        }

        private void OnProjectileSpawned(ProjectileInstanceId id)
        {
            if (_views.ContainsKey(id))
            {
                return;
            }

            var state = FindProjectile(id);
            if (state == null)
            {
                return;
            }

            var go = CreateProjectileView(state);
            if (go == null)
            {
                return;
            }

            var binding = new ViewBinding
            {
                Id = id,
                Go = go,
                Renderer = go.GetComponentInChildren<Renderer>(),
                Animator = go.GetComponentInChildren<Animator>(),
                LastPosition = state.Position,
                Terminal = false,
                CleanupAt = -1f,
                BaseColor = state.SourceActorId == ActorAId ? _colorA : _colorB,
            };
            if (binding.Animator != null)
            {
                binding.Animator.SetTrigger("Spawn");
            }

            _views[id] = binding;
        }

        private void MarkTerminal(ProjectileInstanceId id, bool hit)
        {
            if (!_views.TryGetValue(id, out var view))
            {
                return;
            }

            if (view.Animator != null)
            {
                view.Animator.SetTrigger(hit ? "Hit" : "Expire");
            }

            if (view.Renderer != null)
            {
                view.Renderer.material.color = hit ? _hitColor : _expireColor;
            }

            view.Terminal = true;
            view.CleanupAt = Time.time + _terminalVisualDuration;
        }

        private void SyncProjectileViews()
        {
            if (_runtime == null || _views.Count == 0)
            {
                return;
            }

            foreach (var pair in _views)
            {
                var view = pair.Value;
                if (view.Go == null)
                {
                    _cleanup.Add(pair.Key);
                    continue;
                }

                if (view.Terminal)
                {
                    continue;
                }

                var state = FindProjectile(pair.Key);
                if (state == null)
                {
                    view.Terminal = true;
                    view.CleanupAt = Time.time + _terminalVisualDuration;
                    continue;
                }

                var position = ToUnity(state.Position);
                var previous = ToUnity(view.LastPosition);
                var direction = position - previous;
                view.Go.transform.position = position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    view.Go.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }

                view.Go.transform.localScale = Vector3.one * (_projectileScale * (1f + Mathf.Sin(Time.time * 8f) * 0.08f));
                view.LastPosition = state.Position;

                if (view.Animator != null)
                {
                    view.Animator.SetFloat("Speed", (float)state.SpeedPerTick);
                }
            }
        }

        private void CleanupTerminalViews()
        {
            if (_views.Count == 0)
            {
                return;
            }

            _cleanup.Clear();
            foreach (var pair in _views)
            {
                var view = pair.Value;
                if (view.Go == null)
                {
                    _cleanup.Add(pair.Key);
                    continue;
                }

                if (view.Terminal && view.CleanupAt > 0f && Time.time >= view.CleanupAt)
                {
                    Destroy(view.Go);
                    _cleanup.Add(pair.Key);
                }
            }

            for (var i = 0; i < _cleanup.Count; i++)
            {
                _views.Remove(_cleanup[i]);
            }
        }

        private void ClearViews()
        {
            foreach (var pair in _views)
            {
                if (pair.Value?.Go != null)
                {
                    Destroy(pair.Value.Go);
                }
            }

            _views.Clear();
            _cleanup.Clear();
        }

        private GameObject CreateProjectileView(CombatProjectileState state)
        {
            GameObject go = null;
            if (_projectilePrefab != null)
            {
                go = Instantiate(_projectilePrefab);
            }
            else if (_usePrimitiveWhenPrefabMissing)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }
            }

            if (go == null)
            {
                return null;
            }

            go.name = $"ProjectileView_{state.InstanceId.Value}";
            go.transform.position = ToUnity(state.Position);
            go.transform.localScale = Vector3.one * _projectileScale;

            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = state.SourceActorId == ActorAId ? _colorA : _colorB;
            }

            return go;
        }

        private CombatProjectileState FindProjectile(ProjectileInstanceId id)
        {
            if (_runtime == null)
            {
                return null;
            }

            foreach (var projectile in _runtime.WorldState.Projectiles)
            {
                if (projectile != null && projectile.InstanceId == id)
                {
                    return projectile;
                }
            }

            return null;
        }

        private bool TryGetActor(ActorId id, out CombatActorState actor)
        {
            actor = null;
            return _worldState != null && _worldState.TryGetActor(id, out actor);
        }

        private void UpdateDebug()
        {
            if (_worldState == null)
            {
                _debugTick = 0;
                _debugHpA = 0;
                _debugHpB = 0;
                _debugAliveA = false;
                _debugAliveB = false;
                _debugActiveProjectileViews = 0;
                return;
            }

            _debugTick = _worldState.CurrentTick.Value;
            _debugActiveProjectileViews = _views.Count;
            if (TryGetActor(ActorAId, out var a))
            {
                _debugHpA = (int)a.Resources.GetOrCreate(HealthId).Current;
                _debugAliveA = a.IsAlive;
            }
            else
            {
                _debugHpA = 0;
                _debugAliveA = false;
            }

            if (TryGetActor(ActorBId, out var b))
            {
                _debugHpB = (int)b.Resources.GetOrCreate(HealthId).Current;
                _debugAliveB = b.IsAlive;
            }
            else
            {
                _debugHpB = 0;
                _debugAliveB = false;
            }
        }

        private static WorldPosition GetPosition(Transform anchor, Vector3 fallback)
        {
            var p = anchor != null ? anchor.position : fallback;
            return new WorldPosition(p.x, p.y, p.z);
        }

        private static bool TryParseProjectileId(string message, out ProjectileInstanceId id)
        {
            if (long.TryParse(message, out var value) && value > 0)
            {
                id = new ProjectileInstanceId(value);
                return true;
            }

            id = ProjectileInstanceId.Empty;
            return false;
        }

        private static Vector3 ToUnity(WorldPosition pos)
        {
            return new Vector3((float)pos.X, (float)pos.Y, (float)pos.Z);
        }

        private sealed class UnityEventSink : ICombatEventSink
        {
            private readonly Action<CombatEvent> _callback;

            public UnityEventSink(Action<CombatEvent> callback)
            {
                _callback = callback;
            }

            public void Publish(CombatEvent combatEvent)
            {
                _callback?.Invoke(combatEvent);
            }
        }

        private sealed class TeamResolver : ICombatActorRelationResolver
        {
            public CombatActorRelationFlags ResolveRelation(CombatWorldState worldState, CombatActorState sourceActor, CombatActorState targetActor)
            {
                if (sourceActor == null || targetActor == null)
                {
                    return CombatActorRelationFlags.None;
                }

                if (sourceActor.ActorId == targetActor.ActorId)
                {
                    return CombatActorRelationFlags.Self;
                }

                if (!sourceActor.TeamId.IsEmpty && !targetActor.TeamId.IsEmpty && sourceActor.TeamId == targetActor.TeamId)
                {
                    return CombatActorRelationFlags.Ally | CombatActorRelationFlags.Other;
                }

                return CombatActorRelationFlags.Enemy | CombatActorRelationFlags.Other;
            }
        }
    }
}
