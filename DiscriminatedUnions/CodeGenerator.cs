using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static nemuikoneko.DiscriminatedUnions.SourceGenerator;

namespace nemuikoneko.DiscriminatedUnions
{
    internal static class CodeGenerator
    {
        internal const string MatchMethodName = "Match";
        internal const string MatchWithDefaultMethodName = "MatchWithDefault";

        internal static void GenerateUnionSourceFile(GeneratorExecutionContext context, Union union)
        {
            var sb = new IndentableStringBuilder();

            sb.Append("// Auto-generated");
            sb.Append("#nullable enable");
            sb.Append();

            if (!union.Type.Namespace.IsGlobalNamespace)
            {
                sb.Append($"namespace {union.Type.Namespace};");
                sb.Append();
            }

            union.Type.ParentTypes.ForEach(parentType =>
            {
                sb.Append(parentType.ToDeclarationString());
                sb.Append("{");
                sb.ManualIndent();
            });

            sb.Append(union.Type.ToDeclarationString() + " : " + BuildInterfaceImplementationList(union));
            sb.Append("{");
            sb.Indent(() =>
            {
                AppendTagEnum(sb, union);
                sb.Append();

                AppendFields(sb);
                sb.Append();

                AppendConstructor(sb, union);
                sb.Append();

                AppendCaseConstructors(sb, union);
                sb.Append();

                AppendMatchMethod(sb, union);
                sb.Append();

                if (union.Cases.Count > 1)
                {
                    AppendMatchWithDefaultMethod(sb, union);
                    sb.Append();
                }

                AppendEqualityMethods(sb, union);
            });
            sb.Append("}");

            union.Type.ParentTypes.ForEach(_ =>
            {
                sb.ManualReverseIndent();
                sb.Append("}");
            });

            var source = sb.ToString();

            var reformattedTypeName = union.Type.QualifiedName
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace(',', '_');
            var fileName = $"{reformattedTypeName}.g.cs";

            context.AddSource(fileName, source);
        }

        private static void AppendEqualityMethods(IndentableStringBuilder sb, Union union)
        {
            sb.Append(BuildTypeSpecificEqualsMethod(union));
            sb.Append(BuildGenericEqualsMethod(union));
            sb.Append(BuildGetHashCodeMethod());
            sb.Append(BuildEqualsOperatorMethod(union));
            sb.Append(BuildNotEqualsOperatorMethod(union));
        }

        private static void AppendCaseConstructors(IndentableStringBuilder sb, Union union)
        {
            union.Cases.ToList().ForEach(unionCase => sb.Append(BuildCaseConstructor(union, unionCase)));
        }

        private static void AppendConstructor(IndentableStringBuilder sb, Union union)
        {
            sb.Append($"private {union.Name}(Tag tag, object? data = null)");
            sb.Append("{");
            sb.Indent(() =>
            {
                sb.Append("_tag = tag;");
                sb.Append("_data = data;");
            });
            sb.Append("}");
        }

        private static void AppendFields(IndentableStringBuilder sb)
        {
            sb.Append("private readonly Tag _tag;");
            sb.Append("private readonly object? _data;");
        }

        private static void AppendTagEnum(IndentableStringBuilder sb, Union union)
        {
            sb.Append("private enum Tag : byte");
            sb.Append("{");
            sb.Indent(() =>
            {
                var lastUnionCase = union.Cases.Last();
                union.Cases.TakeWhile(unionCase => !unionCase.Equals(lastUnionCase)).ToList().ForEach(unionCase => sb.Append(unionCase.Name + ","));
                sb.Append(lastUnionCase.Name);
            });
            sb.Append("}");
        }

        private static void AppendMatchMethod(IndentableStringBuilder sb, Union union)
        {
            _AppendMatchMethod(
                sb,
                union,
                matchMethodName: MatchMethodName,
                defaultMethodArgsToNull: false,
                addDefaultMethodArg: false,
                redirectToDefaultBranchIfCaseIsNull: false,
                defaultCaseBody: "throw new System.ArgumentOutOfRangeException();");
        }

        private static void AppendMatchWithDefaultMethod(IndentableStringBuilder sb, Union union)
        {
            _AppendMatchMethod(
                sb,
                union,
                matchMethodName: MatchWithDefaultMethodName,
                defaultMethodArgsToNull: true,
                addDefaultMethodArg: true,
                redirectToDefaultBranchIfCaseIsNull: true,
                defaultCaseBody: "return _();");
        }

        private static void _AppendMatchMethod(
            IndentableStringBuilder sb,
            Union union,
            string matchMethodName,
            bool defaultMethodArgsToNull,
            bool addDefaultMethodArg,
            bool redirectToDefaultBranchIfCaseIsNull,
            string defaultCaseBody)
        {
            sb.Append($"public TResult {matchMethodName}<TResult>(");
            sb.Indent(() =>
            {
                var methodArgs = new List<string>();

                if (addDefaultMethodArg)
                    methodArgs.Add("System.Func<TResult> _");

                methodArgs.AddRange(BuildMatchMethodArgs(union.Cases, defaultToNull: defaultMethodArgsToNull));
                var lastMethodArg = methodArgs.Last();
                methodArgs.TakeWhile(methodArg => methodArg != lastMethodArg).ToList().ForEach(methodArg => sb.Append(methodArg + ","));
                sb.Append(lastMethodArg + ")");
            });
            sb.Append("{");
            sb.Indent(() =>
            {
                sb.Append("switch (_tag)");
                sb.Append("{");
                sb.Indent(() =>
                {
                    BuildMatchMethodSwitchCases(union.Cases, redirectToDefaultBranchIfCaseIsNull: redirectToDefaultBranchIfCaseIsNull).ForEach(switchCase =>
                    {
                        sb.Append(switchCase.caseHeader);
                        sb.Indent(() =>
                        {
                            switchCase.caseBody.ForEach(sb.Append);
                        });
                    });
                    sb.Append("default:");
                    sb.Indent(() =>
                    {
                        sb.Append(defaultCaseBody);
                    });
                });
                sb.Append("}");
            });
            sb.Append("}");
        }

        private static string BuildInterfaceImplementationList(Union union) => $"System.IEquatable<{union.Type.QualifiedName}>";

        private static string BuildTypeSpecificEqualsMethod(Union union)
        {
            return $"public bool Equals({union.Type.QualifiedName} other) => _tag == other._tag && Equals(_data, other._data);";
        }

        private static string BuildGenericEqualsMethod(Union union)
        {
            return $"public override bool Equals(object? obj) => obj is {union.Type.QualifiedName} other && Equals(other);";
        }

        private static string BuildGetHashCodeMethod() => "public override int GetHashCode() => System.HashCode.Combine((int)_tag, _data);";

        private static string BuildEqualsOperatorMethod(Union union)
        {
            return $"public static bool operator ==({union.Type.QualifiedName} left, {union.Type.QualifiedName} right) => left.Equals(right);";
        }

        private static string BuildNotEqualsOperatorMethod(Union union)
        {
            return $"public static bool operator !=({union.Type.QualifiedName} left, {union.Type.QualifiedName} right) => !left.Equals(right);";
        }

        private static string BuildCaseConstructor(Union union, UnionCase unionCase)
        {
            var caseParams = unionCase.Parameters;

            if (caseParams.Count > 0)
            {
                var paramList = caseParams.Select(p => $"{p.Type} {p.Name}").Join(", ");
                var data = $"System.ValueTuple.Create({caseParams.Select(p => p.Name).Join(", ")})";
                return $"public static {union.Type} {unionCase.Name}({paramList}) => new(Tag.{unionCase.Name}, {data});";
            }
            else
            {
                return $"public static readonly {union.Type} {unionCase.Name} = new(Tag.{unionCase.Name});";
            }
        }

        private static List<string> BuildMatchMethodArgs(IReadOnlyList<UnionCase> unionCases, bool defaultToNull = false)
        {
            return unionCases.Select(unionCase =>
            {
                var nullableType = defaultToNull ? "?" : "";
                var nullableDefault = defaultToNull ? " = null" : "";

                if (unionCase.Parameters.Count > 0)
                {
                    var args = unionCase.Parameters.Select(p => p.Type.QualifiedName).Join(",");
                    return $"System.Func<{args}, TResult>{nullableType} {unionCase.Name}{nullableDefault}";
                }
                else
                {
                    return $"System.Func<TResult>{nullableType} {unionCase.Name}{nullableDefault}";
                }
            }).ToList();
        }

        private static List<(string caseHeader, List<string> caseBody)> BuildMatchMethodSwitchCases(
            IReadOnlyList<UnionCase> unionCases,
            bool redirectToDefaultBranchIfCaseIsNull)
        {
            return unionCases.Select((unionCase, caseIndex) =>
            {
                var caseParams = unionCase.Parameters;

                List<string> BuildCaseBody(List<string> caseBodyLines)
                {
                    var caseBody = new List<string>();

                    if (redirectToDefaultBranchIfCaseIsNull)
                        caseBody.Add($"if ({unionCase.Name} == null) goto default;");

                    caseBody.AddRange(caseBodyLines);
                    return caseBody;
                }

                if (caseParams.Count > 0)
                {
                    var dataVarName = $"case{caseIndex + 1}Data";

                    var castArgs = caseParams.Select(p => p.Type.QualifiedName).Join(", ");
                    var extractArgs = Enumerable.Range(1, caseParams.Count).Select(i => $"{dataVarName}.Item{i}").Join(", ");

                    var caseHeader = $"case Tag.{unionCase.Name}:";
                    var caseBody = BuildCaseBody(new()
                    {
                        $"var {dataVarName} = (System.ValueTuple<{castArgs}>)_data!;",
                        $"return {unionCase.Name}({extractArgs});"
                    });

                    return (caseHeader, caseBody);
                }
                else
                {
                    var caseHeader = $"case Tag.{unionCase.Name}:";
                    var caseBody = BuildCaseBody(new() { $"return {unionCase.Name}();" });
                    return (caseHeader, caseBody);
                }
            }).ToList();
        }

        private sealed class IndentableStringBuilder
        {
            private readonly StringBuilder _sb;
            private int _indentLevel;

            internal IndentableStringBuilder()
            {
                _sb = new StringBuilder();
                _indentLevel = 0;
            }

            internal void Append(string str) => _sb.Append(new string(' ', _indentLevel * 4) + str + "\n");

            internal void Append() => Append("");

            internal void Indent(Action f)
            {
                IncreaseIndentLevel();
                f();
                DecreaseIndentLevel();
            }

            internal void ManualIndent() => IncreaseIndentLevel();

            internal void ManualReverseIndent() => DecreaseIndentLevel();

            public override string ToString() => _sb.ToString();

            private void IncreaseIndentLevel()
            {
                _indentLevel += 1;
                if (_indentLevel > 50)
                    _indentLevel = 50;
            }

            private void DecreaseIndentLevel()
            {
                _indentLevel -= 1;
                if (_indentLevel < 0)
                    _indentLevel = 0;
            }
        }
    }
}