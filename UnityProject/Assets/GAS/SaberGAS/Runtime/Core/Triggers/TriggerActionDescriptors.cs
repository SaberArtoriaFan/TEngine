using System;
using System.Collections.Generic;
using Herta;
using Saber.GAS.Abilities;
using Saber.GAS.Actors;
using Saber.GAS.Effects;
using Saber.GAS.Foundation;
using Saber.GAS.Runtime;
using Saber.GAS.Tags;

namespace Saber.GAS.Triggers
{
    [Flags]
    public enum TriggerActionInspectorField
    {
        None = 0,
        SourceActor = 1 << 0,
        TargetActor = 1 << 1,
        TriggeredAbility = 1 << 2,
        EffectAsset = 1 << 3,
        EffectTag = 1 << 4,
        ResourceId = 1 << 5,
        ResourceAmount = 1 << 6,
        RuntimeTag = 1 << 7,
        OperationTemplate = 1 << 8,
        MagnitudeMultiplier = 1 << 9,
        CueName = 1 << 10,
        AbilityToCancel = 1 << 11,
        CustomActionId = 1 << 12,
    }

    public enum TriggerActionGraphTargetKind
    {
        None = 0,
        TriggeredAbility = 1,
        EffectAsset = 2,
        AbilityToCancel = 3,
    }

    public readonly struct CombatTriggerActionExecutionContext
    {
        internal CombatTriggerActionExecutionContext(
            CombatRuntime runtime,
            CombatTriggerContext triggerContext,
            TriggerCandidate candidate,
            TriggerActionDefinition action)
        {
            Runtime = runtime;
            TriggerContext = triggerContext;
            Candidate = candidate;
            Action = action;
        }

        public CombatRuntime Runtime { get; }

        public CombatTriggerContext TriggerContext { get; }

        internal TriggerCandidate Candidate { get; }

        public TriggerActionDefinition Action { get; }

        public ActorId ResolveActor(TriggerActorReference actorReference)
        {
            return TriggerActionExecutionUtility.ResolveActorReference(actorReference, Candidate, TriggerContext);
        }
    }

    public sealed class TriggerActionDescriptor
    {
        private readonly Action<CombatTriggerActionExecutionContext> _executor;
        private readonly Func<TriggerActionDefinition, string> _validator;

        public TriggerActionDescriptor(
            TriggerActionKind kind,
            Action<CombatTriggerActionExecutionContext> executor,
            TriggerActionInspectorField inspectorFields,
            TriggerActionGraphTargetKind graphTargetKind = TriggerActionGraphTargetKind.None,
            Func<TriggerActionDefinition, string> validator = null,
            string displayName = null)
        {
            Kind = kind;
            _executor = executor;
            _validator = validator;
            InspectorFields = inspectorFields;
            GraphTargetKind = graphTargetKind;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName;
        }

        public TriggerActionKind Kind { get; }

        public string DisplayName { get; }

        public TriggerActionInspectorField InspectorFields { get; }

        public TriggerActionGraphTargetKind GraphTargetKind { get; }

        public bool TryExecute(CombatTriggerActionExecutionContext context)
        {
            if (_executor == null)
            {
                return false;
            }

            _executor(context);
            return true;
        }

        public string Validate(TriggerActionDefinition action)
        {
            return _validator == null ? null : _validator(action);
        }
    }

    public interface ICombatTriggerActionDescriptorRegistry
    {
        IReadOnlyList<TriggerActionDescriptor> Descriptors { get; }
    }

    public static class CombatTriggerActionDescriptorRegistryHub
    {
        private static readonly object SyncRoot = new object();
        private static readonly List<ICombatTriggerActionDescriptorRegistry> Registries = new List<ICombatTriggerActionDescriptorRegistry>();
        private static TriggerActionDescriptor[] _cachedDescriptors;
        private static readonly ICombatTriggerActionDescriptorRegistry DefaultRegistry = new DefaultCombatTriggerActionDescriptorRegistry();

        public static void Register(ICombatTriggerActionDescriptorRegistry registry)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            lock (SyncRoot)
            {
                for (var i = 0; i < Registries.Count; i++)
                {
                    if (ReferenceEquals(Registries[i], registry))
                    {
                        return;
                    }
                }

                Registries.Add(registry);
                _cachedDescriptors = null;
            }
        }

        public static IReadOnlyList<ICombatTriggerActionDescriptorRegistry> Snapshot()
        {
            lock (SyncRoot)
            {
                return Registries.ToArray();
            }
        }

        public static IReadOnlyList<TriggerActionDescriptor> SnapshotDescriptors()
        {
            lock (SyncRoot)
            {
                if (_cachedDescriptors != null)
                {
                    return _cachedDescriptors;
                }

                var composite = new CompositeCombatTriggerActionDescriptorRegistry(
                    DefaultRegistry,
                    Registries);
                _cachedDescriptors = composite.SnapshotDescriptors();
                return _cachedDescriptors;
            }
        }

        public static bool TryGetDescriptor(TriggerActionKind kind, out TriggerActionDescriptor descriptor)
        {
            var descriptors = SnapshotDescriptors();
            for (var i = 0; i < descriptors.Count; i++)
            {
                var candidate = descriptors[i];
                if (candidate.Kind == kind)
                {
                    descriptor = candidate;
                    return true;
                }
            }

            descriptor = null;
            return false;
        }

        internal static CompositeCombatTriggerActionDescriptorRegistry CreateComposite(
            IEnumerable<ICombatTriggerActionDescriptorRegistry> runtimeRegistries = null)
        {
            return new CompositeCombatTriggerActionDescriptorRegistry(DefaultRegistry, Registries, runtimeRegistries);
        }
    }

    internal sealed class CompositeCombatTriggerActionDescriptorRegistry
    {
        private readonly Dictionary<TriggerActionKind, TriggerActionDescriptor> _descriptorLookup;
        private readonly TriggerActionDescriptor[] _descriptorSnapshot;

        public CompositeCombatTriggerActionDescriptorRegistry(
            ICombatTriggerActionDescriptorRegistry defaultRegistry,
            IEnumerable<ICombatTriggerActionDescriptorRegistry> globalRegistries,
            IEnumerable<ICombatTriggerActionDescriptorRegistry> runtimeRegistries = null)
        {
            _descriptorLookup = new Dictionary<TriggerActionKind, TriggerActionDescriptor>();
            Append(defaultRegistry);
            Append(globalRegistries);
            Append(runtimeRegistries);

            var descriptors = new List<TriggerActionDescriptor>(_descriptorLookup.Count);
            foreach (var pair in _descriptorLookup)
            {
                descriptors.Add(pair.Value);
            }

            descriptors.Sort(static (left, right) => ((int)left.Kind).CompareTo((int)right.Kind));
            _descriptorSnapshot = descriptors.ToArray();
        }

        public bool TryExecute(CombatTriggerActionExecutionContext executionContext)
        {
            if (executionContext.Action == null)
            {
                return false;
            }

            if (!_descriptorLookup.TryGetValue(executionContext.Action.Kind, out var descriptor))
            {
                return false;
            }

            return descriptor.TryExecute(executionContext);
        }

        public bool TryGetDescriptor(TriggerActionKind kind, out TriggerActionDescriptor descriptor)
        {
            return _descriptorLookup.TryGetValue(kind, out descriptor);
        }

        public TriggerActionDescriptor[] SnapshotDescriptors()
        {
            return _descriptorSnapshot;
        }

        private void Append(ICombatTriggerActionDescriptorRegistry registry)
        {
            if (registry == null || registry.Descriptors == null)
            {
                return;
            }

            for (var i = 0; i < registry.Descriptors.Count; i++)
            {
                var descriptor = registry.Descriptors[i];
                if (descriptor == null)
                {
                    continue;
                }

                if (_descriptorLookup.ContainsKey(descriptor.Kind))
                {
                    throw new InvalidOperationException(
                        $"Duplicate trigger action descriptor registered for '{descriptor.Kind}'.");
                }

                _descriptorLookup.Add(descriptor.Kind, descriptor);
            }
        }

        private void Append(IEnumerable<ICombatTriggerActionDescriptorRegistry> registries)
        {
            if (registries == null)
            {
                return;
            }

            foreach (var registry in registries)
            {
                Append(registry);
            }
        }
    }

    internal static class TriggerActionExecutionUtility
    {
        public static ActorId ResolveActorReference(
            TriggerActorReference actorReference,
            TriggerCandidate candidate,
            CombatTriggerContext context)
        {
            if (context == null)
            {
                return ActorId.Empty;
            }

            switch (actorReference)
            {
                case TriggerActorReference.Owner:
                    return candidate.OwnerActor == null ? context.OwnerActorId : candidate.OwnerActor.ActorId;
                case TriggerActorReference.Instigator:
                    return context.InstigatorActorId;
                case TriggerActorReference.Target:
                    return context.TargetActorId;
                default:
                    return ActorId.Empty;
            }
        }

        public static CombatImpactOperation CloneImpactOperation(CombatImpactOperation source)
        {
            if (source == null)
            {
                return null;
            }

            return new CombatImpactOperation
            {
                Type = source.Type,
                ResourceId = source.ResourceId,
                EffectId = source.EffectId,
                Tag = source.Tag,
                Amount = source.Amount,
                EffectSpec = source.EffectSpec == null
                    ? null
                    : new EffectSpec(
                        source.EffectSpec.Definition,
                        source.EffectSpec.SourceActorId,
                        source.EffectSpec.TargetActorId,
                        source.EffectSpec.AppliedTick,
                        source.EffectSpec.Stacks),
                CueName = source.CueName,
                Projectile = source.Projectile,
                Payload = source.Payload,
            };
        }
    }

    internal sealed class DefaultCombatTriggerActionDescriptorRegistry : ICombatTriggerActionDescriptorRegistry
    {
        private static readonly TriggerActionDescriptor[] DescriptorList =
        {
            new TriggerActionDescriptor(
                TriggerActionKind.None,
                null,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor),
            new TriggerActionDescriptor(
                TriggerActionKind.ActivateAbility,
                ExecuteActivateAbility,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.TriggeredAbility,
                TriggerActionGraphTargetKind.TriggeredAbility,
                static action => action == null || action.TriggeredAbilityId.IsEmpty
                    ? "TriggerActionKind.ActivateAbility requires TriggeredAbilityId."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.ApplyEffect,
                ExecuteApplyEffect,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.EffectAsset,
                TriggerActionGraphTargetKind.EffectAsset,
                static action => action == null || action.EffectDefinition == null
                    ? "TriggerActionKind.ApplyEffect requires EffectDefinition."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.RemoveEffectById,
                ExecuteRemoveEffectById,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.EffectAsset,
                TriggerActionGraphTargetKind.EffectAsset,
                static action => action == null || action.EffectId.IsEmpty
                    ? "TriggerActionKind.RemoveEffectById requires EffectId."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.RemoveEffectsByTag,
                ExecuteRemoveEffectsByTag,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.EffectTag,
                TriggerActionGraphTargetKind.None,
                static action => action == null || string.IsNullOrWhiteSpace(action.EffectTag.Value)
                    ? "TriggerActionKind.RemoveEffectsByTag requires EffectTag."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.AddImpactOperation,
                ExecuteAddImpactOperation,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.OperationTemplate,
                TriggerActionGraphTargetKind.None,
                static action => action == null || action.OperationTemplate == null
                    ? "TriggerActionKind.AddImpactOperation requires OperationTemplate."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.ModifyImpactMagnitude,
                ExecuteModifyImpactMagnitude,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.MagnitudeMultiplier),
            new TriggerActionDescriptor(
                TriggerActionKind.AddResource,
                ExecuteAddResource,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.ResourceId | TriggerActionInspectorField.ResourceAmount,
                TriggerActionGraphTargetKind.None,
                static action => action == null || action.ResourceId.IsEmpty
                    ? "TriggerActionKind.AddResource requires ResourceId."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.RemoveResource,
                ExecuteRemoveResource,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.ResourceId | TriggerActionInspectorField.ResourceAmount,
                TriggerActionGraphTargetKind.None,
                static action => action == null || action.ResourceId.IsEmpty
                    ? "TriggerActionKind.RemoveResource requires ResourceId."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.AddTag,
                ExecuteAddTag,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.RuntimeTag,
                TriggerActionGraphTargetKind.None,
                static action => action == null || string.IsNullOrWhiteSpace(action.Tag.Value)
                    ? "TriggerActionKind.AddTag requires Tag."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.RemoveTag,
                ExecuteRemoveTag,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.RuntimeTag,
                TriggerActionGraphTargetKind.None,
                static action => action == null || string.IsNullOrWhiteSpace(action.Tag.Value)
                    ? "TriggerActionKind.RemoveTag requires Tag."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.CancelAbility,
                ExecuteCancelAbility,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.AbilityToCancel,
                TriggerActionGraphTargetKind.AbilityToCancel,
                static action => action == null || action.AbilityToCancelId.IsEmpty
                    ? "TriggerActionKind.CancelAbility requires AbilityToCancelId."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.CleanseByTag,
                ExecuteRemoveEffectsByTag,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.EffectTag,
                TriggerActionGraphTargetKind.None,
                static action => action == null || string.IsNullOrWhiteSpace(action.EffectTag.Value)
                    ? "TriggerActionKind.CleanseByTag requires EffectTag."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.EmitCue,
                ExecuteEmitCue,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.CueName,
                TriggerActionGraphTargetKind.None,
                static action => action == null || string.IsNullOrWhiteSpace(action.CueName)
                    ? "TriggerActionKind.EmitCue requires CueName."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.SplitImpactToActor,
                ExecuteSplitImpactToActor,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.MagnitudeMultiplier | TriggerActionInspectorField.RuntimeTag),
            new TriggerActionDescriptor(
                TriggerActionKind.AddResourceFromImpact,
                ExecuteAddResourceFromImpact,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.ResourceId | TriggerActionInspectorField.MagnitudeMultiplier,
                TriggerActionGraphTargetKind.None,
                static action => action == null || action.ResourceId.IsEmpty
                    ? "TriggerActionKind.AddResourceFromImpact requires ResourceId."
                    : null),
            new TriggerActionDescriptor(
                TriggerActionKind.Custom,
                ExecuteCustom,
                TriggerActionInspectorField.SourceActor | TriggerActionInspectorField.TargetActor | TriggerActionInspectorField.CustomActionId,
                TriggerActionGraphTargetKind.None,
                static action => action == null || action.CustomActionId <= 0
                    ? "TriggerActionKind.Custom requires CustomActionId > 0."
                    : null),
        };

        public IReadOnlyList<TriggerActionDescriptor> Descriptors => DescriptorList;

        private static void ExecuteActivateAbility(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var sourceActorId = context.ResolveActor(action.SourceActor);
            if (sourceActorId.IsEmpty || action.TriggeredAbilityId.IsEmpty)
            {
                return;
            }

            var targetActorId = context.ResolveActor(action.TargetActor);
            runtime.ExecuteTriggeredAbility(sourceActorId, action.TriggeredAbilityId, targetActorId, null, action.CustomPayload);
        }

        private static void ExecuteApplyEffect(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var sourceActorId = context.ResolveActor(action.SourceActor);
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || action.EffectDefinition == null)
            {
                return;
            }

            runtime.ApplyTriggerEffect(action.EffectDefinition, sourceActorId, targetActorId, 1);
        }

        private static void ExecuteRemoveEffectById(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || action.EffectId.IsEmpty)
            {
                return;
            }

            runtime.RemoveEffectsByIdDirect(targetActorId, action.EffectId);
        }

        private static void ExecuteRemoveEffectsByTag(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || string.IsNullOrWhiteSpace(action.EffectTag.Value))
            {
                return;
            }

            runtime.RemoveEffectsByTagDirect(targetActorId, action.EffectTag);
        }

        private static void ExecuteAddImpactOperation(CombatTriggerActionExecutionContext context)
        {
            var triggerContext = context.TriggerContext;
            var action = context.Action;
            if (triggerContext?.RelatedImpact == null || action.OperationTemplate == null)
            {
                return;
            }

            triggerContext.RelatedImpact.Operations.Add(TriggerActionExecutionUtility.CloneImpactOperation(action.OperationTemplate));
        }

        private static void ExecuteModifyImpactMagnitude(CombatTriggerActionExecutionContext context)
        {
            var triggerContext = context.TriggerContext;
            var action = context.Action;
            if (triggerContext?.RelatedImpact == null)
            {
                return;
            }

            if (triggerContext.RelatedOperation != null &&
                triggerContext.RelatedOperation.Type == CombatImpactOperationType.ResourceDelta)
            {
                triggerContext.RelatedOperation.Amount = triggerContext.RelatedOperation.Amount * action.MagnitudeMultiplier;
                return;
            }

            var operations = triggerContext.RelatedImpact.Operations;
            for (var i = 0; i < operations.Count; i++)
            {
                var operation = operations[i];
                if (operation.Type != CombatImpactOperationType.ResourceDelta)
                {
                    continue;
                }

                operation.Amount = operation.Amount * action.MagnitudeMultiplier;
            }
        }

        private static void ExecuteAddResource(CombatTriggerActionExecutionContext context)
        {
            ExecuteModifyResource(context, false);
        }

        private static void ExecuteRemoveResource(CombatTriggerActionExecutionContext context)
        {
            ExecuteModifyResource(context, true);
        }

        private static void ExecuteModifyResource(CombatTriggerActionExecutionContext context, bool negate)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || action.ResourceId.IsEmpty)
            {
                return;
            }

            var delta = negate ? -action.ResourceAmount : action.ResourceAmount;
            runtime.ModifyResourceDirect(targetActorId, action.ResourceId, delta, context.TriggerContext, context.Candidate.OwnerActor);
        }

        private static void ExecuteAddTag(CombatTriggerActionExecutionContext context)
        {
            ExecuteModifyTag(context, true);
        }

        private static void ExecuteRemoveTag(CombatTriggerActionExecutionContext context)
        {
            ExecuteModifyTag(context, false);
        }

        private static void ExecuteModifyTag(CombatTriggerActionExecutionContext context, bool add)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || string.IsNullOrWhiteSpace(action.Tag.Value))
            {
                return;
            }

            if (add)
            {
                runtime.AddRuntimeTagDirect(targetActorId, action.Tag);
            }
            else
            {
                runtime.RemoveRuntimeTagDirect(targetActorId, action.Tag);
            }
        }

        private static void ExecuteCancelAbility(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || action.AbilityToCancelId.IsEmpty)
            {
                return;
            }

            runtime.CancelAbilityByIdDirect(targetActorId, action.AbilityToCancelId, "Cancelled by trigger.");
        }

        private static void ExecuteEmitCue(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty)
            {
                targetActorId = context.ResolveActor(TriggerActorReference.Owner);
            }

            runtime.EmitCueDirect(targetActorId, action.CueName);
        }

        private static void ExecuteSplitImpactToActor(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty)
            {
                return;
            }

            runtime.SplitImpactToActorDirect(context.TriggerContext, targetActorId, action.MagnitudeMultiplier, action.Tag);
        }

        private static void ExecuteAddResourceFromImpact(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            var action = context.Action;
            var targetActorId = context.ResolveActor(action.TargetActor);
            if (targetActorId.IsEmpty || action.ResourceId.IsEmpty)
            {
                return;
            }

            runtime.AddResourceFromImpactDirect(
                context.TriggerContext,
                targetActorId,
                action.ResourceId,
                action.MagnitudeMultiplier,
                context.Candidate.OwnerActor);
        }

        private static void ExecuteCustom(CombatTriggerActionExecutionContext context)
        {
            var runtime = context.Runtime;
            runtime.ExecuteCustomTriggerAction(new CombatCustomTriggerActionExecutionContext(
                runtime,
                context.TriggerContext,
                context.Action,
                context.Candidate.OwnerActor,
                context.Candidate.SourceKind,
                context.Candidate.SourceEffect,
                context.Candidate.SourceAbilityInstance));
        }
    }
}

