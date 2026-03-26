using Saber.GAS.Tags;

namespace Saber.GAS.Semantics
{
    /// <summary>
    /// 标准战斗语义标签目录。
    /// 业务层只依赖这份目录和语义模块，不需要再到 Runtime Core 里硬写语义判断。
    /// </summary>
    public sealed class CombatSemanticCatalog
    {
        /// <summary>
        /// 创建一份标准语义标签目录。
        /// </summary>
        public CombatSemanticCatalog()
        {
            Ability = new AbilityTagGroup();
            Effect = new EffectTagGroup();
            Impact = new ImpactTagGroup();
            State = new StateTagGroup();
        }

        /// <summary>
        /// 获取技能语义标签分组。
        /// </summary>
        public AbilityTagGroup Ability { get; }

        /// <summary>
        /// 获取效果语义标签分组。
        /// </summary>
        public EffectTagGroup Effect { get; }

        /// <summary>
        /// 获取 Impact 语义标签分组。
        /// </summary>
        public ImpactTagGroup Impact { get; }

        /// <summary>
        /// 获取状态语义标签分组。
        /// </summary>
        public StateTagGroup State { get; }

        /// <summary>
        /// 按控制类型返回对应的状态 Tag。
        /// </summary>
        public GameplayTag GetControlStateTag(ControlSemanticKind controlKind)
        {
            switch (controlKind)
            {
                case ControlSemanticKind.Stun:
                    return State.ControlStun;
                case ControlSemanticKind.Silence:
                    return State.ControlSilence;
                case ControlSemanticKind.Root:
                    return State.ControlRoot;
                case ControlSemanticKind.Disarm:
                    return State.ControlDisarm;
                case ControlSemanticKind.Taunt:
                    return State.ControlTaunt;
                case ControlSemanticKind.Fear:
                    return State.ControlFear;
                case ControlSemanticKind.Charm:
                    return State.ControlCharm;
                case ControlSemanticKind.Suppression:
                    return State.ControlSuppression;
                default:
                    return State.ControlAny;
            }
        }

        /// <summary>
        /// 按免疫类型返回对应的状态 Tag。
        /// </summary>
        public GameplayTag GetImmunityStateTag(ImmunitySemanticKind immunityKind)
        {
            switch (immunityKind)
            {
                case ImmunitySemanticKind.Damage:
                    return State.ImmunityDamage;
                case ImmunitySemanticKind.Control:
                    return State.ImmunityControl;
                case ImmunitySemanticKind.Dispel:
                    return State.ImmunityDispel;
                case ImmunitySemanticKind.All:
                    return State.ImmunityAll;
                default:
                    return State.ImmunityAll;
            }
        }

        public sealed class AbilityTagGroup
        {
            /// <summary>
            /// 表示施法型技能的语义标签。
            /// </summary>
            public GameplayTag Cast { get; } = new GameplayTag("Ability.Semantic.Cast");

            /// <summary>
            /// 表示攻击型技能的语义标签。
            /// </summary>
            public GameplayTag Attack { get; } = new GameplayTag("Ability.Semantic.Attack");
        }

        public sealed class EffectTagGroup
        {
            /// <summary>
            /// 表示伤害效果的语义标签。
            /// </summary>
            public GameplayTag Damage { get; } = new GameplayTag("Effect.Semantic.Damage");

            /// <summary>
            /// 表示治疗效果的语义标签。
            /// </summary>
            public GameplayTag Heal { get; } = new GameplayTag("Effect.Semantic.Heal");

            /// <summary>
            /// 表示护盾效果的语义标签。
            /// </summary>
            public GameplayTag Shield { get; } = new GameplayTag("Effect.Semantic.Shield");

            /// <summary>
            /// 表示控制效果的语义标签。
            /// </summary>
            public GameplayTag Control { get; } = new GameplayTag("Effect.Semantic.Control");

            /// <summary>
            /// 表示驱散效果的语义标签。
            /// </summary>
            public GameplayTag Dispel { get; } = new GameplayTag("Effect.Semantic.Dispel");

            /// <summary>
            /// 表示免疫效果的语义标签。
            /// </summary>
            public GameplayTag Immunity { get; } = new GameplayTag("Effect.Semantic.Immunity");

            /// <summary>
            /// 表示正面效果的语义标签。
            /// </summary>
            public GameplayTag Buff { get; } = new GameplayTag("Effect.Semantic.Buff");

            /// <summary>
            /// 表示负面效果的语义标签。
            /// </summary>
            public GameplayTag Debuff { get; } = new GameplayTag("Effect.Semantic.Debuff");

            /// <summary>
            /// 表示周期效果的语义标签。
            /// </summary>
            public GameplayTag Periodic { get; } = new GameplayTag("Effect.Semantic.Periodic");

            /// <summary>
            /// 表示纯粹伤害的语义标签。
            /// </summary>
            public GameplayTag PureDamage { get; } = new GameplayTag("Effect.Semantic.PureDamage");

            /// <summary>
            /// 表示反弹伤害的语义标签。
            /// </summary>
            public GameplayTag ReflectDamage { get; } = new GameplayTag("Effect.Semantic.ReflectDamage");
        }

        public sealed class ImpactTagGroup
        {
            /// <summary>
            /// 表示伤害结算的 Impact 标签。
            /// </summary>
            public GameplayTag Damage { get; } = new GameplayTag("Impact.Damage");

            /// <summary>
            /// 表示治疗结算的 Impact 标签。
            /// </summary>
            public GameplayTag Heal { get; } = new GameplayTag("Impact.Heal");

            /// <summary>
            /// 表示被护盾吸收过的 Impact 标签。
            /// </summary>
            public GameplayTag ShieldAbsorbed { get; } = new GameplayTag("Impact.ShieldAbsorbed");

            /// <summary>
            /// 表示被免疫拦截的 Impact 标签。
            /// </summary>
            public GameplayTag PreventedByImmunity { get; } = new GameplayTag("Impact.PreventedByImmunity");
        }

        public sealed class StateTagGroup
        {
            /// <summary>
            /// 表示正面状态的语义标签。
            /// </summary>
            public GameplayTag Buff { get; } = new GameplayTag("State.Semantic.Buff");

            /// <summary>
            /// 表示负面状态的语义标签。
            /// </summary>
            public GameplayTag Debuff { get; } = new GameplayTag("State.Semantic.Debuff");

            /// <summary>
            /// 表示可驱散正面状态的语义标签。
            /// </summary>
            public GameplayTag DispellablePositive { get; } = new GameplayTag("State.Semantic.Dispellable.Positive");

            /// <summary>
            /// 表示可驱散负面状态的语义标签。
            /// </summary>
            public GameplayTag DispellableNegative { get; } = new GameplayTag("State.Semantic.Dispellable.Negative");

            /// <summary>
            /// 表示护盾激活中的语义标签。
            /// </summary>
            public GameplayTag ShieldActive { get; } = new GameplayTag("State.Semantic.Shield.Active");

            /// <summary>
            /// 表示任意控制状态的聚合标签。
            /// </summary>
            public GameplayTag ControlAny { get; } = new GameplayTag("State.Semantic.Control.Any");

            /// <summary>
            /// 表示眩晕状态的语义标签。
            /// </summary>
            public GameplayTag ControlStun { get; } = new GameplayTag("State.Semantic.Control.Stun");

            /// <summary>
            /// 表示沉默状态的语义标签。
            /// </summary>
            public GameplayTag ControlSilence { get; } = new GameplayTag("State.Semantic.Control.Silence");

            /// <summary>
            /// 表示定身状态的语义标签。
            /// </summary>
            public GameplayTag ControlRoot { get; } = new GameplayTag("State.Semantic.Control.Root");

            /// <summary>
            /// 表示缴械状态的语义标签。
            /// </summary>
            public GameplayTag ControlDisarm { get; } = new GameplayTag("State.Semantic.Control.Disarm");

            /// <summary>
            /// 表示嘲讽状态的语义标签。
            /// </summary>
            public GameplayTag ControlTaunt { get; } = new GameplayTag("State.Semantic.Control.Taunt");

            /// <summary>
            /// 表示恐惧状态的语义标签。
            /// </summary>
            public GameplayTag ControlFear { get; } = new GameplayTag("State.Semantic.Control.Fear");

            /// <summary>
            /// 表示魅惑状态的语义标签。
            /// </summary>
            public GameplayTag ControlCharm { get; } = new GameplayTag("State.Semantic.Control.Charm");

            /// <summary>
            /// 表示压制状态的语义标签。
            /// </summary>
            public GameplayTag ControlSuppression { get; } = new GameplayTag("State.Semantic.Control.Suppression");

            /// <summary>
            /// 表示伤害免疫状态的语义标签。
            /// </summary>
            public GameplayTag ImmunityDamage { get; } = new GameplayTag("State.Semantic.Immunity.Damage");

            /// <summary>
            /// 表示控制免疫状态的语义标签。
            /// </summary>
            public GameplayTag ImmunityControl { get; } = new GameplayTag("State.Semantic.Immunity.Control");

            /// <summary>
            /// 表示驱散免疫状态的语义标签。
            /// </summary>
            public GameplayTag ImmunityDispel { get; } = new GameplayTag("State.Semantic.Immunity.Dispel");

            /// <summary>
            /// 表示全免疫状态的语义标签。
            /// </summary>
            public GameplayTag ImmunityAll { get; } = new GameplayTag("State.Semantic.Immunity.All");
        }
    }
}
