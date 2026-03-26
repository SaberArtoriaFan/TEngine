using System.Collections.Generic;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Triggers;

namespace Saber.GAS.Authoring
{
    /// <summary>
    /// 在一轮 ScriptableObject 构建过程中缓存已经创建好的运行时定义，避免重复实例化和循环引用。
    /// </summary>
    public sealed class CombatAuthoringBuildContext
    {
        private readonly Dictionary<AbilityDefinitionAsset, AbilityDefinition> _abilities = new Dictionary<AbilityDefinitionAsset, AbilityDefinition>();
        private readonly Dictionary<EffectDefinitionAsset, EffectDefinition> _effects = new Dictionary<EffectDefinitionAsset, EffectDefinition>();
        private readonly Dictionary<TriggerDefinitionAsset, TriggerDefinition> _triggers = new Dictionary<TriggerDefinitionAsset, TriggerDefinition>();

        /// <summary>
        /// 获取当前上下文里已经构建出的全部技能定义。
        /// </summary>
        public IEnumerable<AbilityDefinition> BuiltAbilities => _abilities.Values;

        /// <summary>
        /// 获取当前上下文里已经构建出的全部效果定义。
        /// </summary>
        public IEnumerable<EffectDefinition> BuiltEffects => _effects.Values;

        /// <summary>
        /// 获取当前上下文里已经构建出的全部 Trigger 定义。
        /// </summary>
        public IEnumerable<TriggerDefinition> BuiltTriggers => _triggers.Values;

        internal bool TryGetAbility(AbilityDefinitionAsset asset, out AbilityDefinition definition)
        {
            return _abilities.TryGetValue(asset, out definition);
        }

        internal bool TryGetEffect(EffectDefinitionAsset asset, out EffectDefinition definition)
        {
            return _effects.TryGetValue(asset, out definition);
        }

        internal bool TryGetTrigger(TriggerDefinitionAsset asset, out TriggerDefinition definition)
        {
            return _triggers.TryGetValue(asset, out definition);
        }

        internal void Cache(AbilityDefinitionAsset asset, AbilityDefinition definition)
        {
            _abilities[asset] = definition;
        }

        internal void Cache(EffectDefinitionAsset asset, EffectDefinition definition)
        {
            _effects[asset] = definition;
        }

        internal void Cache(TriggerDefinitionAsset asset, TriggerDefinition definition)
        {
            _triggers[asset] = definition;
        }

        /// <summary>
        /// 构建或获取一份技能定义。
        /// </summary>
        public AbilityDefinition BuildAbility(AbilityDefinitionAsset asset)
        {
            return asset == null ? null : asset.BuildDefinition(this);
        }

        /// <summary>
        /// 构建或获取一份效果定义。
        /// </summary>
        public EffectDefinition BuildEffect(EffectDefinitionAsset asset)
        {
            return asset == null ? null : asset.BuildDefinition(this);
        }

        /// <summary>
        /// 构建或获取一份 Trigger 定义。
        /// </summary>
        public TriggerDefinition BuildTrigger(TriggerDefinitionAsset asset)
        {
            return asset == null ? null : asset.BuildDefinition(this);
        }
    }
}
