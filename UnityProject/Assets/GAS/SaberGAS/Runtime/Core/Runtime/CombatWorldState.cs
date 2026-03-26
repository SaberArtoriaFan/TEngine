using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Runtime
{
    /// <summary>
    /// 一份可被快照、回滚、重放的战斗世界状态。
    /// </summary>
    public sealed class CombatWorldState
    {
        /// <summary>
        /// 以 ActorId 为键索引所有运行中单位状态的字典。
        /// </summary>
        private readonly Dictionary<ActorId, CombatActorState> _actors = new Dictionary<ActorId, CombatActorState>();
        /// <summary>
        /// 以 AbilityId 为键索引所有已注册技能定义的字典。
        /// </summary>
        private readonly Dictionary<AbilityId, AbilityDefinition> _abilities = new Dictionary<AbilityId, AbilityDefinition>();
        /// <summary>
        /// 缓存下一个可分配的技能实例 Id。
        /// </summary>
        internal long _nextAbilityInstanceId = 1;

        /// <summary>
        /// 供 Runtime 内部直接按 Id 访问 Actor 状态的字典视图。
        /// </summary>
        internal Dictionary<ActorId, CombatActorState> ActorLookup => _actors;

        /// <summary>
        /// 供 Runtime 内部直接按 Id 访问技能定义的字典视图。
        /// </summary>
        internal Dictionary<AbilityId, AbilityDefinition> AbilityLookup => _abilities;

        /// <summary>
        /// 创建一份空战斗世界状态。
        /// </summary>
        public CombatWorldState()
        {
            WorldTags = new GameplayTagContainer();
            GlobalTriggers = new List<ActiveTriggerInstance>();
            CurrentTick = SimulationTick.Zero;
            Random = new DeterministicRandom(1u);
        }

        /// <summary>
        /// 获取或设置当前世界逻辑 Tick。
        /// </summary>
        public SimulationTick CurrentTick { get; set; }

        /// <summary>
        /// 获取世界级标签集合。
        /// </summary>
        public GameplayTagContainer WorldTags { get; internal set; }

        /// <summary>
        /// 获取或设置当前世界使用的确定性随机数状态。
        /// </summary>
        public DeterministicRandom Random { get; set; }

        /// <summary>
        /// 获取世界级 Trigger 实例列表。
        /// </summary>
        public IList<ActiveTriggerInstance> GlobalTriggers { get; internal set; }

        /// <summary>
        /// 获取当前世界中的全部单位状态。
        /// </summary>
        public IEnumerable<CombatActorState> Actors => _actors.Values;

        /// <summary>
        /// 获取当前世界注册的全部技能定义。
        /// </summary>
        public IEnumerable<AbilityDefinition> Abilities => _abilities.Values;

        /// <summary>
        /// 注册一个运行时 actor 状态。
        /// </summary>
        public void AddActor(CombatActorState actor)
        {
            _actors[actor.ActorId] = actor;
        }

        /// <summary>
        /// 从世界中移除一个 actor。
        /// </summary>
        public bool RemoveActor(ActorId actorId, out CombatActorState actor)
        {
            if (!_actors.TryGetValue(actorId, out actor))
            {
                return false;
            }

            _actors.Remove(actorId);
            return true;
        }

        /// <summary>
        /// 注册一个技能定义。
        /// </summary>
        public void AddAbility(AbilityDefinition ability)
        {
            _abilities[ability.Id] = ability;
        }

        /// <summary>
        /// 查询 actor。
        /// </summary>
        public bool TryGetActor(ActorId actorId, out CombatActorState actor)
        {
            return _actors.TryGetValue(actorId, out actor);
        }

        /// <summary>
        /// 查询技能定义。
        /// </summary>
        public bool TryGetAbility(AbilityId abilityId, out AbilityDefinition ability)
        {
            return _abilities.TryGetValue(abilityId, out ability);
        }

        /// <summary>
        /// 生成新的能力实例 ID。
        /// </summary>
        public AbilityInstanceId CreateAbilityInstanceId()
        {
            var id = new AbilityInstanceId(_nextAbilityInstanceId);
            _nextAbilityInstanceId += 1;
            return id;
        }

        /// <summary>
        /// 清空世界中的所有 actor 引用。
        /// </summary>
        public void ClearActors()
        {
            _actors.Clear();
        }

        /// <summary>
        /// 清空世界级 Trigger 列表。
        /// </summary>
        public void ClearGlobalTriggers()
        {
            GlobalTriggers.Clear();
        }
    }
}
