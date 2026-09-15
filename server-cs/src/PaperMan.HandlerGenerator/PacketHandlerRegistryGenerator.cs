// =============================================================================
// Compile-time packet-handler discovery for PaperMan.Server.
//
// No runtime reflection is used. The generator binds a static handler whose
// method name is a generated Opcode member, then emits direct method-group
// references into the Router registration table. This registration path is
// trim- and NativeAOT-friendly; it does not claim the whole server is AOT-ready.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PaperMan.HandlerGenerator;

[Generator(LanguageNames.CSharp)]
public sealed class PacketHandlerRegistryGenerator : IIncrementalGenerator
{
    private const string ServerNamespace = "PaperMan.Server";
    private const string OpcodeMetadataName = "PaperMan.Protocol.Opcode";
    private const string RawOpcodeAttributeMetadataName = "PaperMan.Server.RawOpcodeHandlerAttribute";
    private const string HandlerReturnType = "global::System.Threading.Tasks.ValueTask";
    private const string SessionType = "global::PaperMan.Server.Session";
    private const string PacketType = "global::PaperMan.Protocol.Packet";
    private const string ServerContextType = "global::PaperMan.Server.ServerContext";

    // `sub_556680` sends this one-way C2S notification. Every other direct
    // entry in this revision is a catalog *_REQ token; ACK/base tokens are not
    // valid receive-handler names merely because they appear in Opcode.
    private const string MyInfoOpenToken = "GL_MYINFO_OPEN";

    private static readonly DiagnosticDescriptor MissingProtocolSymbols = new(
        id: "PMH001",
        title: "Packet-handler discovery requires protocol symbols",
        messageFormat: "Could not locate required symbol '{0}'",
        category: "PaperMan.HandlerDiscovery",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidHandler = new(
        id: "PMH002",
        title: "Packet handler has an invalid shape",
        messageFormat: "Handler '{0}' must be a static method with signature ValueTask (Session, Packet, ServerContext) in a static partial *Handlers class",
        category: "PaperMan.HandlerDiscovery",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor DuplicateOpcode = new(
        id: "PMH003",
        title: "Packet opcode has multiple handlers",
        messageFormat: "Opcode {0} is handled by both '{1}' and '{2}'",
        category: "PaperMan.HandlerDiscovery",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidRawOpcode = new(
        id: "PMH004",
        title: "Raw packet handler declaration is invalid",
        messageFormat: "Raw handler '{0}' must use [RawOpcodeHandler(ushort)] and may not duplicate a generated Opcode member",
        category: "PaperMan.HandlerDiscovery",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor NonReceiveCatalogToken = new(
        id: "PMH005",
        title: "Catalog token is not a verified C2S handler token",
        messageFormat: "Handler '{0}' must use a *_REQ token or the source-proven one-way GL_MYINFO_OPEN token, not an ACK/base catalog member",
        category: "PaperMan.HandlerDiscovery",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, static (productionContext, compilation) =>
            Execute(productionContext, compilation));
    }

    private static void Execute(SourceProductionContext context, Compilation compilation)
    {
        INamedTypeSymbol? opcodeType = compilation.GetTypeByMetadataName(OpcodeMetadataName);
        INamedTypeSymbol? rawOpcodeAttributeType = compilation.GetTypeByMetadataName(RawOpcodeAttributeMetadataName);
        if (opcodeType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingProtocolSymbols, Location.None, OpcodeMetadataName));
            return;
        }

        if (rawOpcodeAttributeType is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingProtocolSymbols, Location.None, RawOpcodeAttributeMetadataName));
            return;
        }

        Dictionary<string, ushort> opcodeValues = opcodeType.GetMembers()
            .OfType<IFieldSymbol>()
            .Where(static field => field.HasConstantValue && !field.IsImplicitlyDeclared)
            .ToDictionary(
                static field => field.Name,
                static field => Convert.ToUInt16(field.ConstantValue),
                StringComparer.Ordinal);

        var handlers = new List<HandlerDescriptor>();
        foreach (INamedTypeSymbol type in EnumerateTypes(compilation.Assembly.GlobalNamespace))
        {
            if (type.TypeKind != TypeKind.Class
                || !type.IsStatic
                || !string.Equals(type.ContainingNamespace.ToDisplayString(), ServerNamespace, StringComparison.Ordinal)
                || !type.Name.EndsWith("Handlers", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (IMethodSymbol method in type.GetMembers().OfType<IMethodSymbol>())
            {
                bool hasNamedOpcode = opcodeValues.TryGetValue(method.Name, out ushort namedOpcode);
                bool isNamedReceiveToken = hasNamedOpcode && IsNamedReceiveToken(method.Name);
                AttributeData? rawOpcodeAttribute = method.GetAttributes().FirstOrDefault(attribute =>
                    SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, rawOpcodeAttributeType));
                if (hasNamedOpcode
                    && !isNamedReceiveToken
                    && rawOpcodeAttribute is null
                    && HasPacketHandlerSignature(method)
                    && IsPartial(type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        NonReceiveCatalogToken,
                        FirstLocation(method),
                        method.ToDisplayString()));
                    continue;
                }

                if (!isNamedReceiveToken && rawOpcodeAttribute is null)
                {
                    continue;
                }

                if (!HasPacketHandlerSignature(method) || !IsPartial(type))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidHandler,
                        FirstLocation(method),
                        method.ToDisplayString()));
                    continue;
                }

                if (hasNamedOpcode && rawOpcodeAttribute is not null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidRawOpcode,
                        FirstLocation(method),
                        method.ToDisplayString()));
                    continue;
                }

                if (isNamedReceiveToken)
                {
                    handlers.Add(new HandlerDescriptor(type, method, namedOpcode, $"global::PaperMan.Protocol.Opcode.{method.Name}"));
                    continue;
                }

                if (!TryGetRawOpcode(rawOpcodeAttribute!, out ushort rawOpcode))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        InvalidRawOpcode,
                        FirstLocation(method),
                        method.ToDisplayString()));
                    continue;
                }

                handlers.Add(new HandlerDescriptor(
                    type,
                    method,
                    rawOpcode,
                    rawOpcode.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        List<HandlerDescriptor> uniqueHandlers = RemoveDuplicates(context, handlers);
        Emit(context, uniqueHandlers);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol @namespace)
    {
        foreach (INamespaceOrTypeSymbol member in @namespace.GetMembers())
        {
            if (member is INamespaceSymbol nestedNamespace)
            {
                foreach (INamedTypeSymbol nestedType in EnumerateTypes(nestedNamespace))
                {
                    yield return nestedType;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
            }
        }
    }

    private static bool IsNamedReceiveToken(string token) =>
        token.EndsWith("_REQ", StringComparison.Ordinal)
        || string.Equals(token, MyInfoOpenToken, StringComparison.Ordinal);

    private static bool HasPacketHandlerSignature(IMethodSymbol method) =>
        method.MethodKind == MethodKind.Ordinary
        && method.IsStatic
        && string.Equals(method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), HandlerReturnType, StringComparison.Ordinal)
        && method.Parameters.Length == 3
        && string.Equals(method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), SessionType, StringComparison.Ordinal)
        && string.Equals(method.Parameters[1].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), PacketType, StringComparison.Ordinal)
        && string.Equals(method.Parameters[2].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), ServerContextType, StringComparison.Ordinal);

    private static bool IsPartial(INamedTypeSymbol type) => type.DeclaringSyntaxReferences.All(static declaration =>
        declaration.GetSyntax() is TypeDeclarationSyntax syntax
        && syntax.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword)));

    private static bool TryGetRawOpcode(AttributeData attribute, out ushort opcode)
    {
        if (attribute.ConstructorArguments.Length == 1
            && attribute.ConstructorArguments[0].Value is ushort rawOpcode)
        {
            opcode = rawOpcode;
            return true;
        }

        opcode = 0;
        return false;
    }

    private static List<HandlerDescriptor> RemoveDuplicates(
        SourceProductionContext context,
        IEnumerable<HandlerDescriptor> handlers)
    {
        var byOpcode = new Dictionary<ushort, HandlerDescriptor>();
        foreach (HandlerDescriptor handler in handlers.OrderBy(static handler => handler.Opcode).ThenBy(static handler => handler.Method.Name, StringComparer.Ordinal))
        {
            if (byOpcode.TryGetValue(handler.Opcode, out HandlerDescriptor existing))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateOpcode,
                    FirstLocation(handler.Method),
                    handler.Opcode,
                    existing.Method.ToDisplayString(),
                    handler.Method.ToDisplayString()));
                continue;
            }

            byOpcode.Add(handler.Opcode, handler);
        }

        return byOpcode.Values
            .OrderBy(static handler => handler.ContainingType.Name, StringComparer.Ordinal)
            .ThenBy(static handler => handler.Opcode)
            .ToList();
    }

    private static void Emit(SourceProductionContext context, IReadOnlyList<HandlerDescriptor> handlers)
    {
        var output = new StringBuilder();
        output.AppendLine("// <auto-generated />");
        output.AppendLine("// Generated by PaperMan.HandlerGenerator. Do not edit.");
        output.AppendLine("#nullable enable");
        output.AppendLine();
        output.AppendLine("namespace PaperMan.Server;");
        output.AppendLine();
        output.AppendLine("internal static class GeneratedPacketHandlerRegistration");
        output.AppendLine("{");
        output.AppendLine("    internal static void AddTo(global::System.Collections.Generic.Dictionary<ushort, global::PaperMan.Server.PacketHandler> table)");
        output.AppendLine("    {");
        output.AppendLine("        global::System.ArgumentNullException.ThrowIfNull(table);");

        foreach (IGrouping<string, HandlerDescriptor> family in handlers.GroupBy(static handler => handler.ContainingType.Name).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            output.Append("        global::PaperMan.Server.");
            output.Append(family.Key);
            output.AppendLine(".AddDiscoveredHandlers(table);");
        }

        output.AppendLine("    }");
        output.AppendLine("}");

        foreach (IGrouping<string, HandlerDescriptor> family in handlers.GroupBy(static handler => handler.ContainingType.Name).OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            output.AppendLine();
            output.Append("public static partial class ");
            output.Append(family.Key);
            output.AppendLine();
            output.AppendLine("{");
            output.AppendLine("    internal static void AddDiscoveredHandlers(global::System.Collections.Generic.Dictionary<ushort, global::PaperMan.Server.PacketHandler> table)");
            output.AppendLine("    {");
            foreach (HandlerDescriptor handler in family.OrderBy(static handler => handler.Opcode))
            {
                output.Append("        table.Add((ushort)");
                output.Append(handler.OpcodeExpression);
                output.Append(", ");
                output.Append(handler.Method.Name);
                output.AppendLine(");");
            }

            output.AppendLine("    }");
            output.AppendLine("}");
        }

        context.AddSource("GeneratedPacketHandlerRegistration.g.cs", output.ToString());
    }

    private static Location FirstLocation(ISymbol symbol) =>
        symbol.Locations.FirstOrDefault(static location => location.IsInSource) ?? Location.None;

    private sealed class HandlerDescriptor
    {
        public HandlerDescriptor(
            INamedTypeSymbol containingType,
            IMethodSymbol method,
            ushort opcode,
            string opcodeExpression)
        {
            ContainingType = containingType;
            Method = method;
            Opcode = opcode;
            OpcodeExpression = opcodeExpression;
        }

        public INamedTypeSymbol ContainingType { get; }
        public IMethodSymbol Method { get; }
        public ushort Opcode { get; }
        public string OpcodeExpression { get; }
    }
}
