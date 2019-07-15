using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Shared.Extensions;

namespace KC.Actin.Analyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ActinAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "Actin";

        private static DiagnosticDescriptor Rule_ActorTypeNeedsAttribute = new DiagnosticDescriptor(DiagnosticId,
            title: "Type Needs Attribute",
            messageFormat: "Type {0} should use the PeerAttribute or the SingletonAttribute, or it will not be automatically run.",
            category : "Actin",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault:
            true,
            description: "The Description");

        private static DiagnosticDescriptor Rule_ActorMemberNeedsAttribute = new DiagnosticDescriptor(DiagnosticId,
            title: "Member Needs Attribute",
            messageFormat: "Member {0} should use the PeerAttribute or the SingletonAttribute, or it will remain null indefinitely.",
            category: "Actin",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault:
            true,
            description: "The Description");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule_ActorTypeNeedsAttribute, Rule_ActorMemberNeedsAttribute); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType, SymbolKind.Field, SymbolKind.Property);
        }
       
        private static void AnalyzeSymbol(SymbolAnalysisContext context)
        {
            switch (context.Symbol) {
                case INamedTypeSymbol named:
                    //Class definition:
                    if (named.ExtendsActor(context) && !named.GetAttributes().IncludeSingletonOrInstance()) { 
                            var diagnostic = Diagnostic.Create(Rule_ActorTypeNeedsAttribute, named.Locations[0], named.Name);
                            context.ReportDiagnostic(diagnostic);
                    }
                    break;
                case IFieldSymbol field:
                    //throw new Exception($"{field.Name} :: {field.Type.ExtendsActor(context)}");
                    if (field.ContainingType.ExtendsActor(context) && field.Type.ExtendsActor(context) && !field.GetAttributes().IncludeSingletonOrInstanceOrPeer()) {
                            var diagnostic = Diagnostic.Create(Rule_ActorMemberNeedsAttribute, field.Locations[0], field.Name);
                            context.ReportDiagnostic(diagnostic);
                    }
                    break;
                case IPropertySymbol property:
                    if (property.ContainingType.ExtendsActor(context) && property.Type.ExtendsActor(context) && !property.GetAttributes().IncludeSingletonOrInstanceOrPeer()) {
                            var diagnostic = Diagnostic.Create(Rule_ActorMemberNeedsAttribute, property.Locations[0], property.Name);
                            context.ReportDiagnostic(diagnostic);
                    }
                    break;
            }
            
        }
    }

    public static class AnalyzerExtensions {
        private static string actorSansTypeName = $"{typeof(Actor_SansType).FullName}";
        public static bool ExtendsActor(this ITypeSymbol symbol, SymbolAnalysisContext context) {
            if (symbol == null || symbol.IsValueType || symbol.SpecialType != SpecialType.None || symbol.IsAbstract) {
                return false;
            }
            var actor = context.Compilation.GetTypeByMetadataName(actorSansTypeName);
            if (actor == null) {
                return false;
            }
            var conversionInfo = context.Compilation.ClassifyCommonConversion(symbol, actor);
            if (conversionInfo.IsImplicit) {
                return true;
            }
            return false;
        }

        public static bool IncludeSingletonOrInstance(this ImmutableArray<AttributeData> attributes) {
            return attributes.Any(x =>
                x.AttributeClass.Name.Equals(nameof(SingletonAttribute))
                || x.AttributeClass.Name.Equals(nameof(InstanceAttribute)));
        }

        public static bool IncludeSingletonOrInstanceOrPeer(this ImmutableArray<AttributeData> attributes) {
            return attributes.Any(x =>
                x.AttributeClass.Name.Equals(nameof(SingletonAttribute))
                || x.AttributeClass.Name.Equals(nameof(PeerAttribute))
                || x.AttributeClass.Name.Equals(nameof(InstanceAttribute)));
        }

    }
}
