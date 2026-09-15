// =============================================================================
// Compile-time marker for a deliberately tokenless C2S opcode.
//
// Normal handlers are discovered from their canonical Opcode-member method name.
// The sole raw boundary (206) has no catalog request token, so its numeric value
// is explicit here for the compile-time generator without runtime reflection.
// =============================================================================
namespace PaperMan.Server;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
internal sealed class RawOpcodeHandlerAttribute(ushort opcode) : Attribute
{
    public ushort Opcode { get; } = opcode;
}
