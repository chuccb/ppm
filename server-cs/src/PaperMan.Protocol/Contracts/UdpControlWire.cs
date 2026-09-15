// =============================================================================
// UDP-private control packets whose complete client-side path is directly known.
//
// Evidence:
//   * request builder: sub_596670 (opcode 19)
//   * completion handler: sub_595E80 case 20 -> sub_5968C0
//
// This is intentionally a separate enum from Opcode.cs. Opcode.cs is the TCP
// catalog registered by sub_9D2050; sub_595E80 dispatches an independent UDP
// namespace. The client source proves field positions and widths, but not the
// server-domain meaning of every source value. Keep those names source-oriented.
// =============================================================================
namespace PaperMan.Protocol;

/// <summary>Directly evidenced values in the private UDP dispatcher namespace.</summary>
public enum UdpPrivateOpcode : ushort
{
    /// <summary>sub_596670 constructs this numeric request literal.</summary>
    Opcode19 = 19,

    /// <summary>sub_595E80 case 20 dispatches this numeric response literal.</summary>
    Opcode20 = 20,
}

/// <summary>
/// The exact opcode-19 payload written by sub_596670. This is a client report,
/// not an authenticated server identity. In particular, the native source does
/// not establish a server-side validation rule for any of these fields.
/// </summary>
public sealed record UdpControlRequest(
    byte ActiveChannelIndex,
    byte CurrentRoomSlot,
    sbyte SourceModeEqualsTwoFlag,
    sbyte SourceDependentSlot,
    int ClientReportedPlayerId,
    string LocalNickname)
{
    /// <summary>Parses the whole source-proven opcode-19 payload, including its required NUL.</summary>
    public static UdpControlRequest Read(Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.OpcodeRaw != (ushort)UdpPrivateOpcode.Opcode19)
        {
            throw new ArgumentException(
                $"Expected UDP-private opcode {(ushort)UdpPrivateOpcode.Opcode19}, got {packet.OpcodeRaw}.",
                nameof(packet));
        }

        byte activeChannelIndex = packet.ReadU8();
        byte currentRoomSlot = packet.ReadU8();
        sbyte sourceModeEqualsTwoFlag = packet.ReadS8();
        sbyte sourceDependentSlot = packet.ReadS8();
        int clientReportedPlayerId = packet.ReadS32();

        // This is emitted from the native comparison `n2 == 2`, so values
        // other than 0/1 cannot be a normal opcode-19 source shape. The true
        // arm has a directly observed -2 sentinel; no range is inferred for
        // the false arm because its `n0x10` domain remains unresolved.
        if (sourceModeEqualsTwoFlag != 0 && sourceModeEqualsTwoFlag != 1)
        {
            throw new InvalidDataException("UDP-private opcode 19 has a non-boolean source-mode comparison flag.");
        }

        if (sourceModeEqualsTwoFlag == 1 && sourceDependentSlot != -2)
        {
            throw new InvalidDataException("UDP-private opcode 19 is missing the native -2 special-case sentinel.");
        }

        // sub_5926F0 writes strlen + its terminating NUL. There is no length
        // prefix. The caller's current remaining bytes give the only safe
        // packet-local bound; a missing NUL is not a valid native emission.
        if (packet.Remaining == 0)
        {
            throw new EndOfStreamException("UDP-private opcode 19 is missing its NUL-terminated nickname.");
        }

        string localNickname = packet.ReadNulTerminatedAnsiString(packet.Remaining - 1);
        if (packet.Remaining != 0)
        {
            throw new InvalidDataException("UDP-private opcode 19 has trailing bytes.");
        }

        return new(
            activeChannelIndex,
            currentRoomSlot,
            sourceModeEqualsTwoFlag,
            sourceDependentSlot,
            clientReportedPlayerId,
            localNickname);
    }
}
