using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.IO;

// ClassDiagramGenerator from https://github.com/pierre3/PlantUmlClassDiagramGenerator
namespace Diagrams
{
    /// <summary>
    /// Classe para gerar o diagrama de classes PlantUML a partir do código-fonte C #.
    /// </summary>
    public class ClassDiagramGenerator : CSharpSyntaxWalker
    {
        private TextWriter writer;
        private string indent;
        private int nestingDepth = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="writer">TextWriter que gera o resultado.</param>
        /// <param name="indent">String para usar como recuo.</param>
        public ClassDiagramGenerator(TextWriter writer, string indent)
        {
            this.writer = writer;
            this.indent = indent;
        }

        /// <summary>
        /// Definição da interface de saída no formato PlantUML.
        /// </summary>
        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitInterfaceDeclaration(node));
        }

        /// <summary>
        /// Definição de classe de saída no formato PlantUML.
        /// </summary>
        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            VisitTypeDeclaration(node, () => base.VisitClassDeclaration(node));
        }

        /// <summary>
        /// Saída da definição da estrutura no formato PlantUML.
        /// </summary>
        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var name = node.Identifier.ToString();
            var typeParam = node.TypeParameterList?.ToString() ?? "";

            WriteLine($"class {name}{typeParam} <<struct>> {{");

            nestingDepth++;
            base.VisitStructDeclaration(node);
            nestingDepth--;

            WriteLine("}");
        }

        /// <summary>
        /// Definição de tipo de enum de saída no formato PlantUML.
        /// </summary>
        /// <param name="node"></param>
        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            WriteLine($"{node.EnumKeyword} {node.Identifier} {{");

            nestingDepth++;
            base.VisitEnumDeclaration(node);
            nestingDepth--;

            WriteLine("}");
        }

        /// <summary>
        /// Saída da definição de tipo (classe, interface, estrutura) no formato PlantUML.
        /// </summary>
        /// <param name="node"></param>
        private void VisitTypeDeclaration(TypeDeclarationSyntax node, Action visitBase)
        {
            var modifiers = GetTypeModifiersText(node.Modifiers);
            var keyword = (node.Modifiers.Any(SyntaxKind.AbstractKeyword) ? "abstract " : "")
                + node.Keyword.ToString();
            var name = node.Identifier.ToString();
            var typeParam = node.TypeParameterList?.ToString() ?? "";

            WriteLine($"{keyword} {name}{typeParam} {modifiers}{{");

            nestingDepth++;
            visitBase();
            nestingDepth--;

            WriteLine("}");

            if (node.BaseList != null)
            {
                foreach (var b in node.BaseList.Types)
                {
                    WriteLine($"{name} <|-- {b.Type.ToFullString()}");
                }
            }
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var name = node.Identifier.ToString();
            var args = node.ParameterList.Parameters.Select(p => $"{p.Identifier}:{p.Type}");

            WriteLine($"{modifiers}{name}({string.Join(", ", args)})");
        }

        /// <summary>
        /// Definição do campo de saída.
        /// </summary>
        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var typeName = node.Declaration.Type.ToString();
            var variables = node.Declaration.Variables;
            foreach (var field in variables)
            {
                var useLiteralInit = field.Initializer?.Value?.Kind().ToString().EndsWith("LiteralExpression") ?? false;
                var initValue = useLiteralInit ? (" = " + field.Initializer.Value.ToString()) : "";

                WriteLine($"{modifiers}{field.Identifier} : {typeName}{initValue}");
            }
        }

        /// <summary>
        /// Definição da propriedade de saída.
        /// </summary>
        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var name = node.Identifier.ToString();
            var typeName = node.Type.ToString();
            var accessor = node.AccessorList?.Accessors
                .Where(x => !x.Modifiers.Select(y => y.Kind()).Contains(SyntaxKind.PrivateKeyword))
                .Select(x => $"<<{(x.Modifiers.ToString() == "" ? "" : (x.Modifiers.ToString() + " "))}{x.Keyword}>>");

            var useLiteralInit = node.Initializer?.Value?.Kind().ToString().EndsWith("LiteralExpression") ?? false;
            var initValue = useLiteralInit ? (" = " + node.Initializer.Value.ToString()) : "";

            var acc = (accessor != null) ? string.Join(" ", accessor) : string.Empty;

            WriteLine($"{modifiers}{name} : {typeName} {acc}{initValue}");
        }

        /// <summary>
        /// Definição do método de saída.
        /// </summary>
        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var modifiers = GetMemberModifiersText(node.Modifiers);
            var name = node.Identifier.ToString();
            var returnType = node.ReturnType.ToString();
            var args = node.ParameterList.Parameters.Select(p => $"{p.Identifier}:{p.Type}");

            WriteLine($"{modifiers}{name}({string.Join(", ", args)}) : {returnType}");
        }

        /// <summary>
        /// Membros de enumeração de saída.
        /// </summary>
        /// <param name="node"></param>
        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            WriteLine($"{node.Identifier}{node.EqualsValue},");
        }

        /// <summary>
        /// Escreva uma linha na saída de resultado TextWriter.
        /// </summary>
        private void WriteLine(string line)
        {
            // Adicione recuo no início da linha para o nível de aninhamento.
            var space = string.Concat(Enumerable.Repeat(indent, nestingDepth));
            writer.WriteLine(space + line);
        }

        /// <summary>
        /// Converter modificadores de tipo (classe, interface, estrutura) em seqüências de caracteres.
        /// </summary>
        /// <param name="modifiers">TokenList do modificador.</param>
        /// <returns>Sequência de caracteres após a conversão.</returns>
        private string GetTypeModifiersText(SyntaxTokenList modifiers)
        {
            var tokens = modifiers.Select(token =>
            {
                switch (token.Kind())
                {
                    case SyntaxKind.PublicKeyword:
                    case SyntaxKind.PrivateKeyword:
                    case SyntaxKind.ProtectedKeyword:
                    case SyntaxKind.InternalKeyword:
                    case SyntaxKind.AbstractKeyword:
                        return "";
                    default:
                        return $"<<{token.ValueText}>>";
                }
            }).Where(token => token != "");

            var result = string.Join(" ", tokens);
            if (result != string.Empty)
            {
                result += " ";
            };
            return result;
        }

        /// <summary>
        /// Converter qualificador de membro do tipo em sequência.
        /// </summary>
        /// <param name="modifiers">TokenList do modificador.</param>
        /// <returns></returns>
        private string GetMemberModifiersText(SyntaxTokenList modifiers)
        {
            var tokens = modifiers.Select(token =>
            {
                switch (token.Kind())
                {
                    case SyntaxKind.PublicKeyword:
                        return "+";
                    case SyntaxKind.PrivateKeyword:
                        return "-";
                    case SyntaxKind.ProtectedKeyword:
                        return "#";
                    case SyntaxKind.AbstractKeyword:
                    case SyntaxKind.StaticKeyword:
                        return $"{{{token.ValueText}}}";
                    case SyntaxKind.InternalKeyword:
                    default:
                        return $"<<{token.ValueText}>>";
                }
            });

            var result = string.Join(" ", tokens);
            if (result != string.Empty)
            {
                result += " ";
            };
            return result;
        }
    }
}
