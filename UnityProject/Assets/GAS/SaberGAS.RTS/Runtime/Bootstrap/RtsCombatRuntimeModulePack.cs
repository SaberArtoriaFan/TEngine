using System;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.CustomActions;
using Saber.GAS.RTS.Modules;
using Saber.GAS.RTS.Operations;
using Saber.GAS.RTS.Relations;
using Saber.GAS.RTS.Spatial;
using Saber.GAS.Triggers;

namespace Saber.GAS.RTS.Bootstrap
{
    /// <summary>
    /// RTS 侧的 CombatRuntime 装配包。
    /// 它负责把 RTS 默认关系解析器和后续 RTS 模块依赖整理到一处。
    /// </summary>
    public sealed class RtsCombatRuntimeModulePack
    {
        /// <summary>
        /// 创建一份带默认 RTS 关系实现的装配包。
        /// </summary>
        public RtsCombatRuntimeModulePack()
        {
            RelationResolver = new RtsTeamRelationResolver();
            ModeRules = new DefaultCombatModeRules();
            SpatialQueryService = new RtsOctreeSpatialQueryService();
            ReflectDamageCustomTriggerActionRegistry = new RtsReflectDamageCustomTriggerActionRegistry();
            LifeStealImpactOperationHandler = new RtsLifeStealImpactOperationHandler();
        }

        /// <summary>
        /// 获取或设置 RTS 默认关系解析器。
        /// </summary>
        public ICombatActorRelationResolver RelationResolver { get; set; }

        /// <summary>
        /// 获取或设置要使用的战斗模式规则。
        /// </summary>
        public ICombatModeRules ModeRules { get; set; }

        /// <summary>
        /// 获取或设置要使用的目标解析器。
        /// </summary>
        public ITargetingResolver TargetingResolver { get; set; }

        /// <summary>
        /// 获取或设置 RTS 空间查询服务。
        /// </summary>
        public IRtsSpatialQueryService SpatialQueryService { get; set; }

        /// <summary>
        /// 获取或设置 RTS 可见性服务。
        /// </summary>
        public IRtsVisibilityService VisibilityService { get; set; }

        /// <summary>
        /// 获取或设置 RTS 指令校验服务。
        /// </summary>
        public IRtsOrderRuleService OrderRuleService { get; set; }

        /// <summary>
        /// 获取或设置“受伤反给最远敌人”动作的回退注册表。
        /// 当源码生成注册表不可用时，会自动注入该注册表以保证动作可执行。
        /// </summary>
        public ICombatCustomTriggerActionRegistry ReflectDamageCustomTriggerActionRegistry { get; set; }

        /// <summary>
        /// 获取或设置 RTS 示例吸血 Operation 的处理器。
        /// </summary>
        public IImpactOperationHandler LifeStealImpactOperationHandler { get; set; }

        /// <summary>
        /// 创建一份已经套用 RTS 默认装配的 RuntimeOptions。
        /// </summary>
        public CombatRuntimeOptions CreateRuntimeOptions()
        {
            var options = new CombatRuntimeOptions();
            ApplyTo(options);
            return options;
        }

        /// <summary>
        /// 将 RTS 默认装配写入指定的 RuntimeOptions。
        /// 未显式配置的项会使用装配包中的默认值。
        /// </summary>
        public void ApplyTo(CombatRuntimeOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.RelationResolver == null)
            {
                options.RelationResolver = RelationResolver;
            }

            if (options.ModeRules == null)
            {
                options.ModeRules = ModeRules;
            }

            if (options.TargetingResolver == null)
            {
                options.TargetingResolver = TargetingResolver ?? CreateDefaultTargetingResolver(options.RelationResolver);
            }

            InjectReflectDamageCustomActionRegistry(options);
            InjectImpactOperationHandler(options);
        }

        private ITargetingResolver CreateDefaultTargetingResolver(ICombatActorRelationResolver relationResolver)
        {
            if (SpatialQueryService == null)
            {
                return new DefaultTargetingResolver();
            }

            return new RtsSpatialTargetingResolver(
                SpatialQueryService,
                relationResolver ?? new RtsTeamRelationResolver(),
                new DefaultTargetingResolver());
        }

        private void InjectReflectDamageCustomActionRegistry(CombatRuntimeOptions options)
        {
            if (ReflectDamageCustomTriggerActionRegistry == null)
            {
                return;
            }

            if (HasCustomActionId(options.CustomTriggerActionRegistries, RtsReflectDamageToFarthestEnemyTriggerAction.ActionId))
            {
                return;
            }

            if (HasCustomActionId(CombatCustomTriggerActionRegistryHub.Snapshot(), RtsReflectDamageToFarthestEnemyTriggerAction.ActionId))
            {
                return;
            }

            options.CustomTriggerActionRegistries.Add(ReflectDamageCustomTriggerActionRegistry);
        }

        private static bool HasCustomActionId(
            System.Collections.Generic.IEnumerable<ICombatCustomTriggerActionRegistry> registries,
            int customActionId)
        {
            if (registries == null || customActionId <= 0)
            {
                return false;
            }

            foreach (var registry in registries)
            {
                if (registry == null || registry.RegisteredCustomActionIds == null)
                {
                    continue;
                }

                for (var i = 0; i < registry.RegisteredCustomActionIds.Count; i++)
                {
                    if (registry.RegisteredCustomActionIds[i] == customActionId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void InjectImpactOperationHandler(CombatRuntimeOptions options)
        {
            if (options == null || LifeStealImpactOperationHandler == null)
            {
                return;
            }

            for (var i = 0; i < options.ImpactOperationHandlers.Count; i++)
            {
                var existing = options.ImpactOperationHandlers[i];
                if (existing == null)
                {
                    continue;
                }

                if (ReferenceEquals(existing, LifeStealImpactOperationHandler) ||
                    existing.GetType() == LifeStealImpactOperationHandler.GetType())
                {
                    return;
                }
            }

            options.ImpactOperationHandlers.Add(LifeStealImpactOperationHandler);
        }
    }
}
