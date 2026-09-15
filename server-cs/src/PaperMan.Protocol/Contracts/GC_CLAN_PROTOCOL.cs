// =============================================================================
// GC_CLAN_PROTOCOL_REQ (583) / GC_CLAN_PROTOCOL_ACK (584) container grammar.
//
// Both payloads begin with s32 sub_opcode. ACK dispatch is sub_54D040 (case
// 584); all 24 observed REQ builders construct opcode 583 then WriteS32(sub).
// This inner numeric space is independent of the top-level opcode catalog.
//
// GC_CLAN_CREATE_REQ/ACK (585/586) is an independent pair, not a sub-op.
// Field layouts, direction, and native consumers are in docs/PACKETS.md §2,
// "GC_CLAN_PROTOCOL container grammar".
// =============================================================================
namespace PaperMan.Protocol;

/// <summary>
/// The s32 sub_opcode values observed in GC_CLAN_PROTOCOL_REQ builders or the
/// GC_CLAN_PROTOCOL_ACK sub_54D040 dispatcher. Native code exposes switch
/// literals, not symbolic names, for this inner protocol. Members therefore
/// preserve the literal as SubNNN; comments record observed client data flow
/// and are not asserted to be original identifiers. Not every number occurs in
/// both directions.
/// </summary>
public enum GC_CLAN_PROTOCOL_SubOpcode
{
    Sub182 = 182,             // ACK: s32 clan_id → welcome message (sub_54E890)
    Sub184 = 184,             // REQ: str clan_name; ACK: s32 id, str nick
    Sub185 = 185,             // REQ: s32, s32 uid, s32 11, s32 0
    Sub186 = 186,             // REQ: str nick; ACK: s32 uid, str nick
    Sub187 = 187,             // REQ: s32 clan_id → clan information lookup
    Sub188 = 188,             // REQ: s32 clan_id, s32 page
    Sub189 = 189,             // REQ: s32 clan_id; ACK: s32, str ×2
    Sub191 = 191,             // REQ: s32 n3, str nick
    Sub192 = 192,             // REQ: s32; ACK: s32, str×4, s32×5 summary
    Sub193 = 193,             // REQ: s32 flag
    Sub194 = 194,             // ACK only (sub_54EE10)
    Sub195 = 195,             // REQ: s32 id, str
    Sub196 = 196,             // REQ: s32 count + raw(4*count)
    Sub197 = 197,             // same shape as 196
    Sub198 = 198,             // ACK: s32, s32, str×2, s32 (sub_54F450)
    Sub199 = 199,             // ACK (sub_54F5A0)
    Sub200 = 200,             // REQ: s32 clan_id; ACK member list (sub_54EE70)
    Sub201 = 201,             // ACK: s32, [s32] (sub_54F0F0)
    Sub202 = 202,             // REQ: no fields; matching ACK sub-op is UNRESOLVED
    Sub203 = 203,             // REQ: str message; ACK: str nick, str message (sub_54F2D0)
    Sub204 = 204,             // ACK (sub_54F330)
    Sub205 = 205,             // REQ/ACK: str ×3 (sub_54F3D0)
    Sub206 = 206,             // ACK (sub_54F700)
    Sub207 = 207,             // ACK (sub_54F8D0)
    Sub208 = 208,             // REQ: no fields
    Sub209 = 209,             // REQ: no fields
    Sub210 = 210,             // REQ: str; ACK: s32 uid, str (sub_54E770)
    Sub211 = 211,             // REQ: s32 uid; ACK updates rank (sub_54FBA0)
    Sub212 = 212,             // mirror shape of 211 (sub_54FD50)
    Sub213 = 213,             // ACK only
    Sub214 = 214,             // ACK only
    Sub215 = 215,             // ACK only
    Sub218 = 218,             // ACK only
    Sub219 = 219,             // ACK only
    Sub381 = 381,             // ACK: s32×2 (sub_54FF00)
    Sub382 = 382,             // ACK: s32×2 (sub_54FFC0)
    Sub383 = 383,             // ACK only
}

/// <summary>Byte-exact GC_CLAN_PROTOCOL_REQ/ACK sub-op container operations.</summary>
public static class GC_CLAN_PROTOCOL_Wire
{
    /// <summary>Creates 584: s32 sub_opcode followed by the supplied sub-payload.</summary>
    public static Packet CreateAcknowledgement(
        GC_CLAN_PROTOCOL_SubOpcode subOpcode,
        Action<Packet>? writeSubPayload = null)
    {
        var packet = new Packet(Opcode.GC_CLAN_PROTOCOL_ACK).WriteS32((int)subOpcode);
        writeSubPayload?.Invoke(packet);
        return packet;
    }

    /// <summary>Reads the leading s32 sub_opcode from a 583 request.</summary>
    public static GC_CLAN_PROTOCOL_SubOpcode ReadRequestSubOpcode(Packet request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return (GC_CLAN_PROTOCOL_SubOpcode)request.ReadS32();
    }
}
