// =============================================================================
// GI_CHANGEWP_REQ (220) → GI_CHANGEWP_ACK (221)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 220 GI_CHANGEWP_REQ (sub_573340): u8 changedCount followed by only the
    // groups whose local profile differs.  221's receiver builds a fresh
    // CClientData and then replaces the global profile, so the server reply
    // must instead carry the authoritative *four-group* snapshot.
    private static async ValueTask GI_CHANGEWP_REQ(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ requires an authenticated player identity.");
        }

        List<Db.WeaponGroup> changedGroups = ReadGI_CHANGEWP_REQ_Groups(packet);
        if (!context.Db.TryChangeWeaponGroups(session.UserId, changedGroups, out List<Db.WeaponGroup> groups))
        {
            // The 221 client parser exposes no rejection status byte.  Do not
            // forge an all-zero/full snapshot or a nominal success: retain the
            // existing profile and let the malformed/unauthorized request fail.
            throw new InvalidDataException("GI_CHANGEWP_REQ failed wire, ownership, or resource compatibility validation.");
        }

        var acknowledgement = new Packet(Opcode.GI_CHANGEWP_ACK).WriteU8(4);
        foreach (Db.WeaponGroup group in groups)
        {
            WriteGI_CHANGEWP_ACK_Groups(acknowledgement, group);
        }

        await session.SendAsync(acknowledgement);
    }

    private static List<Db.WeaponGroup> ReadGI_CHANGEWP_REQ_Groups(Packet packet)
    {
        if (packet.Remaining < 1)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ is missing its group count.");
        }

        byte changedCount = packet.ReadU8();
        if (changedCount is 0 or > 4)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ group count must be 1..4.");
        }

        var groups = new List<Db.WeaponGroup>(changedCount);
        var seenGroupNumbers = new HashSet<byte>();
        for (int i = 0; i < changedCount; i++)
        {
            if (packet.Remaining < 3)
            {
                throw new InvalidDataException("GI_CHANGEWP_REQ has a truncated group prefix.");
            }

            byte groupNumber = packet.ReadU8();
            ushort primaryOffset = packet.ReadU16();
            if (groupNumber >= 4 || !seenGroupNumbers.Add(groupNumber))
            {
                throw new InvalidDataException("GI_CHANGEWP_REQ has an invalid or duplicate group number.");
            }

            ushort secondaryOffset = 0;
            ushort meleeOffset = 0;
            ushort throwOffset = 0;
            if (groupNumber != 3)
            {
                if (packet.Remaining < 6)
                {
                    throw new InvalidDataException("GI_CHANGEWP_REQ has a truncated non-switch group.");
                }

                secondaryOffset = packet.ReadU16();
                meleeOffset = packet.ReadU16();
                throwOffset = packet.ReadU16();
            }

            var parts = new int[8];
            if (primaryOffset != 0)
            {
                if (packet.Remaining < 32)
                {
                    throw new InvalidDataException("GI_CHANGEWP_REQ has a truncated primary part array.");
                }

                for (int part = 0; part < parts.Length; part++)
                {
                    parts[part] = packet.ReadS32();
                }
            }

            groups.Add(new Db.WeaponGroup(
                groupNumber, primaryOffset, secondaryOffset, meleeOffset, throwOffset, parts));
        }

        if (packet.Remaining != 0)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ has trailing bytes.");
        }

        return groups;
    }

    private static void WriteGI_CHANGEWP_ACK_Groups(Packet packet, Db.WeaponGroup group)
    {
        packet.WriteU8(group.GroupNo)
              .WriteU16(group.PrimaryOffset);
        if (group.GroupNo != 3)
        {
            packet.WriteU16(group.SecondaryOffset)
                  .WriteU16(group.MeleeOffset)
                  .WriteU16(group.ThrowOffset);
        }

        if (group.PrimaryOffset != 0)
        {
            foreach (int part in group.Parts)
            {
                packet.WriteS32(part);
            }
        }
    }

}
