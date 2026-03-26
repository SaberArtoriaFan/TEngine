using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Pooling;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.Semantics
{
    /// <summary>
    /// 标准战斗语义模块。
    /// 统一负责语义标签解释、效果接线、护盾状态维护，以及快照深拷贝时的扩展克隆。
    /// </summary>
    public sealed class CombatSemanticModule : ICombatRuleModule, ICombatExtensionCloneHandler
    {
        /// <summary>
        /// 缓存语义效果状态对象池。
        /// </summary>
        private readonly CombatObjectPool<CombatSemanticEffectState> _effectStatePool;
        /// <summary>
        /// 缓存护盾状态对象池。
        /// </summary>
        private readonly CombatObjectPool<ActiveShieldState> _shieldStatePool;

        /// <summary>
        /// 使用指定目录创建标准语义模块。
        /// </summary>
        public CombatSemanticModule(CombatSemanticCatalog catalog = null)
        {
            Catalog = catalog ?? new CombatSemanticCatalog();
            _effectStatePool = new CombatObjectPool<CombatSemanticEffectState>(() => new CombatSemanticEffectState());
            _shieldStatePool = new CombatObjectPool<ActiveShieldState>(() => new ActiveShieldState());
        }

        /// <summary>
        /// 获取当前模块使用的语义标签目录。
        /// </summary>
        public CombatSemanticCatalog Catalog { get; }

        /// <summary>
        /// 初始化语义模块。
        /// 当前实现不需要额外启动逻辑，保留此入口用于后续扩展。
        /// </summary>
        public void Initialize(CombatRuntime runtime)
        {
        }

        /// <summary>
        /// 关闭语义模块并清空内部对象池。
        /// </summary>
        public void Shutdown(CombatRuntime runtime)
        {
            _shieldStatePool.Reset();
            _effectStatePool.Reset();
        }

        /// <summary>
        /// 在技能尝试阶段应用标准控制语义的阻断规则。
        /// </summary>
        public void EvaluateAbilityAttempt(CombatActionAttempt attempt, CombatWorldState worldState)
        {
            if (attempt == null || attempt.SourceActor == null || attempt.Ability == null)
            {
                return;
            }

            var actor = attempt.SourceActor;
            if (actor.Tags.Contains(Catalog.State.ControlStun) ||
                actor.Tags.Contains(Catalog.State.ControlSuppression) ||
                actor.Tags.Contains(Catalog.State.ControlFear) ||
                actor.Tags.Contains(Catalog.State.ControlCharm) ||
                actor.Tags.Contains(Catalog.State.ControlTaunt))
            {
                attempt.AddBlock(ActionBlockKind.SourceState, "Source actor is under a hard control state.");
                return;
            }

            if (actor.Tags.Contains(Catalog.State.ControlSilence) &&
                attempt.Ability.AbilityTags.Contains(Catalog.Ability.Cast))
            {
                attempt.AddBlock(ActionBlockKind.SourceState, "Source actor is silenced.");
            }

            if (actor.Tags.Contains(Catalog.State.ControlDisarm) &&
                attempt.Ability.AbilityTags.Contains(Catalog.Ability.Attack))
            {
                attempt.AddBlock(ActionBlockKind.SourceState, "Source actor is disarmed.");
            }
        }

        /// <summary>
        /// 在效果施加前检查控制免疫和驱散免疫。
        /// </summary>
        public bool TryBlockEffectApplication(
            CombatActorState targetActor,
            EffectDefinition effectDefinition,
            CombatWorldState worldState,
            out string reason)
        {
            reason = null;
            if (targetActor == null || effectDefinition == null)
            {
                return false;
            }

            if (IsControlEffect(effectDefinition) && HasControlImmunity(targetActor))
            {
                reason = "Target actor is immune to control.";
                return true;
            }

            if (IsDispelEffect(effectDefinition) && HasDispelImmunity(targetActor))
            {
                reason = "Target actor is immune to dispel.";
                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断某个效果是否需要创建语义层运行时状态。
        /// </summary>
        public bool HasRuntimeEffectPayload(EffectDefinition effectDefinition)
        {
            return TryGetEffectExtension(effectDefinition, out var extension) &&
                   extension.Shields.Count > 0;
        }

        /// <summary>
        /// 在效果刚施加时创建或重建语义运行时状态。
        /// </summary>
        public void OnEffectApplied(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime)
        {
            if (!TryGetEffectExtension(activeEffect?.Spec?.Definition, out var extension) || extension.Shields.Count == 0)
            {
                return;
            }

            var state = GetOrCreateEffectState(activeEffect, extension);
            RebuildShieldStates(state, activeEffect.Spec.Stacks);
        }

        /// <summary>
        /// 在效果刷新时按定义决定是否重建护盾容量。
        /// </summary>
        public void OnEffectRefreshed(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime)
        {
            if (!TryGetEffectState(activeEffect, out var state) || state.Extension == null)
            {
                return;
            }

            for (var i = 0; i < state.Extension.Shields.Count; i++)
            {
                var shieldDefinition = state.Extension.Shields[i];
                if (shieldDefinition != null && shieldDefinition.RefreshCapacityOnReapply)
                {
                    RebuildShieldStates(state, activeEffect.Spec.Stacks);
                    return;
                }
            }
        }

        /// <summary>
        /// 在效果移除前释放语义层附带的运行时状态。
        /// </summary>
        public void OnEffectRemoving(CombatActorState targetActor, ActiveEffect activeEffect, CombatRuntime runtime)
        {
            if (!TryGetEffectState(activeEffect, out var state))
            {
                return;
            }

            ReleaseShieldStates(state);
            activeEffect.ExtensionStates.Remove(state);
            _effectStatePool.Return(state);
        }

        /// <summary>
        /// 处理资源变化的标准语义，例如伤害免疫和护盾吸收。
        /// </summary>
        public void ProcessResourceDelta(CombatResourceDeltaContext context)
        {
            if (!IsDamageDelta(context))
            {
                return;
            }

            if (HasDamageImmunity(context.TargetActor, context))
            {
                context.AddImpactTag(Catalog.Impact.PreventedByImmunity);
                context.Amount = FP._0;
                context.ConsumeDefaultApply = true;
                return;
            }

            var remainingDamage = AbsorbDamageByShields(context.TargetActor, context);
            if (remainingDamage <= FP._0)
            {
                context.Amount = FP._0;
                context.ConsumeDefaultApply = true;
                return;
            }

            context.Amount = -remainingDamage;
        }

        /// <summary>
        /// 判断当前模块是否能克隆指定效果扩展定义。
        /// </summary>
        public bool CanCloneEffectExtension(ICombatEffectExtensionDefinition definition)
        {
            return definition is CombatSemanticEffectExtension;
        }

        /// <summary>
        /// 深拷贝一份效果扩展定义。
        /// </summary>
        public ICombatEffectExtensionDefinition CloneEffectExtension(ICombatEffectExtensionDefinition definition)
        {
            if (!(definition is CombatSemanticEffectExtension source))
            {
                return null;
            }

            var clone = new CombatSemanticEffectExtension();
            for (var i = 0; i < source.Shields.Count; i++)
            {
                clone.Shields.Add(CloneShieldDefinition(source.Shields[i]));
            }

            return clone;
        }

        /// <summary>
        /// 判断当前模块是否能克隆指定运行时扩展状态。
        /// </summary>
        public bool CanCloneActiveEffectState(ICombatActiveEffectExtensionState state)
        {
            return state is CombatSemanticEffectState;
        }

        /// <summary>
        /// 深拷贝一份效果运行时扩展状态。
        /// </summary>
        public ICombatActiveEffectExtensionState CloneActiveEffectState(ICombatActiveEffectExtensionState state)
        {
            if (!(state is CombatSemanticEffectState source))
            {
                return null;
            }

            var clone = new CombatSemanticEffectState
            {
                Extension = (CombatSemanticEffectExtension)CloneEffectExtension(source.Extension),
                ActiveShields = new List<ActiveShieldState>(),
            };

            for (var i = 0; i < source.ActiveShields.Count; i++)
            {
                clone.ActiveShields.Add(CloneShieldState(source.ActiveShields[i]));
            }

            return clone;
        }

        /// <summary>
        /// 判断本次资源变化是否属于伤害语义。
        /// </summary>
        private bool IsDamageDelta(CombatResourceDeltaContext context)
        {
            if (context == null || context.TargetActor == null || context.ResourceId.IsEmpty || context.Amount >= FP._0)
            {
                return false;
            }

            if (context.Impact != null)
            {
                return context.Impact.Tags.Contains(Catalog.Impact.Damage) ||
                       context.Impact.Tags.Contains(Catalog.Effect.Damage);
            }

            return context.EffectDefinition != null &&
                   context.EffectDefinition.EffectTags.Contains(Catalog.Effect.Damage);
        }

        /// <summary>
        /// 判断目标当前是否免疫本次伤害。
        /// </summary>
        private bool HasDamageImmunity(CombatActorState actor, CombatResourceDeltaContext context)
        {
            if (actor == null)
            {
                return false;
            }

            if (actor.Tags.Contains(Catalog.State.ImmunityAll))
            {
                return true;
            }

            if (!actor.Tags.Contains(Catalog.State.ImmunityDamage))
            {
                return false;
            }

            return !IsPureDamage(context);
        }

        /// <summary>
        /// 判断本次伤害是否为纯粹伤害。
        /// </summary>
        private bool IsPureDamage(CombatResourceDeltaContext context)
        {
            if (context.Impact != null && context.Impact.Tags.Contains(Catalog.Effect.PureDamage))
            {
                return true;
            }

            return context.EffectDefinition != null &&
                   context.EffectDefinition.EffectTags.Contains(Catalog.Effect.PureDamage);
        }

        /// <summary>
        /// 判断目标是否具有控制免疫。
        /// </summary>
        private bool HasControlImmunity(CombatActorState actor)
        {
            return actor != null &&
                   (actor.Tags.Contains(Catalog.State.ImmunityAll) ||
                    actor.Tags.Contains(Catalog.State.ImmunityControl));
        }

        /// <summary>
        /// 判断目标是否具有驱散免疫。
        /// </summary>
        private bool HasDispelImmunity(CombatActorState actor)
        {
            return actor != null &&
                   (actor.Tags.Contains(Catalog.State.ImmunityAll) ||
                    actor.Tags.Contains(Catalog.State.ImmunityDispel));
        }

        /// <summary>
        /// 判断一个效果是否属于控制类效果。
        /// </summary>
        private bool IsControlEffect(EffectDefinition effectDefinition)
        {
            return effectDefinition != null &&
                   (effectDefinition.EffectTags.Contains(Catalog.Effect.Control) ||
                    effectDefinition.GrantedTags.Contains(Catalog.State.ControlAny));
        }

        /// <summary>
        /// 判断一个效果是否属于驱散类效果。
        /// </summary>
        private bool IsDispelEffect(EffectDefinition effectDefinition)
        {
            return effectDefinition != null &&
                   effectDefinition.EffectTags.Contains(Catalog.Effect.Dispel);
        }

        /// <summary>
        /// 用目标身上的护盾吸收本次伤害，并返回剩余伤害。
        /// </summary>
        private FP AbsorbDamageByShields(CombatActorState targetActor, CombatResourceDeltaContext context)
        {
            if (targetActor == null || context == null || context.Amount >= FP._0)
            {
                return FP._0;
            }

            var incomingDamage = -context.Amount;
            var remainingDamage = incomingDamage;
            for (var effectIndex = targetActor.ActiveEffects.Count - 1; effectIndex >= 0 && remainingDamage > FP._0; effectIndex--)
            {
                var activeEffect = targetActor.ActiveEffects[effectIndex];
                if (!TryGetEffectState(activeEffect, out var state) || state.ActiveShields.Count == 0)
                {
                    continue;
                }

                for (var shieldIndex = state.ActiveShields.Count - 1; shieldIndex >= 0 && remainingDamage > FP._0; shieldIndex--)
                {
                    var shieldState = state.ActiveShields[shieldIndex];
                    if (!DoesShieldMatch(shieldState, context) || shieldState.Remaining <= FP._0)
                    {
                        continue;
                    }

                    var absorbed = shieldState.Remaining < remainingDamage
                        ? shieldState.Remaining
                        : remainingDamage;

                    shieldState.Remaining -= absorbed;
                    remainingDamage -= absorbed;
                }
            }

            if (remainingDamage < incomingDamage)
            {
                context.AddImpactTag(Catalog.Impact.ShieldAbsorbed);
                RemoveDepletedShieldEffects(targetActor, context.Runtime);
            }

            return remainingDamage;
        }

        /// <summary>
        /// 判断某层护盾是否能拦截本次伤害。
        /// </summary>
        private bool DoesShieldMatch(ActiveShieldState shieldState, CombatResourceDeltaContext context)
        {
            if (shieldState == null || shieldState.Definition == null || shieldState.Definition.ProtectedResourceId != context.ResourceId)
            {
                return false;
            }

            if (context.Impact == null)
            {
                return true;
            }

            var requiredTags = shieldState.Definition.RequiredImpactTags;
            if (requiredTags.Count > 0 && !context.Impact.Tags.ContainsAll(requiredTags))
            {
                return false;
            }

            var blockedTags = shieldState.Definition.BlockedImpactTags;
            if (blockedTags.Count > 0 && context.Impact.Tags.ContainsAny(blockedTags))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 移除已经被耗尽且声明需要移除源效果的护盾效果。
        /// </summary>
        private void RemoveDepletedShieldEffects(CombatActorState actor, CombatRuntime runtime)
        {
            if (actor == null || runtime == null)
            {
                return;
            }

            for (var i = actor.ActiveEffects.Count - 1; i >= 0; i--)
            {
                var activeEffect = actor.ActiveEffects[i];
                if (!TryGetEffectState(activeEffect, out var state) || !ShouldRemoveDepletedShieldEffect(state))
                {
                    continue;
                }

                runtime.RemoveEffectDirect(actor, activeEffect);
            }
        }

        /// <summary>
        /// 判断一个语义状态对应的护盾效果是否应被移除。
        /// </summary>
        private bool ShouldRemoveDepletedShieldEffect(CombatSemanticEffectState state)
        {
            if (state == null || state.ActiveShields.Count == 0)
            {
                return false;
            }

            for (var i = 0; i < state.ActiveShields.Count; i++)
            {
                var shieldState = state.ActiveShields[i];
                if (shieldState == null || shieldState.Definition == null)
                {
                    continue;
                }

                if (!shieldState.Definition.RemoveSourceEffectWhenDepleted || shieldState.Remaining > FP._0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 获取或创建持续效果对应的语义运行时状态。
        /// </summary>
        private CombatSemanticEffectState GetOrCreateEffectState(ActiveEffect activeEffect, CombatSemanticEffectExtension extension)
        {
            if (TryGetEffectState(activeEffect, out var existing))
            {
                existing.Extension = extension;
                return existing;
            }

            var state = _effectStatePool.Rent();
            state.Initialize(extension);
            activeEffect.ExtensionStates.Add(state);
            return state;
        }

        /// <summary>
        /// 从持续效果的扩展状态列表里查找语义状态。
        /// </summary>
        private bool TryGetEffectState(ActiveEffect activeEffect, out CombatSemanticEffectState state)
        {
            state = null;
            if (activeEffect == null)
            {
                return false;
            }

            for (var i = 0; i < activeEffect.ExtensionStates.Count; i++)
            {
                state = activeEffect.ExtensionStates[i] as CombatSemanticEffectState;
                if (state != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 从效果定义的扩展列表里查找语义扩展。
        /// </summary>
        private bool TryGetEffectExtension(EffectDefinition effectDefinition, out CombatSemanticEffectExtension extension)
        {
            extension = null;
            if (effectDefinition == null)
            {
                return false;
            }

            for (var i = 0; i < effectDefinition.Extensions.Count; i++)
            {
                extension = effectDefinition.Extensions[i] as CombatSemanticEffectExtension;
                if (extension != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 按当前层数重建护盾运行时列表。
        /// </summary>
        private void RebuildShieldStates(CombatSemanticEffectState state, int stacks)
        {
            if (state == null || state.Extension == null)
            {
                return;
            }

            ReleaseShieldStates(state);
            for (var i = 0; i < state.Extension.Shields.Count; i++)
            {
                var definition = state.Extension.Shields[i];
                if (definition == null || definition.Capacity <= FP._0)
                {
                    continue;
                }

                var shieldState = _shieldStatePool.Rent();
                shieldState.Initialize(definition, stacks);
                state.ActiveShields.Add(shieldState);
            }
        }

        /// <summary>
        /// 释放某个语义状态持有的全部护盾实例。
        /// </summary>
        private void ReleaseShieldStates(CombatSemanticEffectState state)
        {
            if (state == null)
            {
                return;
            }

            for (var i = state.ActiveShields.Count - 1; i >= 0; i--)
            {
                _shieldStatePool.Return(state.ActiveShields[i]);
            }

            state.ActiveShields.Clear();
        }

        /// <summary>
        /// 深拷贝一份护盾定义。
        /// </summary>
        private ShieldSemanticDefinition CloneShieldDefinition(ShieldSemanticDefinition source)
        {
            if (source == null)
            {
                return null;
            }

            return new ShieldSemanticDefinition
            {
                Name = source.Name,
                ProtectedResourceId = source.ProtectedResourceId,
                Capacity = source.Capacity,
                RequiredImpactTags = CloneTagContainer(source.RequiredImpactTags),
                BlockedImpactTags = CloneTagContainer(source.BlockedImpactTags),
                RefreshCapacityOnReapply = source.RefreshCapacityOnReapply,
                RemoveSourceEffectWhenDepleted = source.RemoveSourceEffectWhenDepleted,
            };
        }

        /// <summary>
        /// 深拷贝一份护盾运行时状态。
        /// </summary>
        private ActiveShieldState CloneShieldState(ActiveShieldState source)
        {
            if (source == null)
            {
                return null;
            }

            return new ActiveShieldState
            {
                Definition = CloneShieldDefinition(source.Definition),
                Remaining = source.Remaining,
            };
        }

        /// <summary>
        /// 深拷贝一个 Tag 容器。
        /// </summary>
        private GameplayTagContainer CloneTagContainer(GameplayTagContainer source)
        {
            return source == null ? new GameplayTagContainer() : new GameplayTagContainer(source);
        }
    }
}
