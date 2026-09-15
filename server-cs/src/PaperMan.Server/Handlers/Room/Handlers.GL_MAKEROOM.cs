// =============================================================================
// GL_MAKEROOM_REQ (111) → GL_MAKEROOM_ACK (112)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class RoomHandlers
{
    /// <summary>112 的 err 碼 (sub_56A7B0: err!=0 → 顯示失敗訊息)。</summary>
    private enum GL_MAKEROOM_ACK_Error : byte
    {
        Ok = 0,
        Full = 1,                                           // 210 房全滿
        BadParams = 2,
    }

    // 111 normal title form (sub_449320 → sub_56A5A0):
    //   u8 0xFF title-form marker, s8 has_password, str title,
    //   [has_password: str password], u8 max_players, u8 modeIndex,
    //   u8 requested_map, u8 no_skill_background.
    //
    // sub_449320 is the only reachable caller and passes -1 for the first
    // argument, which sub_56A5A0 serializes as 0xFF. The alternative native
    // no-title form has no reachable caller, so it is not a valid server
    // create-room request. This marker is not a map ID.
    //
    // 112 (sub_56A7B0) always begins with its six-field room result; err==0
    // adds team-mode plus two team blocks. The client unconditionally reads
    // both blocks for a successful response, including empty teams.
    private readonly record struct GL_MAKEROOM_REQ_Fields(
        string Title,
        string? Password,
        byte MaxPlayers,
        byte ModeIndex,
        byte RequestedMapId,
        bool NoSkillBackground);

    private static async ValueTask GL_MAKEROOM_REQ(Session session, Packet packet, ServerContext context)
    {
        if (!TryReadGL_MAKEROOM_REQ(packet, out GL_MAKEROOM_REQ_Fields request))
        {
            return;
        }

        byte mapId = ResolveMap(request.RequestedMapId, request.ModeIndex, context.Db);
        Room? room = session.UserId != 0
            ? context.Rooms.Create(
                session,
                mapId,
                request.Title,
                request.Password,
                request.ModeIndex,
                request.MaxPlayers,
                request.NoSkillBackground)
            : null;

        GL_MAKEROOM_ACK_Error err = room is null ? GL_MAKEROOM_ACK_Error.Full : GL_MAKEROOM_ACK_Error.Ok;
        var ack = new Packet(Opcode.GL_MAKEROOM_ACK)
            .WriteU8((byte)err)
            .WriteU8(room?.RoomNo ?? 0)                     // room_no (失敗時為 0)
            .WriteU16(room?.MaxSlotMask ?? 0)               // v52 → +110 上限槽位點陣
            .WriteS32(room?.RoomUid ?? 0)                   // v57 → dword_F2A65C
            .WriteBool(room?.NoSkillBg ?? false)            // v50 → +185 no_skill_bg
            .WriteBool(room?.TeamShuffle ?? false);         // v53 → mode+13 隊打散

        if (room is not null)
        {
            session.RoomNo = room.RoomNo;
            ack.WriteU8(IsNativeTwoTeamMode(request.ModeIndex) ? (byte)2 : (byte)0) // n2_1 team_mode (2=隊伍房 → CCustomTexture)
               .WriteU32(0)                                 // team A uid (新房間尚無分隊)
               .WriteU32(0)                                 // team A crc
               .WriteStr("")                                // team A name
               .WriteU8(0)                                  // team A flag
               .WriteU32(0)                                 // team B uid
               .WriteU32(0)                                 // team B crc
               .WriteStr("")                                // team B name
               .WriteU8(0);                                 // team B flag
        }

        await session.SendAsync(ack);
    }

    private static bool TryReadGL_MAKEROOM_REQ(Packet packet, out GL_MAKEROOM_REQ_Fields request)
    {
        request = default;

        if (packet.Remaining < 2)
        {
            return false;
        }

        if (packet.ReadU8() != byte.MaxValue)
        {
            return false;
        }

        // After the marker: flag + empty title NUL + four final bytes.
        if (packet.Remaining < 6)
        {
            return false;
        }

        sbyte hasPassword = packet.ReadS8();
        if (!TryReadGL_MAKEROOM_REQ_NulTerminatedAnsiString(packet, maxContentBytes: 50, out string title)
            || title.Length == 0)
        {
            return false;
        }

        string? password = null;
        if (hasPassword != 0
            && !TryReadGL_MAKEROOM_REQ_NulTerminatedAnsiString(packet, Packet.MaxPayload, out password))
        {
            return false;
        }

        if (packet.Remaining != 4)
        {
            return false;
        }

        request = new GL_MAKEROOM_REQ_Fields(
            title,
            password,
            packet.ReadU8(),
            packet.ReadU8(),
            packet.ReadU8(),
            packet.ReadU8() != 0);
        return true;
    }

    // Scan before advancing so a malformed variable field is a silent no-op,
    // rather than a partial parse followed by a room mutation.
    private static bool TryReadGL_MAKEROOM_REQ_NulTerminatedAnsiString(Packet packet, int maxContentBytes, out string value)
    {
        value = string.Empty;
        if (packet.Remaining <= 0)
        {
            return false;
        }

        ReadOnlySpan<byte> remaining = packet.Payload[packet.ReadPos..];
        int bytesToScan = Math.Min(remaining.Length, checked(maxContentBytes + 1));
        if (remaining[..bytesToScan].IndexOf((byte)0) < 0)
        {
            return false;
        }

        value = packet.ReadStr();
        return true;
    }

}
