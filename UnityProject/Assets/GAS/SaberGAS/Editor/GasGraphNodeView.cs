using System;
using System.Collections.Generic;
using Saber.GAS.Authoring;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace Saber.GAS.Editor
{
    internal sealed class GasGraphNodeView : Node
    {
        private readonly Dictionary<GasGraphRelationKind, Port> _outputPorts = new Dictionary<GasGraphRelationKind, Port>();

        public GasGraphNodeView(
            UnityEngine.Object asset,
            Action<UnityEngine.Object> onSelected,
            Action<UnityEngine.Object> onDoubleClicked)
        {
            Asset = asset;
            title = asset == null ? "Unknown" : asset.name;
            viewDataKey = asset == null ? Guid.NewGuid().ToString() : asset.GetInstanceID().ToString();

            var summary = new Label(BuildSummary(asset));
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.color = new Color(0.84f, 0.86f, 0.9f, 1f);
            extensionContainer.Add(summary);

            var inputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, asset == null ? typeof(UnityEngine.Object) : asset.GetType());
            inputPort.portName = "In";
            inputPort.userData = new GasGraphPortData(asset, GasGraphRelationKind.None, asset == null ? typeof(UnityEngine.Object) : asset.GetType(), true);
            inputContainer.Add(inputPort);

            CreateOutputPorts(asset);

            RefreshExpandedState();
            RefreshPorts();

            RegisterCallback<MouseDownEvent>(evt =>
            {
                if (Asset == null || evt.button != (int)MouseButton.LeftMouse)
                {
                    return;
                }

                onSelected?.Invoke(Asset);
                if (evt.clickCount >= 2)
                {
                    onDoubleClicked?.Invoke(Asset);
                }
            });
        }

        public UnityEngine.Object Asset { get; }

        public Port GetOutputPort(GasGraphRelationKind kind)
        {
            return _outputPorts.TryGetValue(kind, out var port) ? port : null;
        }

        private void CreateOutputPorts(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case AbilityDefinitionAsset:
                    AddOutputPort("Effects", GasGraphRelationKind.AbilityEffects, typeof(EffectDefinitionAsset));
                    AddOutputPort("Periodic", GasGraphRelationKind.AbilityPeriodicEffects, typeof(EffectDefinitionAsset));
                    AddOutputPort("End", GasGraphRelationKind.AbilityEndEffects, typeof(EffectDefinitionAsset));
                    AddOutputPort("Triggers", GasGraphRelationKind.AbilityTriggers, typeof(TriggerDefinitionAsset));
                    break;
                case EffectDefinitionAsset:
                    AddOutputPort("Removed", GasGraphRelationKind.EffectRemovedTargetEffects, typeof(EffectDefinitionAsset));
                    AddOutputPort("Triggers", GasGraphRelationKind.EffectTriggers, typeof(TriggerDefinitionAsset));
                    break;
                case CombatActorTemplateAsset:
                    AddOutputPort("Abilities", GasGraphRelationKind.ActorGrantedAbilities, typeof(AbilityDefinitionAsset));
                    AddOutputPort("Triggers", GasGraphRelationKind.ActorTriggers, typeof(TriggerDefinitionAsset));
                    break;
                case TriggerDefinitionAsset:
                    AddOutputPort("ApplyEffect", GasGraphRelationKind.TriggerApplyEffect, typeof(EffectDefinitionAsset));
                    AddOutputPort("Activate", GasGraphRelationKind.TriggerActivateAbility, typeof(AbilityDefinitionAsset));
                    AddOutputPort("Cancel", GasGraphRelationKind.TriggerCancelAbility, typeof(AbilityDefinitionAsset));
                    break;
            }
        }

        private void AddOutputPort(string label, GasGraphRelationKind kind, Type targetType)
        {
            var port = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, targetType);
            port.portName = label;
            port.userData = new GasGraphPortData(Asset, kind, targetType, false);
            outputContainer.Add(port);
            _outputPorts[kind] = port;
        }

        private static string BuildSummary(UnityEngine.Object asset)
        {
            return asset switch
            {
                AbilityDefinitionAsset => "Ability",
                EffectDefinitionAsset => "Effect",
                TriggerDefinitionAsset => "Trigger",
                CombatActorTemplateAsset => "Actor Template",
                _ => "Asset"
            };
        }
    }
}
