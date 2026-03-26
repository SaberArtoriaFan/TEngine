using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Saber.GAS.CustomTriggerActionGenerator
{
    [Generator]
    public sealed class CombatExtensionRegistryGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            context.RegisterSourceOutput(context.CompilationProvider, static (productionContext, compilation) =>
            {
                Execute(productionContext, compilation);
            });
        }

        private static void Execute(SourceProductionContext context, Compilation compilation)
        {
            var customTriggerActionInterface = compilation.GetTypeByMetadataName("Saber.GAS.Triggers.ICombatCustomTriggerAction");
            var actionGateInterface = compilation.GetTypeByMetadataName("Saber.GAS.Runtime.ICombatActionGate");
            var impactMutatorInterface = compilation.GetTypeByMetadataName("Saber.GAS.Runtime.ICombatImpactMutator");
            var impactResolverInterface = compilation.GetTypeByMetadataName("Saber.GAS.Runtime.ICombatImpactResolver");
            var ruleModuleInterface = compilation.GetTypeByMetadataName("Saber.GAS.Runtime.ICombatRuleModule");
            var cloneHandlerInterface = compilation.GetTypeByMetadataName("Saber.GAS.Runtime.ICombatExtensionCloneHandler");
            var assemblyRegistryInterface = compilation.GetTypeByMetadataName("Saber.GAS.Runtime.ICombatAssemblyExtensionRegistry");

            if (customTriggerActionInterface == null &&
                actionGateInterface == null &&
                impactMutatorInterface == null &&
                impactResolverInterface == null &&
                ruleModuleInterface == null &&
                cloneHandlerInterface == null)
            {
                return;
            }

            var allConstructibleTypes = new List<INamedTypeSymbol>();
            CollectConstructibleTypes(compilation.Assembly.GlobalNamespace, allConstructibleTypes);
            allConstructibleTypes.Sort(static (left, right) => string.CompareOrdinal(
                GetTypeKey(left),
                GetTypeKey(right)));

            var customActionTypes = FilterInterfaceImplementations(allConstructibleTypes, customTriggerActionInterface);
            var actionGateTypes = FilterInterfaceImplementations(allConstructibleTypes, actionGateInterface);
            var impactMutatorTypes = FilterInterfaceImplementations(allConstructibleTypes, impactMutatorInterface);
            var impactResolverTypes = FilterInterfaceImplementations(allConstructibleTypes, impactResolverInterface);
            var ruleModuleTypes = FilterInterfaceImplementations(allConstructibleTypes, ruleModuleInterface);
            var cloneHandlerTypes = FilterInterfaceImplementations(allConstructibleTypes, cloneHandlerInterface);

            var generateCustomRegistry = customActionTypes.Count > 0 && customTriggerActionInterface != null;
            var generateAssemblyRegistry = assemblyRegistryInterface != null &&
                                           (actionGateTypes.Count > 0 ||
                                            impactMutatorTypes.Count > 0 ||
                                            impactResolverTypes.Count > 0 ||
                                            ruleModuleTypes.Count > 0 ||
                                            cloneHandlerTypes.Count > 0);
            if (!generateCustomRegistry && !generateAssemblyRegistry)
            {
                return;
            }

            var instanceTypes = CollectInstanceTypes(
                customActionTypes,
                actionGateTypes,
                impactMutatorTypes,
                impactResolverTypes,
                ruleModuleTypes,
                cloneHandlerTypes);

            var assemblyName = SanitizeIdentifier(compilation.AssemblyName ?? "UnknownAssembly");
            var customRegistryName = $"{assemblyName}GeneratedCombatCustomTriggerActionRegistry";
            var assemblyRegistryName = $"{assemblyName}GeneratedCombatAssemblyExtensionRegistry";
            var bootstrapName = $"{assemblyName}GeneratedCombatExtensionBootstrap";
            var instanceClassName = $"{assemblyName}GeneratedCombatExtensionInstances";

            context.AddSource(
                $"{assemblyName}.GeneratedCombatExtensions.g.cs",
                SourceText.From(
                    GenerateRegistrySource(
                        customRegistryName,
                        assemblyRegistryName,
                        bootstrapName,
                        instanceClassName,
                        instanceTypes,
                        customActionTypes,
                        actionGateTypes,
                        impactMutatorTypes,
                        impactResolverTypes,
                        ruleModuleTypes,
                        cloneHandlerTypes,
                        generateCustomRegistry,
                        generateAssemblyRegistry),
                    Encoding.UTF8));

            if (!HasAccessibleModuleInitializerAttribute(compilation))
            {
                context.AddSource(
                    "ModuleInitializerAttribute.g.cs",
                    SourceText.From(GenerateModuleInitializerAttributeSource(), Encoding.UTF8));
            }
        }

        private static bool HasAccessibleModuleInitializerAttribute(Compilation compilation)
        {
            const string metadataName = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

            var moduleInitializerAttribute = compilation.GetTypeByMetadataName(metadataName);
            if (moduleInitializerAttribute != null && moduleInitializerAttribute.DeclaredAccessibility == Accessibility.Public)
            {
                return true;
            }

            foreach (var referencedAssembly in compilation.SourceModule.ReferencedAssemblySymbols)
            {
                var referencedAttribute = referencedAssembly.GetTypeByMetadataName(metadataName);
                if (referencedAttribute != null && referencedAttribute.DeclaredAccessibility == Accessibility.Public)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CollectConstructibleTypes(INamespaceSymbol namespaceSymbol, ICollection<INamedTypeSymbol> results)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamespaceSymbol childNamespace)
                {
                    CollectConstructibleTypes(childNamespace, results);
                    continue;
                }

                if (member is INamedTypeSymbol typeSymbol)
                {
                    CollectConstructibleTypes(typeSymbol, results);
                }
            }
        }

        private static void CollectConstructibleTypes(INamedTypeSymbol typeSymbol, ICollection<INamedTypeSymbol> results)
        {
            if (IsConstructibleType(typeSymbol))
            {
                results.Add(typeSymbol);
            }

            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                CollectConstructibleTypes(nestedType, results);
            }
        }

        private static bool IsConstructibleType(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.TypeKind != TypeKind.Class ||
                typeSymbol.IsAbstract ||
                typeSymbol.IsGenericType)
            {
                return false;
            }

            if (typeSymbol.DeclaredAccessibility != Accessibility.Public &&
                typeSymbol.DeclaredAccessibility != Accessibility.Internal)
            {
                return false;
            }

            foreach (var constructor in typeSymbol.InstanceConstructors)
            {
                if (constructor.DeclaredAccessibility == Accessibility.Private)
                {
                    continue;
                }

                if (constructor.Parameters.Length == 0 || constructor.Parameters.All(static parameter => parameter.IsOptional))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<INamedTypeSymbol> FilterInterfaceImplementations(
            IReadOnlyList<INamedTypeSymbol> candidates,
            INamedTypeSymbol? interfaceSymbol)
        {
            var results = new List<INamedTypeSymbol>();
            if (interfaceSymbol == null)
            {
                return results;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (ImplementsInterface(candidate, interfaceSymbol))
                {
                    results.Add(candidate);
                }
            }

            return results;
        }

        private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, INamedTypeSymbol interfaceSymbol)
        {
            foreach (var implementedInterface in typeSymbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(implementedInterface, interfaceSymbol))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<INamedTypeSymbol> CollectInstanceTypes(params IReadOnlyList<INamedTypeSymbol>[] groups)
        {
            var results = new List<INamedTypeSymbol>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (var groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group == null)
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < group.Count; typeIndex++)
                {
                    var typeSymbol = group[typeIndex];
                    var typeKey = GetTypeKey(typeSymbol);
                    if (!seen.Add(typeKey))
                    {
                        continue;
                    }

                    results.Add(typeSymbol);
                }
            }

            results.Sort(static (left, right) => string.CompareOrdinal(GetTypeKey(left), GetTypeKey(right)));
            return results;
        }

        private static string GenerateRegistrySource(
            string customRegistryName,
            string assemblyRegistryName,
            string bootstrapName,
            string instanceClassName,
            IReadOnlyList<INamedTypeSymbol> instanceTypes,
            IReadOnlyList<INamedTypeSymbol> customActionTypes,
            IReadOnlyList<INamedTypeSymbol> actionGateTypes,
            IReadOnlyList<INamedTypeSymbol> impactMutatorTypes,
            IReadOnlyList<INamedTypeSymbol> impactResolverTypes,
            IReadOnlyList<INamedTypeSymbol> ruleModuleTypes,
            IReadOnlyList<INamedTypeSymbol> cloneHandlerTypes,
            bool generateCustomRegistry,
            bool generateAssemblyRegistry)
        {
            var builder = new StringBuilder();
            var instanceLookup = BuildInstanceLookup(instanceTypes);

            builder.AppendLine("using System;");
            builder.AppendLine("using System.Collections.Generic;");
            builder.AppendLine("using System.Runtime.CompilerServices;");
            builder.AppendLine();
            builder.AppendLine("namespace Saber.GAS.Generated");
            builder.AppendLine("{");

            AppendInstanceContainer(builder, instanceClassName, instanceTypes);

            if (generateCustomRegistry)
            {
                AppendCustomTriggerActionRegistry(builder, customRegistryName, instanceClassName, instanceLookup, customActionTypes);
            }

            if (generateAssemblyRegistry)
            {
                AppendAssemblyExtensionRegistry(
                    builder,
                    assemblyRegistryName,
                    instanceClassName,
                    instanceLookup,
                    actionGateTypes,
                    impactMutatorTypes,
                    impactResolverTypes,
                    ruleModuleTypes,
                    cloneHandlerTypes);
            }

            AppendBootstrap(builder, customRegistryName, assemblyRegistryName, bootstrapName, generateCustomRegistry, generateAssemblyRegistry);

            builder.AppendLine("}");
            return builder.ToString();
        }

        private static Dictionary<string, int> BuildInstanceLookup(IReadOnlyList<INamedTypeSymbol> instanceTypes)
        {
            var lookup = new Dictionary<string, int>(instanceTypes.Count, StringComparer.Ordinal);
            for (var i = 0; i < instanceTypes.Count; i++)
            {
                lookup[GetTypeKey(instanceTypes[i])] = i;
            }

            return lookup;
        }

        private static void AppendInstanceContainer(
            StringBuilder builder,
            string instanceClassName,
            IReadOnlyList<INamedTypeSymbol> instanceTypes)
        {
            builder.Append("    internal static class ").Append(instanceClassName).AppendLine();
            builder.AppendLine("    {");

            for (var i = 0; i < instanceTypes.Count; i++)
            {
                builder.Append("        internal static readonly ")
                    .Append(instanceTypes[i].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .Append(" Instance_")
                    .Append(i)
                    .Append(" = new ")
                    .Append(instanceTypes[i].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                    .AppendLine("();");
            }

            if (instanceTypes.Count == 0)
            {
                builder.AppendLine("        internal static readonly object Empty = null;");
            }

            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendCustomTriggerActionRegistry(
            StringBuilder builder,
            string registryName,
            string instanceClassName,
            IReadOnlyDictionary<string, int> instanceLookup,
            IReadOnlyList<INamedTypeSymbol> customActionTypes)
        {
            builder.Append("    internal sealed class ").Append(registryName)
                .AppendLine(" : global::Saber.GAS.Triggers.ICombatCustomTriggerActionRegistry");
            builder.AppendLine("    {");
            builder.AppendLine("        private static readonly global::Saber.GAS.Triggers.ICombatCustomTriggerAction[] Actions =");
            builder.AppendLine("        {");

            AppendInterfaceArrayEntries(builder, instanceClassName, instanceLookup, customActionTypes);

            builder.AppendLine("        };");
            builder.AppendLine();
            builder.AppendLine("        private static readonly Dictionary<int, global::Saber.GAS.Triggers.ICombatCustomTriggerAction> Lookup = BuildLookup();");
            builder.AppendLine("        private static readonly int[] CustomActionIds = BuildCustomActionIds();");
            builder.AppendLine();
            builder.AppendLine("        public IReadOnlyList<int> RegisteredCustomActionIds => CustomActionIds;");
            builder.AppendLine();
            builder.AppendLine("        public bool TryResolve(int customActionId, out global::Saber.GAS.Triggers.ICombatCustomTriggerAction action)");
            builder.AppendLine("        {");
            builder.AppendLine("            return Lookup.TryGetValue(customActionId, out action);");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static Dictionary<int, global::Saber.GAS.Triggers.ICombatCustomTriggerAction> BuildLookup()");
            builder.AppendLine("        {");
            builder.AppendLine("            var lookup = new Dictionary<int, global::Saber.GAS.Triggers.ICombatCustomTriggerAction>(Actions.Length);");
            builder.AppendLine("            for (var i = 0; i < Actions.Length; i++)");
            builder.AppendLine("            {");
            builder.AppendLine("                var action = Actions[i];");
            builder.AppendLine("                if (action == null)");
            builder.AppendLine("                {");
            builder.AppendLine("                    continue;");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                if (action.CustomId <= 0)");
            builder.AppendLine("                {");
            builder.AppendLine("                    throw new InvalidOperationException($\"Custom trigger action '{action.GetType().FullName}' must expose a positive CustomId.\");");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                if (lookup.ContainsKey(action.CustomId))");
            builder.AppendLine("                {");
            builder.Append("                    throw new InvalidOperationException($\"Duplicate custom trigger action id '{action.CustomId}' detected inside assembly registry '");
            builder.Append(registryName);
            builder.AppendLine("'.\");");
            builder.AppendLine("                }");
            builder.AppendLine();
            builder.AppendLine("                lookup.Add(action.CustomId, action);");
            builder.AppendLine("            }");
            builder.AppendLine();
            builder.AppendLine("            return lookup;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        private static int[] BuildCustomActionIds()");
            builder.AppendLine("        {");
            builder.AppendLine("            var ids = new int[Lookup.Count];");
            builder.AppendLine("            Lookup.Keys.CopyTo(ids, 0);");
            builder.AppendLine("            Array.Sort(ids);");
            builder.AppendLine("            return ids;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendAssemblyExtensionRegistry(
            StringBuilder builder,
            string registryName,
            string instanceClassName,
            IReadOnlyDictionary<string, int> instanceLookup,
            IReadOnlyList<INamedTypeSymbol> actionGateTypes,
            IReadOnlyList<INamedTypeSymbol> impactMutatorTypes,
            IReadOnlyList<INamedTypeSymbol> impactResolverTypes,
            IReadOnlyList<INamedTypeSymbol> ruleModuleTypes,
            IReadOnlyList<INamedTypeSymbol> cloneHandlerTypes)
        {
            builder.Append("    internal sealed class ").Append(registryName)
                .AppendLine(" : global::Saber.GAS.Runtime.ICombatAssemblyExtensionRegistry");
            builder.AppendLine("    {");
            AppendRuntimeRegistryArray(builder, "ActionGateItems", "global::Saber.GAS.Runtime.ICombatActionGate", instanceClassName, instanceLookup, actionGateTypes);
            AppendRuntimeRegistryArray(builder, "ImpactMutatorItems", "global::Saber.GAS.Runtime.ICombatImpactMutator", instanceClassName, instanceLookup, impactMutatorTypes);
            AppendRuntimeRegistryArray(builder, "ImpactResolverItems", "global::Saber.GAS.Runtime.ICombatImpactResolver", instanceClassName, instanceLookup, impactResolverTypes);
            AppendRuntimeRegistryArray(builder, "RuleModuleItems", "global::Saber.GAS.Runtime.ICombatRuleModule", instanceClassName, instanceLookup, ruleModuleTypes);
            AppendRuntimeRegistryArray(builder, "ExtensionCloneHandlerItems", "global::Saber.GAS.Runtime.ICombatExtensionCloneHandler", instanceClassName, instanceLookup, cloneHandlerTypes);
            builder.AppendLine();
            builder.AppendLine("        public IReadOnlyList<global::Saber.GAS.Runtime.ICombatActionGate> ActionGates => ActionGateItems;");
            builder.AppendLine("        public IReadOnlyList<global::Saber.GAS.Runtime.ICombatImpactMutator> ImpactMutators => ImpactMutatorItems;");
            builder.AppendLine("        public IReadOnlyList<global::Saber.GAS.Runtime.ICombatImpactResolver> ImpactResolvers => ImpactResolverItems;");
            builder.AppendLine("        public IReadOnlyList<global::Saber.GAS.Runtime.ICombatRuleModule> RuleModules => RuleModuleItems;");
            builder.AppendLine("        public IReadOnlyList<global::Saber.GAS.Runtime.ICombatExtensionCloneHandler> ExtensionCloneHandlers => ExtensionCloneHandlerItems;");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static void AppendRuntimeRegistryArray(
            StringBuilder builder,
            string fieldName,
            string interfaceTypeName,
            string instanceClassName,
            IReadOnlyDictionary<string, int> instanceLookup,
            IReadOnlyList<INamedTypeSymbol> types)
        {
            builder.Append("        private static readonly ")
                .Append(interfaceTypeName)
                .Append("[] ")
                .Append(fieldName)
                .AppendLine(" =");
            builder.AppendLine("        {");
            AppendInterfaceArrayEntries(builder, instanceClassName, instanceLookup, types);
            builder.AppendLine("        };");
        }

        private static void AppendInterfaceArrayEntries(
            StringBuilder builder,
            string instanceClassName,
            IReadOnlyDictionary<string, int> instanceLookup,
            IReadOnlyList<INamedTypeSymbol> types)
        {
            for (var i = 0; i < types.Count; i++)
            {
                var typeKey = GetTypeKey(types[i]);
                var index = instanceLookup[typeKey];
                builder.Append("            ")
                    .Append(instanceClassName)
                    .Append(".Instance_")
                    .Append(index)
                    .AppendLine(",");
            }
        }

        private static void AppendBootstrap(
            StringBuilder builder,
            string customRegistryName,
            string assemblyRegistryName,
            string bootstrapName,
            bool generateCustomRegistry,
            bool generateAssemblyRegistry)
        {
            builder.Append("    internal static class ").Append(bootstrapName).AppendLine();
            builder.AppendLine("    {");

            if (generateCustomRegistry)
            {
                builder.Append("        private static readonly global::Saber.GAS.Triggers.ICombatCustomTriggerActionRegistry CustomTriggerActionRegistry = new ")
                    .Append(customRegistryName)
                    .AppendLine("();");
            }

            if (generateAssemblyRegistry)
            {
                builder.Append("        private static readonly global::Saber.GAS.Runtime.ICombatAssemblyExtensionRegistry AssemblyExtensionRegistry = new ")
                    .Append(assemblyRegistryName)
                    .AppendLine("();");
            }

            if (generateCustomRegistry || generateAssemblyRegistry)
            {
                builder.AppendLine();
            }

            builder.AppendLine("        [ModuleInitializer]");
            builder.AppendLine("        internal static void Register()");
            builder.AppendLine("        {");

            if (generateCustomRegistry)
            {
                builder.AppendLine("            global::Saber.GAS.Triggers.CombatCustomTriggerActionRegistryHub.Register(CustomTriggerActionRegistry);");
            }

            if (generateAssemblyRegistry)
            {
                builder.AppendLine("            global::Saber.GAS.Runtime.CombatAssemblyExtensionRegistryHub.Register(AssemblyExtensionRegistry);");
            }

            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine();
        }

        private static string GenerateModuleInitializerAttributeSource()
        {
            return @"namespace System.Runtime.CompilerServices
{
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, Inherited = false)]
    public sealed class ModuleInitializerAttribute : global::System.Attribute
    {
    }
}";
        }

        private static string GetTypeKey(INamedTypeSymbol symbol)
        {
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        }

        private static string SanitizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "UnknownAssembly";
            }

            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                if ((character >= 'a' && character <= 'z') ||
                    (character >= 'A' && character <= 'Z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_')
                {
                    builder.Append(character);
                    continue;
                }

                builder.Append('_');
            }

            if (builder.Length == 0)
            {
                return "UnknownAssembly";
            }

            if (builder[0] >= '0' && builder[0] <= '9')
            {
                builder.Insert(0, '_');
            }

            return builder.ToString();
        }
    }
}
