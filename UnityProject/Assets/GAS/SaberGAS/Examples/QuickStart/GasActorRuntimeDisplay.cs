using System;
using System.Text;
using Herta;
using Saber.GAS.Actors;
using Saber.GAS.Foundation;
using Saber.GAS.Triggers;
using UnityEngine;

namespace Saber.GAS.Examples.QuickStart
{
    /// <summary>
    /// Actor-side inspector-only runtime viewer for AttributeSet/Ability/Effect/Trigger.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GasActorRuntimeDisplay : MonoBehaviour
    {
        public enum InspectorSection
        {
            Overview = 0,
            AttributeSet = 1,
            Ability = 2,
            Effect = 3,
            Trigger = 4,
        }

        [Header("Binding")]
        [SerializeField] private MonoBehaviour _runtimeProvider;
        [SerializeField] private string _actorId;
        [SerializeField] private string _title;
        [SerializeField] private bool _autoFindProviderInScene = true;

        [Header("Refresh")]
        [SerializeField] private bool _autoRefreshInPlayMode = true;
        [SerializeField] [Min(0.05f)] private float _refreshInterval = 0.25f;
        [SerializeField] [Range(3, 50)] private int _maxEntriesPerSection = 12;
        [SerializeField] private InspectorSection _section = InspectorSection.Overview;

        [Header("Inspector Output (Read Only)")]
        [SerializeField] private string _status = "Not refreshed.";
        [SerializeField] [TextArea(12, 40)] private string _inspectorText = string.Empty;

        private readonly StringBuilder _builder = new StringBuilder(2048);
        private IGasActorRuntimeStateProvider _provider;
        private float _nextRefreshTime;

        public InspectorSection Section => _section;

        public string InspectorStatus => _status;

        public string InspectorText => _inspectorText;

        public bool AutoRefreshInPlayMode => _autoRefreshInPlayMode;

        public void Configure(MonoBehaviour runtimeProvider, ActorId actorId, string title)
        {
            _runtimeProvider = runtimeProvider;
            _actorId = actorId.Value;
            _title = title;
            RefreshInspectorContent();
        }

        public void SetSection(InspectorSection section)
        {
            _section = section;
            RefreshInspectorContent();
        }

        [ContextMenu("Refresh Inspector Data")]
        public void RefreshInspectorContent()
        {
            if (!ResolveProvider())
            {
                _status = "Provider missing.";
                _inspectorText = string.Empty;
                return;
            }

            if (!TryBuildActorId(out var actorId))
            {
                _status = "ActorId invalid.";
                _inspectorText = string.Empty;
                return;
            }

            if (!_provider.TryGetActorState(actorId, out var actor) || actor == null)
            {
                _status = $"Actor not found: {actorId.Value}";
                _inspectorText = string.Empty;
                return;
            }

            _builder.Length = 0;
            AppendHeader(actor);

            switch (_section)
            {
                case InspectorSection.Overview:
                    AppendOverview(actor);
                    break;
                case InspectorSection.AttributeSet:
                    AppendAttributeSection(actor);
                    break;
                case InspectorSection.Ability:
                    AppendAbilitySection(actor);
                    break;
                case InspectorSection.Effect:
                    AppendEffectSection(actor);
                    break;
                case InspectorSection.Trigger:
                    AppendTriggerSection(actor);
                    break;
            }

            _inspectorText = _builder.ToString();
            var tick = _provider.WorldState != null ? _provider.WorldState.CurrentTick.Value : 0L;
            _status = $"OK Tick={tick}  Time={DateTime.Now:HH:mm:ss}";
        }

        private void Update()
        {
            if (!Application.isPlaying || !_autoRefreshInPlayMode)
            {
                return;
            }

            if (Time.unscaledTime < _nextRefreshTime)
            {
                return;
            }

            _nextRefreshTime = Time.unscaledTime + _refreshInterval;
            RefreshInspectorContent();
        }

        private void AppendHeader(CombatActorState actor)
        {
            var header = string.IsNullOrWhiteSpace(_title) ? actor.ActorId.Value : _title;
            var tick = _provider.WorldState != null ? _provider.WorldState.CurrentTick.Value : 0L;

            _builder.Append(header);
            _builder.Append(" | Tick=");
            _builder.Append(tick);
            _builder.Append('\n');
            _builder.Append("ActorId=");
            _builder.Append(actor.ActorId.Value);
            _builder.Append(" Team=");
            _builder.Append(actor.TeamId.Value);
            _builder.Append(" Alive=");
            _builder.Append(actor.IsAlive ? "Y" : "N");
            _builder.Append('\n');
            _builder.Append("Pos=");
            _builder.Append(FormatFp(actor.Position.X));
            _builder.Append(',');
            _builder.Append(FormatFp(actor.Position.Y));
            _builder.Append(',');
            _builder.Append(FormatFp(actor.Position.Z));
            _builder.Append('\n');
        }

        private void AppendOverview(CombatActorState actor)
        {
            _builder.Append("[Overview]");
            _builder.Append('\n');
            _builder.Append("- Attributes=");
            _builder.Append(CountAttributeEntries(actor));
            _builder.Append('\n');
            _builder.Append("- Resources=");
            _builder.Append(CountResourceEntries(actor));
            _builder.Append('\n');
            _builder.Append("- GrantedAbilities=");
            _builder.Append(actor.GrantedAbilities.Count);
            _builder.Append('\n');
            _builder.Append("- ActiveAbilityInstances=");
            _builder.Append(actor.ActiveAbilityInstances.Count);
            _builder.Append('\n');
            _builder.Append("- ActiveEffects=");
            _builder.Append(actor.ActiveEffects.Count);
            _builder.Append('\n');
            _builder.Append("- ActorTriggers=");
            _builder.Append(actor.ActiveTriggers.Count);
            _builder.Append('\n');

            var abilityTriggerTotal = 0;
            for (var i = 0; i < actor.ActiveAbilityInstances.Count; i++)
            {
                abilityTriggerTotal += actor.ActiveAbilityInstances[i].ActiveTriggers.Count;
            }

            var effectTriggerTotal = 0;
            for (var i = 0; i < actor.ActiveEffects.Count; i++)
            {
                effectTriggerTotal += actor.ActiveEffects[i].ActiveTriggers.Count;
            }

            _builder.Append("- AbilityInstanceTriggers=");
            _builder.Append(abilityTriggerTotal);
            _builder.Append('\n');
            _builder.Append("- EffectInstanceTriggers=");
            _builder.Append(effectTriggerTotal);
            _builder.Append('\n');
        }

        private void AppendAttributeSection(CombatActorState actor)
        {
            _builder.Append("[AttributeSet]");
            _builder.Append('\n');
            var count = 0;
            foreach (var pair in actor.Attributes.Entries)
            {
                count++;
                if (count > _maxEntriesPerSection)
                {
                    _builder.Append("... (truncated)");
                    _builder.Append('\n');
                    break;
                }

                var value = pair.Value;
                _builder.Append("- ");
                _builder.Append(pair.Key.Value);
                _builder.Append(" Base=");
                _builder.Append(FormatFp(value.BaseValue));
                _builder.Append(" Cur=");
                _builder.Append(FormatFp(value.Evaluate()));
                _builder.Append(" Mods=");
                _builder.Append(value.Modifiers.Count);
                _builder.Append('\n');
            }

            if (count == 0)
            {
                _builder.Append("- (empty)");
                _builder.Append('\n');
            }

            _builder.Append('\n');
            _builder.Append("[ResourceSet]");
            _builder.Append('\n');
            var resourceCount = 0;
            foreach (var pair in actor.Resources.Entries)
            {
                resourceCount++;
                if (resourceCount > _maxEntriesPerSection)
                {
                    _builder.Append("... (truncated)");
                    _builder.Append('\n');
                    break;
                }

                var value = pair.Value;
                _builder.Append("- ");
                _builder.Append(pair.Key.Value);
                _builder.Append(" Cur=");
                _builder.Append(FormatFp(value.Current));
                _builder.Append('/');
                _builder.Append(FormatFp(value.Max));
                _builder.Append(" Regen=");
                _builder.Append(FormatFp(value.RegenPerTick));
                _builder.Append('\n');
            }

            if (resourceCount == 0)
            {
                _builder.Append("- (empty)");
                _builder.Append('\n');
            }
        }

        private void AppendAbilitySection(CombatActorState actor)
        {
            _builder.Append("[Ability: Granted]");
            _builder.Append('\n');
            if (actor.GrantedAbilities.Count == 0)
            {
                _builder.Append("- (empty)");
                _builder.Append('\n');
            }
            else
            {
                var printed = 0;
                for (var i = 0; i < actor.GrantedAbilities.Count; i++)
                {
                    printed++;
                    if (printed > _maxEntriesPerSection)
                    {
                        _builder.Append("... (truncated)");
                        _builder.Append('\n');
                        break;
                    }

                    var abilityId = actor.GrantedAbilities[i];
                    Saber.GAS.Abilities.AbilityDefinition definition = null;
                    var hasDefinition = _provider.WorldState != null &&
                                        _provider.WorldState.TryGetAbility(abilityId, out definition);
                    _builder.Append("- ");
                    _builder.Append(abilityId.Value);
                    if (hasDefinition)
                    {
                        if (!string.IsNullOrWhiteSpace(definition.Name))
                        {
                            _builder.Append(" (");
                            _builder.Append(definition.Name);
                            _builder.Append(')');
                        }

                        _builder.Append(" Effects=");
                        _builder.Append(definition.Effects.Count + definition.PeriodicEffects.Count + definition.EndEffects.Count);
                        _builder.Append(" Triggers=");
                        _builder.Append(definition.Triggers.Count);
                    }
                    else
                    {
                        _builder.Append(" (def missing)");
                    }

                    _builder.Append('\n');
                }
            }

            _builder.Append('\n');
            _builder.Append("[Ability: ActiveInstances]");
            _builder.Append('\n');
            if (actor.ActiveAbilityInstances.Count == 0)
            {
                _builder.Append("- (none)");
                _builder.Append('\n');
                return;
            }

            var activePrinted = 0;
            for (var i = 0; i < actor.ActiveAbilityInstances.Count; i++)
            {
                activePrinted++;
                if (activePrinted > _maxEntriesPerSection)
                {
                    _builder.Append("... (truncated)");
                    _builder.Append('\n');
                    break;
                }

                var instance = actor.ActiveAbilityInstances[i];
                _builder.Append("- #");
                _builder.Append(instance.InstanceId.Value);
                _builder.Append(" Ability=");
                _builder.Append(instance.Ability != null ? instance.Ability.Id.Value : "(null)");
                _builder.Append(" State=");
                _builder.Append(instance.State);
                _builder.Append(" Triggers=");
                _builder.Append(instance.ActiveTriggers.Count);
                _builder.Append('\n');
            }
        }

        private void AppendEffectSection(CombatActorState actor)
        {
            _builder.Append("[Effect: Active]");
            _builder.Append('\n');
            if (actor.ActiveEffects.Count == 0)
            {
                _builder.Append("- (none)");
                _builder.Append('\n');
                return;
            }

            var printed = 0;
            for (var i = 0; i < actor.ActiveEffects.Count; i++)
            {
                printed++;
                if (printed > _maxEntriesPerSection)
                {
                    _builder.Append("... (truncated)");
                    _builder.Append('\n');
                    break;
                }

                var effect = actor.ActiveEffects[i];
                var spec = effect.Spec;
                var definition = spec != null ? spec.Definition : null;
                _builder.Append("- #");
                _builder.Append(i + 1);
                _builder.Append(" Id=");
                _builder.Append(definition != null ? definition.Id.Value : "(null)");
                _builder.Append(" Stacks=");
                _builder.Append(spec != null ? spec.Stacks : 0);
                _builder.Append(" Duration=");
                _builder.Append(definition != null ? definition.DurationPolicy.ToString() : "-");
                _builder.Append(" Triggers=");
                _builder.Append(effect.ActiveTriggers.Count);
                _builder.Append('\n');
            }
        }

        private void AppendTriggerSection(CombatActorState actor)
        {
            _builder.Append("[Trigger: Actor]");
            _builder.Append('\n');
            AppendActiveTriggerList(actor.ActiveTriggers, "  ");

            _builder.Append('\n');
            _builder.Append("[Trigger: AbilityInstances]");
            _builder.Append('\n');
            var abilityPrinted = 0;
            var abilityHasAny = false;
            for (var i = 0; i < actor.ActiveAbilityInstances.Count; i++)
            {
                var triggerList = actor.ActiveAbilityInstances[i].ActiveTriggers;
                for (var j = 0; j < triggerList.Count; j++)
                {
                    abilityHasAny = true;
                    abilityPrinted++;
                    if (abilityPrinted > _maxEntriesPerSection)
                    {
                        _builder.Append("... (truncated)");
                        _builder.Append('\n');
                        i = actor.ActiveAbilityInstances.Count;
                        break;
                    }

                    AppendActiveTriggerLine(triggerList[j], "  [Ability]");
                }
            }

            if (!abilityHasAny)
            {
                _builder.Append("  (none)");
                _builder.Append('\n');
            }

            _builder.Append('\n');
            _builder.Append("[Trigger: Effects]");
            _builder.Append('\n');
            var effectPrinted = 0;
            var effectHasAny = false;
            for (var i = 0; i < actor.ActiveEffects.Count; i++)
            {
                var triggerList = actor.ActiveEffects[i].ActiveTriggers;
                for (var j = 0; j < triggerList.Count; j++)
                {
                    effectHasAny = true;
                    effectPrinted++;
                    if (effectPrinted > _maxEntriesPerSection)
                    {
                        _builder.Append("... (truncated)");
                        _builder.Append('\n');
                        i = actor.ActiveEffects.Count;
                        break;
                    }

                    AppendActiveTriggerLine(triggerList[j], "  [Effect]");
                }
            }

            if (!effectHasAny)
            {
                _builder.Append("  (none)");
                _builder.Append('\n');
            }
        }

        private void AppendActiveTriggerList(System.Collections.Generic.IList<ActiveTriggerInstance> triggerList, string prefix)
        {
            if (triggerList == null || triggerList.Count == 0)
            {
                _builder.Append(prefix);
                _builder.Append("(none)");
                _builder.Append('\n');
                return;
            }

            var printed = 0;
            for (var i = 0; i < triggerList.Count; i++)
            {
                printed++;
                if (printed > _maxEntriesPerSection)
                {
                    _builder.Append("... (truncated)");
                    _builder.Append('\n');
                    break;
                }

                AppendActiveTriggerLine(triggerList[i], prefix);
            }
        }

        private void AppendActiveTriggerLine(ActiveTriggerInstance trigger, string prefix)
        {
            var definition = trigger != null ? trigger.Definition : null;
            _builder.Append(prefix);
            _builder.Append(definition != null ? definition.Id.Value : "(null)");
            if (definition != null && !string.IsNullOrWhiteSpace(definition.Name))
            {
                _builder.Append(" (");
                _builder.Append(definition.Name);
                _builder.Append(')');
            }

            _builder.Append(" En=");
            _builder.Append(trigger != null && trigger.Enabled ? "Y" : "N");
            _builder.Append(" Total=");
            _builder.Append(trigger != null ? trigger.TriggerCountTotal : 0);
            _builder.Append(" Charges=");
            _builder.Append(trigger != null ? trigger.RemainingCharges : 0);
            _builder.Append('\n');
        }

        private bool ResolveProvider()
        {
            if (_provider != null)
            {
                return true;
            }

            if (_runtimeProvider != null)
            {
                _provider = _runtimeProvider as IGasActorRuntimeStateProvider;
                if (_provider != null)
                {
                    return true;
                }
            }

            if (!_autoFindProviderInScene)
            {
                return false;
            }

            var candidates = FindObjectsOfType<MonoBehaviour>(true);
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate is IGasActorRuntimeStateProvider provider)
                {
                    _runtimeProvider = candidate;
                    _provider = provider;
                    return true;
                }
            }

            return false;
        }

        private bool TryBuildActorId(out ActorId actorId)
        {
            actorId = ActorId.Empty;
            if (string.IsNullOrWhiteSpace(_actorId))
            {
                return false;
            }

            try
            {
                actorId = new ActorId(_actorId);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int CountAttributeEntries(CombatActorState actor)
        {
            var count = 0;
            foreach (var _ in actor.Attributes.Entries)
            {
                count++;
            }

            return count;
        }

        private static int CountResourceEntries(CombatActorState actor)
        {
            var count = 0;
            foreach (var _ in actor.Resources.Entries)
            {
                count++;
            }

            return count;
        }

        private static string FormatFp(FP value)
        {
            return ((float)value).ToString("0.##");
        }
    }
}
