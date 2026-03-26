using System;
using Saber.GAS.Runtime;
using Saber.GAS.RTS.Modules;
using Saber.GAS.RTS.Relations;

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
            TargetingResolver = new DefaultTargetingResolver();
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
                options.TargetingResolver = TargetingResolver;
            }
        }
    }
}
