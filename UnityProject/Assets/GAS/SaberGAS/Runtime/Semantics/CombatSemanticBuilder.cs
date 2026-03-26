using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Tags;

namespace Saber.GAS.Semantics
{
    /// <summary>
    /// 标准战斗语义定义构造器。
    /// 负责把常见机制翻译成效果标签、状态标签和附加扩展数据。
    /// </summary>
    public sealed class CombatSemanticBuilder
    {
        /// <summary>
        /// 使用指定目录创建语义构造器。
        /// </summary>
        public CombatSemanticBuilder(CombatSemanticCatalog catalog = null)
        {
            Catalog = catalog ?? new CombatSemanticCatalog();
        }

        /// <summary>
        /// 获取当前构造器依赖的语义标签目录。
        /// </summary>
        public CombatSemanticCatalog Catalog { get; }

        /// <summary>
        /// 把一个技能标记为施法型技能。
        /// </summary>
        public void MarkAsCastAbility(AbilityDefinition ability)
        {
            if (ability == null)
            {
                return;
            }

            ability.AbilityTags.Add(Catalog.Ability.Cast);
        }

        /// <summary>
        /// 把一个技能标记为攻击型技能。
        /// </summary>
        public void MarkAsAttackAbility(AbilityDefinition ability)
        {
            if (ability == null)
            {
                return;
            }

            ability.AbilityTags.Add(Catalog.Ability.Attack);
        }

        /// <summary>
        /// 构造一个瞬时伤害效果定义。
        /// </summary>
        public EffectDefinition CreateDamageEffect(
            EffectId effectId,
            ResourceId resourceId,
            FP magnitude,
            DamageSemanticKind damageKind = DamageSemanticKind.Standard)
        {
            var effect = new EffectDefinition(effectId);
            effect.EffectTags.Add(Catalog.Effect.Damage);
            effect.EffectTags.Add(Catalog.Impact.Damage);
            effect.InstantResourceDeltas.Add(new ResourceDeltaDefinition(resourceId, -FPMath.Abs(magnitude)));
            AddDamageKindTags(effect, damageKind);
            return effect;
        }

        /// <summary>
        /// 构造一个带周期 Tick 的持续伤害效果定义。
        /// </summary>
        public EffectDefinition CreatePeriodicDamageEffect(
            EffectId effectId,
            ResourceId resourceId,
            FP magnitudePerTick,
            long periodTicks,
            long durationTicks,
            DamageSemanticKind damageKind = DamageSemanticKind.Periodic)
        {
            var effect = new EffectDefinition(effectId)
            {
                DurationPolicy = EffectDurationPolicy.Timed,
                DurationTicks = durationTicks,
                PeriodTicks = periodTicks,
            };
            effect.EffectTags.Add(Catalog.Effect.Damage);
            effect.EffectTags.Add(Catalog.Effect.Periodic);
            effect.EffectTags.Add(Catalog.Impact.Damage);
            effect.PeriodicResourceDeltas.Add(new ResourceDeltaDefinition(resourceId, -FPMath.Abs(magnitudePerTick)));
            AddDamageKindTags(effect, damageKind);
            return effect;
        }

        /// <summary>
        /// 构造一个瞬时治疗效果定义。
        /// </summary>
        public EffectDefinition CreateHealEffect(
            EffectId effectId,
            ResourceId resourceId,
            FP magnitude,
            HealSemanticKind healKind = HealSemanticKind.Standard)
        {
            var effect = new EffectDefinition(effectId);
            effect.EffectTags.Add(Catalog.Effect.Heal);
            effect.EffectTags.Add(Catalog.Impact.Heal);
            effect.InstantResourceDeltas.Add(new ResourceDeltaDefinition(resourceId, FPMath.Abs(magnitude)));
            AddHealKindTags(effect, healKind);
            return effect;
        }

        /// <summary>
        /// 构造一个标准护盾效果定义。
        /// </summary>
        public EffectDefinition CreateShieldEffect(
            EffectId effectId,
            ResourceId protectedResourceId,
            FP capacity,
            long durationTicks,
            GameplayTag requiredImpactTag = default(GameplayTag))
        {
            var effect = new EffectDefinition(effectId)
            {
                DurationPolicy = durationTicks > 0 ? EffectDurationPolicy.Timed : EffectDurationPolicy.Infinite,
                DurationTicks = durationTicks,
            };

            effect.EffectTags.Add(Catalog.Effect.Shield);
            effect.GrantedTags.Add(Catalog.State.ShieldActive);
            AttachShieldDefinition(effect, new ShieldSemanticDefinition(protectedResourceId, FPMath.Abs(capacity))
            {
                Name = effectId.Value,
            }, requiredImpactTag);
            return effect;
        }

        /// <summary>
        /// 构造一个控制效果定义，并自动挂上对应控制 Tag。
        /// </summary>
        public EffectDefinition CreateControlEffect(
            EffectId effectId,
            ControlSemanticKind controlKind,
            long durationTicks,
            bool isDebuff = true)
        {
            var effect = new EffectDefinition(effectId)
            {
                DurationPolicy = durationTicks > 0 ? EffectDurationPolicy.Timed : EffectDurationPolicy.Infinite,
                DurationTicks = durationTicks,
            };

            effect.EffectTags.Add(Catalog.Effect.Control);
            effect.GrantedTags.Add(Catalog.State.ControlAny);
            effect.GrantedTags.Add(Catalog.GetControlStateTag(controlKind));
            effect.GrantedTags.Add(isDebuff ? Catalog.State.Debuff : Catalog.State.Buff);
            if (isDebuff)
            {
                effect.GrantedTags.Add(Catalog.State.DispellableNegative);
            }

            return effect;
        }

        /// <summary>
        /// 构造一个免疫效果定义。
        /// </summary>
        public EffectDefinition CreateImmunityEffect(
            EffectId effectId,
            ImmunitySemanticKind immunityKind,
            long durationTicks)
        {
            var effect = new EffectDefinition(effectId)
            {
                DurationPolicy = durationTicks > 0 ? EffectDurationPolicy.Timed : EffectDurationPolicy.Infinite,
                DurationTicks = durationTicks,
            };

            effect.EffectTags.Add(Catalog.Effect.Immunity);
            effect.GrantedTags.Add(Catalog.GetImmunityStateTag(immunityKind));
            effect.GrantedTags.Add(Catalog.State.Buff);
            effect.GrantedTags.Add(Catalog.State.DispellablePositive);
            return effect;
        }

        /// <summary>
        /// 构造一个驱散效果定义。
        /// </summary>
        public EffectDefinition CreateDispelEffect(
            EffectId effectId,
            DispelSemanticKind dispelKind,
            bool removePositive = false,
            bool removeNegative = true)
        {
            var effect = new EffectDefinition(effectId);
            effect.EffectTags.Add(Catalog.Effect.Dispel);

            if (removePositive || dispelKind == DispelSemanticKind.FullPurge)
            {
                effect.RemovedTargetEffectTags.Add(Catalog.State.DispellablePositive);
            }

            if (removeNegative || dispelKind == DispelSemanticKind.FullPurge)
            {
                effect.RemovedTargetEffectTags.Add(Catalog.State.DispellableNegative);
            }

            return effect;
        }

        /// <summary>
        /// 获取或创建效果上的语义扩展。
        /// </summary>
        public CombatSemanticEffectExtension GetOrCreateExtension(EffectDefinition effect)
        {
            if (effect == null)
            {
                return null;
            }

            for (var i = 0; i < effect.Extensions.Count; i++)
            {
                if (effect.Extensions[i] is CombatSemanticEffectExtension existing)
                {
                    return existing;
                }
            }

            var extension = new CombatSemanticEffectExtension();
            effect.Extensions.Add(extension);
            return extension;
        }

        /// <summary>
        /// 把一个护盾定义附着到效果扩展上。
        /// </summary>
        private void AttachShieldDefinition(
            EffectDefinition effect,
            ShieldSemanticDefinition shieldDefinition,
            GameplayTag requiredImpactTag)
        {
            var extension = GetOrCreateExtension(effect);
            if (!string.IsNullOrWhiteSpace(requiredImpactTag.Value))
            {
                shieldDefinition.RequiredImpactTags.Add(requiredImpactTag);
            }

            extension.Shields.Add(shieldDefinition);
        }

        /// <summary>
        /// 根据伤害类型补充对应的语义 Tag。
        /// </summary>
        private void AddDamageKindTags(EffectDefinition effect, DamageSemanticKind damageKind)
        {
            if (damageKind == DamageSemanticKind.Periodic)
            {
                effect.EffectTags.Add(Catalog.Effect.Periodic);
            }

            if (damageKind == DamageSemanticKind.Pure)
            {
                effect.EffectTags.Add(Catalog.Effect.PureDamage);
            }

            if (damageKind == DamageSemanticKind.Reflect)
            {
                effect.EffectTags.Add(Catalog.Effect.ReflectDamage);
            }
        }

        /// <summary>
        /// 根据治疗类型补充对应的语义 Tag。
        /// </summary>
        private void AddHealKindTags(EffectDefinition effect, HealSemanticKind healKind)
        {
            if (healKind == HealSemanticKind.Periodic || healKind == HealSemanticKind.Drain)
            {
                effect.EffectTags.Add(Catalog.Effect.Periodic);
            }
        }
    }
}
