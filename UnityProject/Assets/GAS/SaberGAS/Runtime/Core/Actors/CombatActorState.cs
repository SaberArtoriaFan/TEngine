using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Attributes;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Pooling;
using Saber.GAS.Resources;
using Saber.GAS.Tags;
using Saber.GAS.Triggers;

namespace Saber.GAS.Actors
{
    /// <summary>
    /// 运行时战斗单位状态，承载属性、资源、能力、效果和标签引用计数。
    /// </summary>
    public sealed class CombatActorState : ICombatPoolable
    {
        /// <summary>
        /// 缓存每个技能的冷却结束 Tick。
        /// </summary>
        private readonly Dictionary<AbilityId, SimulationTick> _cooldownEndTicks = new Dictionary<AbilityId, SimulationTick>();
        /// <summary>
        /// 缓存运行时标签的引用计数。
        /// </summary>
        private readonly Dictionary<GameplayTag, int> _tagReferenceCounts = new Dictionary<GameplayTag, int>();

        /// <summary>
        /// 创建一个可池化的战斗单位状态，并初始化内部容器。
        /// </summary>
        internal CombatActorState()
        {
            Tags = new GameplayTagContainer();
            Attributes = new AttributeSet();
            Resources = new ResourceSet();
            GrantedAbilities = new List<AbilityId>();
            ActiveEffects = new List<ActiveEffect>();
            ActiveAbilityInstances = new List<ActiveAbilityInstance>();
            ActiveTriggers = new List<ActiveTriggerInstance>();
            ResetForPool();
        }

        /// <summary>
        /// 使用单位标识创建一份战斗单位状态。
        /// </summary>
        public CombatActorState(ActorId actorId)
            : this()
        {
            Initialize(actorId);
        }

        /// <summary>
        /// 用新的 ActorId 初始化单位状态。
        /// </summary>
        internal void Initialize(ActorId actorId)
        {
            ResetForPool();
            ActorId = actorId;
            IsAlive = true;
        }

        /// <summary>
        /// 获取内部冷却结束 Tick 索引。
        /// </summary>
        internal Dictionary<AbilityId, SimulationTick> CooldownEndTicks => _cooldownEndTicks;

        /// <summary>
        /// 获取内部标签引用计数字典。
        /// </summary>
        internal Dictionary<GameplayTag, int> TagReferenceCounts => _tagReferenceCounts;

        /// <summary>
        /// 获取当前单位 Id。
        /// </summary>
        public ActorId ActorId { get; internal set; }

        /// <summary>
        /// 获取或设置当前单位所属队伍。
        /// </summary>
        public TeamId TeamId { get; set; }

        /// <summary>
        /// 获取或设置当前单位位置。
        /// </summary>
        public WorldPosition Position { get; set; }

        /// <summary>
        /// 获取或设置当前单位是否存活。
        /// </summary>
        public bool IsAlive { get; set; }

        /// <summary>
        /// 获取当前单位持有的运行时标签集合。
        /// </summary>
        public GameplayTagContainer Tags { get; internal set; }

        /// <summary>
        /// 获取当前单位的属性集合。
        /// </summary>
        public AttributeSet Attributes { get; internal set; }

        /// <summary>
        /// 获取当前单位的资源集合。
        /// </summary>
        public ResourceSet Resources { get; internal set; }

        /// <summary>
        /// 获取当前单位拥有的技能 Id 列表。
        /// </summary>
        public IList<AbilityId> GrantedAbilities { get; internal set; }

        /// <summary>
        /// 获取当前单位身上的持续效果列表。
        /// </summary>
        public IList<ActiveEffect> ActiveEffects { get; internal set; }

        /// <summary>
        /// 获取当前单位的运行中技能实例列表。
        /// </summary>
        public IList<ActiveAbilityInstance> ActiveAbilityInstances { get; internal set; }

        /// <summary>
        /// 获取当前单位本体持有的运行时 Trigger 列表。
        /// </summary>
        public IList<ActiveTriggerInstance> ActiveTriggers { get; internal set; }

        /// <summary>
        /// 向单位授予一个能力。
        /// </summary>
        public void GrantAbility(AbilityId abilityId)
        {
            if (HasAbility(abilityId))
            {
                return;
            }

            GrantedAbilities.Add(abilityId);
        }

        /// <summary>
        /// 撤销单位上的一个能力。
        /// </summary>
        public bool RevokeAbility(AbilityId abilityId)
        {
            return GrantedAbilities.Remove(abilityId);
        }

        /// <summary>
        /// 检查单位是否拥有指定能力。
        /// </summary>
        public bool HasAbility(AbilityId abilityId)
        {
            for (var i = 0; i < GrantedAbilities.Count; i++)
            {
                if (GrantedAbilities[i] == abilityId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 检查能力是否仍处于冷却中。
        /// </summary>
        public bool IsAbilityOnCooldown(AbilityId abilityId, SimulationTick currentTick)
        {
            SimulationTick cooldownEndTick;
            if (!_cooldownEndTicks.TryGetValue(abilityId, out cooldownEndTick))
            {
                return false;
            }

            return currentTick < cooldownEndTick;
        }

        /// <summary>
        /// 设置能力冷却结束 Tick。
        /// </summary>
        public void SetCooldown(AbilityId abilityId, SimulationTick endTick)
        {
            _cooldownEndTicks[abilityId] = endTick;
        }

        /// <summary>
        /// 查询能力冷却结束 Tick。
        /// </summary>
        public bool TryGetCooldownEndTick(AbilityId abilityId, out SimulationTick endTick)
        {
            return _cooldownEndTicks.TryGetValue(abilityId, out endTick);
        }

        /// <summary>
        /// 为标签增加引用计数；第一次出现时会真正挂到 Tags 容器上。
        /// </summary>
        public void AddTagReference(GameplayTag tag)
        {
            int count;
            if (_tagReferenceCounts.TryGetValue(tag, out count))
            {
                _tagReferenceCounts[tag] = count + 1;
                return;
            }

            _tagReferenceCounts[tag] = 1;
            Tags.Add(tag);
        }

        /// <summary>
        /// 为标签减少引用计数；计数归零后会从 Tags 容器上移除。
        /// </summary>
        public void RemoveTagReference(GameplayTag tag)
        {
            int count;
            if (!_tagReferenceCounts.TryGetValue(tag, out count))
            {
                Tags.Remove(tag);
                return;
            }

            count -= 1;
            if (count <= 0)
            {
                _tagReferenceCounts.Remove(tag);
                Tags.Remove(tag);
                return;
            }

            _tagReferenceCounts[tag] = count;
        }

        /// <summary>
        /// 回收到对象池前重置自身状态。
        /// </summary>
        public void ResetForPool()
        {
            ActorId = ActorId.Empty;
            TeamId = TeamId.Empty;
            Position = WorldPosition.Zero;
            IsAlive = true;
            Tags.Clear();
            Attributes.Reset();
            Resources.Reset();
            GrantedAbilities.Clear();
            ActiveEffects.Clear();
            ActiveAbilityInstances.Clear();
            ActiveTriggers.Clear();
            _cooldownEndTicks.Clear();
            _tagReferenceCounts.Clear();
        }
    }
}
