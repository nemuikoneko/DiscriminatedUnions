using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace DiscriminatedUnions
{
    [Generator]
    public sealed class SourceGenerator : ISourceGenerator
    {
        public void Execute(GeneratorExecutionContext context)
        {
            context.Compilation.SyntaxTrees.ToList().ForEach(syntaxTree => GenerateUnions(context, syntaxTree));
        }

        public void Initialize(GeneratorInitializationContext context)
        {
        }

        private static void GenerateUnions(GeneratorExecutionContext context, SyntaxTree syntaxTree)
        {
            var model = context.Compilation.GetSemanticModel(syntaxTree);

            bool HasUnionInterface(StructDeclarationSyntax structDeclNode)
            {
                var structDeclType = model.GetDeclaredSymbol(structDeclNode) ?? throw new Exception("Failed to retrieve type symbol for struct declaration");
                return structDeclType.Interfaces.Any(i => i.Name == "IDiscriminatedUnion");
            }

            bool IsEligibleUnion(StructDeclarationSyntax structDeclNode)
            {
                if (!structDeclNode.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)))
                    return false;

                if (!HasUnionInterface(structDeclNode))
                    return false;

                return true;
            }

            static bool UnionHasCases(Union union) => union.Cases.Count != 0;

            syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<StructDeclarationSyntax>()
                .Where(IsEligibleUnion)
                .Select(structDeclNode => BuildUnion(model, structDeclNode))
                .Where(UnionHasCases)
                .ToList()
                .ForEach(union => CodeGenerator.GenerateUnionSourceFile(context, union));
        }

        private static Union BuildUnion(SemanticModel model, StructDeclarationSyntax structDeclNode)
        {
            var structDeclSymbol = model.GetDeclaredSymbol(structDeclNode) ?? throw new Exception("Failed to retrieve symbol for struct declaration");

            var name = structDeclNode.Identifier.Text;
            var type = TypeInfo.FromNamedTypeSymbol(structDeclSymbol);
            var cases = GatherUnionCases(model, structDeclNode);

            return new Union(name: name, type: type, cases: cases);
        }

        private static IReadOnlyList<UnionCase> GatherUnionCases(SemanticModel model, StructDeclarationSyntax structDeclNode)
            => structDeclNode
                .DescendantNodes()
                .OfType<InterfaceDeclarationSyntax>()
                .Where(interfaceDeclNode => interfaceDeclNode.Identifier.Text == "Cases")
                .Select(interfaceDeclNode =>
                {
                    return interfaceDeclNode
                        .Members
                        .OfType<MethodDeclarationSyntax>()
                        .Where(methodDeclNode => (methodDeclNode.ReturnType as PredefinedTypeSyntax)?.Keyword.Text == "void")
                        .Select(methodDeclNode =>
                        {
                            var caseName = methodDeclNode.Identifier.Text;

                            var caseParameters = methodDeclNode.ParameterList.Parameters.Select(parameterNode =>
                            {
                                var paramSymbol = model.GetDeclaredSymbol(parameterNode) ?? throw new Exception("Failed to retrieve parameter symbol");

                                return new UnionCaseParameter(
                                    name: parameterNode.Identifier.Text,
                                    type: TypeInfo.FromParameterSymbol(paramSymbol));
                            }).ToList();

                            return new UnionCase(name: caseName, parameters: caseParameters);
                        })
                        .ToList();
                })
                .Single();

        internal readonly struct UnionCase
        {
            internal UnionCase(
                string name,
                IReadOnlyList<UnionCaseParameter> parameters)
            {
                Name = name;
                Parameters = parameters;
            }

            internal string Name { get; }

            internal IReadOnlyList<UnionCaseParameter> Parameters { get; }
        }

        internal readonly struct UnionCaseParameter
        {
            internal UnionCaseParameter(
                string name,
                TypeInfo type)
            {
                Name = name;
                Type = type;
            }

            internal string Name { get; }

            internal TypeInfo Type { get; }
        }

        internal readonly struct Union
        {
            internal Union(
                string name,
                TypeInfo type,
                IReadOnlyList<UnionCase> cases)
            {
                Name = name;
                Cases = cases;
                Type = type;
            }

            internal string Name { get; }

            internal IReadOnlyList<UnionCase> Cases { get; }

            internal TypeInfo Type { get; }
        }

        internal readonly struct TypeInfo
        {
            private readonly ITypeSymbol _typeSymbol;
            private readonly string _name;
            private readonly TypeKind _typeKind;
            private readonly Accessibility _accessibility;
            private readonly bool _isPartial;
            private readonly bool _isReadOnly;
            private readonly bool _isStatic;
            private readonly bool _isTypeParameter;
            private readonly IReadOnlyList<ITypeParameterSymbol> _typeParameters;

            internal static TypeInfo FromNamedTypeSymbol(INamedTypeSymbol namedTypeSymbol)
            {
                return FromTypeSymbol(namedTypeSymbol, typeParameters: namedTypeSymbol.TypeParameters.ToList());
            }

            internal static TypeInfo FromParameterSymbol(IParameterSymbol parameterSymbol)
            {
                var typeSymbol = parameterSymbol.Type;
                var isTypeParameter = typeSymbol.TypeKind == TypeKind.TypeParameter;
                return FromTypeSymbol(typeSymbol, isTypeParameter: isTypeParameter);
            }

            private static TypeInfo FromTypeSymbol(
                ITypeSymbol typeSymbol,
                bool isTypeParameter = false,
                IReadOnlyList<ITypeParameterSymbol>? typeParameters = null)
            {
                var isPartial = typeSymbol.TypeKind switch
                {
                    TypeKind.TypeParameter => false,
                    _ => typeSymbol.IsPartial() ?? false
                };

                return new(
                    typeSymbol: typeSymbol,
                    name: typeSymbol.Name,
                    typeKind: typeSymbol.TypeKind,
                    accessibility: typeSymbol.DeclaredAccessibility,
                    @namespace: typeSymbol.ContainingNamespace,
                    isPartial: isPartial,
                    isReadOnly: typeSymbol.IsReadOnly,
                    isStatic: typeSymbol.IsStatic,
                    isTypeParameter: isTypeParameter,
                    typeParameters: typeParameters ?? new List<ITypeParameterSymbol>(),
                    parentTypes: GatherParentTypes(typeSymbol));
            }

            private TypeInfo(
                ITypeSymbol typeSymbol,
                string name,
                INamespaceSymbol @namespace,
                TypeKind typeKind,
                Accessibility accessibility,
                IReadOnlyList<TypeInfo> parentTypes,
                bool isPartial,
                bool isReadOnly,
                bool isStatic,
                bool isTypeParameter,
                IReadOnlyList<ITypeParameterSymbol> typeParameters)
            {
                _typeSymbol = typeSymbol;
                _name = name;
                _typeKind = typeKind;
                _accessibility = accessibility;
                _isPartial = isPartial;
                _isReadOnly = isReadOnly;
                _isStatic = isStatic;
                _isTypeParameter = isTypeParameter;
                _typeParameters = typeParameters;
                Namespace = @namespace;
                ParentTypes = parentTypes;
            }

            internal INamespaceSymbol Namespace { get; }

            internal IReadOnlyList<TypeInfo> ParentTypes { get; }

            internal string ToDeclarationString()
            {
                if (_isTypeParameter)
                    throw new InvalidOperationException("Type is a type parameter and as such has no declaration");

                var partialStr = _isPartial ? "partial " : "";
                var readonlyStr = _isReadOnly ? "readonly " : "";
                var staticStr = _isStatic ? "static " : "";

                var accessibilityStr = _accessibility switch
                {
                    Accessibility.Private => "private",
                    Accessibility.Protected => "protected",
                    Accessibility.Internal => "internal",
                    Accessibility.Public => "public",
                    _ => throw new Exception("Unsupported accessibility modifier"),
                };

                var typeKindStr = _typeKind switch
                {
                    TypeKind.Class => "class",
                    TypeKind.Struct => "struct",
                    TypeKind.Interface => "interface",
                    _ => throw new Exception("Unsupported type kind"),
                };

                var typeParamsStr = _typeParameters.Count != 0 ? $"<{_typeParameters.Select(tp => tp.Name).Join(", ")}>" : "";

                return $"{accessibilityStr} {staticStr}{readonlyStr}{partialStr}{typeKindStr} {_name}{typeParamsStr}".Replace("  ", " ");
            }

            internal string QualifiedName
            {
                get
                {
                    if (_isTypeParameter)
                    {
                        return _name;
                    }
                    else
                    {
                        return _typeSymbol.ToDisplayString(
                            new SymbolDisplayFormat(
                                typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
                                genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters));
                    }
                }
            }

            public override string ToString() => QualifiedName;

            private static IReadOnlyList<TypeInfo> GatherParentTypes(ISymbol symbol)
            {
                var parentTypes = new List<TypeInfo>();

                while (true)
                {
                    var parentSymbol = symbol.ContainingSymbol;
                    var parentType = symbol.ContainingType;

                    if (parentType == null)
                        break;

                    parentTypes.Add(FromNamedTypeSymbol(parentType));
                    symbol = parentSymbol;
                }

                parentTypes.Reverse();
                return parentTypes;
            }
        }
    }
}