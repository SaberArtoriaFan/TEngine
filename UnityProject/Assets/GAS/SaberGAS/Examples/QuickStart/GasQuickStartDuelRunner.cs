using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using UnityEngine;

namespace Saber.GAS.Examples.QuickStart
{
    /// <summary>
    /// 最小可运行的 GAS 示例：启动后创建两个 Actor，按固定 Tick 间隔互相释放即时伤害能力。
    /// 用于快速验证 Runtime、Ability、Effect、Targeting 与 Tick 管线是否正常工作。
    /// </summary>
    public sealed class GasQuickStartDuelRunner : MonoBehaviour
    {
        private static readonly ActorId ActorAId = new ActorId("Actor.Example.QuickStart.A");
        private static readonly ActorId ActorBId = new ActorId("Actor.Example.QuickStart.B");
        private static readonly TeamId TeamAId = new TeamId("Team.Example.QuickStart.A");
        private static readonly TeamId TeamBId = new TeamId("Team.Example.QuickStart.B");
        private static readonly AbilityId InstantStrikeAbilityId = new AbilityId("Ability.Example.QuickStart.InstantStrike");
        private static readonly EffectId InstantStrikeEffectId = new EffectId("Effect.Example.QuickStart.InstantStrike.Damage");
        private static readonly ResourceId HealthResourceId = new ResourceId("Resource.Example.QuickStart.Health");

        [Header("自动运行")]
        [SerializeField]
        private bool _autoStartOnPlay = true;
        [SerializeField]
        private bool _restartAfterFinished;

        [Header("Tick 配置")]
        [SerializeField]
        [Min(0.01f)]
        private float _secondsPerTick = 0.2f;
        [SerializeField]
        [Min(1)]
        private int _ticksBetweenAttacks = 8;
        [SerializeField]
        [Min(1)]
        private int _maxSimulationTicks = 400;

        [Header("能力数值")]
        [SerializeField]
        [Min(1)]
        private int _startingHealth = 100;
        [SerializeField]
        [Min(1)]
        private int _instantDamage = 15;
        [SerializeField]
        [Min(0f)]
        private float _attackRange = 100f;

        [Header("场景中的 Actor 锚点（可选）")]
        [SerializeField]
        private Transform _actorAAnchor;
        [SerializeField]
        private Transform _actorBAnchor;
        [SerializeField]
        private bool _createPrimitiveAnchorsWhenMissing = true;

        [Header("运行时调试状态（只读）")]
        [SerializeField]
        private long _debugCurrentTick;
        [SerializeField]
        private int _debugActorAHealth;
        [SerializeField]
        private int _debugActorBHealth;
        [SerializeField]
        private bool _debugActorAAlive;
        [SerializeField]
        private bool _debugActorBAlive;

        private CombatWorldState _worldState;
        private CombatRuntime _runtime;
        private float _tickTimer;
        private long _nextAttackTick;
        private bool _running;

        private void Start()
        {
            if (_autoStartOnPlay && Application.isPlaying)
            {
                StartDuel();
            }
        }

        private void Update()
        {
            if (!_running || _runtime == null)
            {
                return;
            }

            _tickTimer += Time.deltaTime;
            while (_tickTimer >= _secondsPerTick)
            {
                _tickTimer -= _secondsPerTick;
                StepOnceInternal();
            }
        }

        private void OnDestroy()
        {
            StopDuel();
        }

        [ContextMenu("Start Duel")]
        public void StartDuel()
        {
            StopDuel();
            NormalizeConfig();
            EnsureSceneAnchors();

            _worldState = new CombatWorldState();
            var runtimeOptions = new CombatRuntimeOptions
            {
                EventSink = new UnityLogEventSink(OnCombatEvent),
                RelationResolver = new QuickStartRelationResolver(),
            };
            _runtime = new CombatRuntime(_worldState, runtimeOptions);

            var ability = BuildInstantStrikeAbility();
            _worldState.AddAbility(ability);

            var actorAPosition = GetWorldPosition(_actorAAnchor, new Vector3(-2f, 0f, 0f));
            var actorBPosition = GetWorldPosition(_actorBAnchor, new Vector3(2f, 0f, 0f));
            CreateActor(ActorAId, TeamAId, actorAPosition);
            CreateActor(ActorBId, TeamBId, actorBPosition);

            _tickTimer = 0f;
            _nextAttackTick = 0L;
            _running = true;
            UpdateDebugSnapshot();

            Debug.Log("[GAS QuickStart] Duel started.");
            Debug.Log(BuildHealthSnapshot());
        }

        [ContextMenu("Stop Duel")]
        public void StopDuel()
        {
            _running = false;
            _tickTimer = 0f;
            _nextAttackTick = 0L;

            if (_runtime != null)
            {
                _runtime.Shutdown();
                _runtime = null;
            }

            _worldState = null;
            UpdateDebugSnapshot();
        }

        [ContextMenu("Step Once")]
        public void StepOnce()
        {
            if (!_running || _runtime == null)
            {
                Debug.LogWarning("[GAS QuickStart] Duel is not running. Please start duel first.");
                return;
            }

            StepOnceInternal();
        }

        private void StepOnceInternal()
        {
            _runtime.Tick();

            if (_worldState.CurrentTick.Value >= _nextAttackTick)
            {
                // 双方同 Tick 互相出手，便于快速验证命中、扣血、死亡链路。
                TryActivateAttack(ActorAId, ActorBId);
                TryActivateAttack(ActorBId, ActorAId);
                _nextAttackTick = _worldState.CurrentTick.Value + _ticksBetweenAttacks;
                Debug.Log(BuildHealthSnapshot());
            }

            UpdateDebugSnapshot();

            if (HasDuelEnded(out var endReason))
            {
                _running = false;
                Debug.Log("[GAS QuickStart] " + endReason);

                if (_restartAfterFinished)
                {
                    StartDuel();
                }
            }
        }

        private void NormalizeConfig()
        {
            if (_secondsPerTick <= 0f)
            {
                _secondsPerTick = 0.2f;
            }

            if (_ticksBetweenAttacks <= 0)
            {
                _ticksBetweenAttacks = 1;
            }

            if (_maxSimulationTicks <= 0)
            {
                _maxSimulationTicks = 1;
            }

            if (_startingHealth <= 0)
            {
                _startingHealth = 1;
            }

            if (_instantDamage <= 0)
            {
                _instantDamage = 1;
            }

            if (_attackRange < 0f)
            {
                _attackRange = 0f;
            }
        }

        private void EnsureSceneAnchors()
        {
            if (!_createPrimitiveAnchorsWhenMissing)
            {
                return;
            }

            if (_actorAAnchor == null)
            {
                _actorAAnchor = CreateAnchor("ActorA_Anchor", new Vector3(-2f, 0f, 0f), new Color(0.2f, 0.7f, 1f));
            }

            if (_actorBAnchor == null)
            {
                _actorBAnchor = CreateAnchor("ActorB_Anchor", new Vector3(2f, 0f, 0f), new Color(1f, 0.35f, 0.35f));
            }
        }

        private static Transform CreateAnchor(string name, Vector3 position, Color color)
        {
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            primitive.name = name;
            primitive.transform.position = position;

            var renderer = primitive.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
            }

            return primitive.transform;
        }

        private CombatActorState CreateActor(ActorId actorId, TeamId teamId, WorldPosition position)
        {
            var actor = _runtime.AddActor(actorId);
            actor.TeamId = teamId;
            actor.Position = position;
            actor.IsAlive = true;
            actor.GrantAbility(InstantStrikeAbilityId);

            var health = actor.Resources.GetOrCreate(HealthResourceId, _startingHealth, _startingHealth);
            health.SetCurrent(_startingHealth);
            health.SetMax(_startingHealth);
            health.SetRegenPerTick(FP._0);
            return actor;
        }

        private AbilityDefinition BuildInstantStrikeAbility()
        {
            var ability = new AbilityDefinition(InstantStrikeAbilityId)
            {
                Name = "Example.InstantStrike",
                ActivationMode = AbilityActivationMode.Instant,
            };

            ability.Targeting.Kind = AbilityTargetKind.Actor;
            ability.Targeting.AllowedFlags = AbilityTargetFlags.Enemy | AbilityTargetFlags.Alive;
            ability.Targeting.MaxRange = _attackRange;

            var effect = new EffectDefinition(InstantStrikeEffectId);
            effect.InstantResourceDeltas.Add(new ResourceDeltaDefinition(HealthResourceId, -_instantDamage));
            ability.Effects.Add(effect);

            return ability;
        }

        private void TryActivateAttack(ActorId sourceActorId, ActorId targetActorId)
        {
            if (!TryGetActor(sourceActorId, out var sourceActor) ||
                !TryGetActor(targetActorId, out var targetActor) ||
                !sourceActor.IsAlive ||
                !targetActor.IsAlive)
            {
                return;
            }

            var targetData = new AbilityTargetData();
            targetData.TargetActorIds.Add(targetActorId);

            var request = new AbilityActivationRequest
            {
                SourceActorId = sourceActorId,
                AbilityId = InstantStrikeAbilityId,
                TargetData = targetData,
                RequestTick = _worldState.CurrentTick,
            };

            var result = _runtime.TryActivate(request);
            if (!result.Succeeded)
            {
                Debug.LogWarning(
                    $"[GAS QuickStart] {sourceActorId} -> {targetActorId} activate failed: {BuildFailureReason(result)}");
                return;
            }

            TryMarkDeadByHealth(sourceActorId, targetActorId);
        }

        private void TryMarkDeadByHealth(ActorId sourceActorId, ActorId targetActorId)
        {
            if (!TryGetActor(targetActorId, out var targetActor) || !targetActor.IsAlive)
            {
                return;
            }

            var health = targetActor.Resources.GetOrCreate(HealthResourceId);
            if (health.Current > FP._0)
            {
                return;
            }

            _runtime.SetActorAliveState(targetActorId, false, sourceActorId, InstantStrikeAbilityId);
            Debug.Log($"[GAS QuickStart] {targetActorId} has fallen at tick {_worldState.CurrentTick.Value}.");
        }

        private bool HasDuelEnded(out string reason)
        {
            reason = string.Empty;

            if (_worldState == null)
            {
                reason = "World state is missing.";
                return true;
            }

            var tick = _worldState.CurrentTick.Value;
            if (tick >= _maxSimulationTicks)
            {
                reason = $"Reached max simulation tick: {_maxSimulationTicks}.";
                return true;
            }

            var hasA = TryGetActor(ActorAId, out var actorA);
            var hasB = TryGetActor(ActorBId, out var actorB);
            if (!hasA || !hasB)
            {
                reason = "Actor state missing.";
                return true;
            }

            if (!actorA.IsAlive && !actorB.IsAlive)
            {
                reason = "Duel ended in a draw.";
                return true;
            }

            if (!actorA.IsAlive)
            {
                reason = "Actor B wins.";
                return true;
            }

            if (!actorB.IsAlive)
            {
                reason = "Actor A wins.";
                return true;
            }

            return false;
        }

        private bool TryGetActor(ActorId actorId, out CombatActorState actor)
        {
            actor = null;
            return _worldState != null && _worldState.TryGetActor(actorId, out actor);
        }

        private void UpdateDebugSnapshot()
        {
            if (_worldState == null)
            {
                _debugCurrentTick = 0L;
                _debugActorAHealth = 0;
                _debugActorBHealth = 0;
                _debugActorAAlive = false;
                _debugActorBAlive = false;
                return;
            }

            _debugCurrentTick = _worldState.CurrentTick.Value;

            if (TryGetActor(ActorAId, out var actorA))
            {
                _debugActorAHealth = (int)actorA.Resources.GetOrCreate(HealthResourceId).Current;
                _debugActorAAlive = actorA.IsAlive;
            }
            else
            {
                _debugActorAHealth = 0;
                _debugActorAAlive = false;
            }

            if (TryGetActor(ActorBId, out var actorB))
            {
                _debugActorBHealth = (int)actorB.Resources.GetOrCreate(HealthResourceId).Current;
                _debugActorBAlive = actorB.IsAlive;
            }
            else
            {
                _debugActorBHealth = 0;
                _debugActorBAlive = false;
            }
        }

        private string BuildHealthSnapshot()
        {
            if (!TryGetActor(ActorAId, out var actorA) || !TryGetActor(ActorBId, out var actorB))
            {
                return "[GAS QuickStart] Actor snapshot unavailable.";
            }

            var actorAHealth = actorA.Resources.GetOrCreate(HealthResourceId);
            var actorBHealth = actorB.Resources.GetOrCreate(HealthResourceId);

            return
                $"[GAS QuickStart] Tick={_worldState.CurrentTick.Value} | " +
                $"A HP={actorAHealth.Current}/{actorAHealth.Max} Alive={actorA.IsAlive} | " +
                $"B HP={actorBHealth.Current}/{actorBHealth.Max} Alive={actorB.IsAlive}";
        }

        private void OnCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent == null)
            {
                return;
            }

            if (combatEvent.Kind == CombatEventKind.AbilityBlocked)
            {
                Debug.LogWarning(
                    $"[GAS QuickStart][Event] {combatEvent.Kind} Actor={combatEvent.ActorId} Msg={combatEvent.Message}");
            }
        }

        private static string BuildFailureReason(AbilityActivationResult result)
        {
            if (result == null)
            {
                return "Unknown reason.";
            }

            if (!string.IsNullOrWhiteSpace(result.FailureReason))
            {
                return result.FailureReason;
            }

            if (result.Blocks != null && result.Blocks.Count > 0)
            {
                return result.Blocks[0].Message;
            }

            return "No failure detail provided.";
        }

        private static WorldPosition GetWorldPosition(Transform anchor, Vector3 fallback)
        {
            var source = anchor != null ? anchor.position : fallback;
            return new WorldPosition(source.x, source.y, source.z);
        }

        private sealed class UnityLogEventSink : ICombatEventSink
        {
            private readonly System.Action<CombatEvent> _onEvent;

            public UnityLogEventSink(System.Action<CombatEvent> onEvent)
            {
                _onEvent = onEvent;
            }

            public void Publish(CombatEvent combatEvent)
            {
                _onEvent?.Invoke(combatEvent);
            }
        }

        private sealed class QuickStartRelationResolver : ICombatActorRelationResolver
        {
            public CombatActorRelationFlags ResolveRelation(
                CombatWorldState worldState,
                CombatActorState sourceActor,
                CombatActorState targetActor)
            {
                if (sourceActor == null || targetActor == null)
                {
                    return CombatActorRelationFlags.None;
                }

                if (sourceActor.ActorId == targetActor.ActorId)
                {
                    return CombatActorRelationFlags.Self;
                }

                if (!sourceActor.TeamId.IsEmpty &&
                    !targetActor.TeamId.IsEmpty &&
                    sourceActor.TeamId == targetActor.TeamId)
                {
                    return CombatActorRelationFlags.Ally | CombatActorRelationFlags.Other;
                }

                return CombatActorRelationFlags.Enemy | CombatActorRelationFlags.Other;
            }
        }
    }
}
