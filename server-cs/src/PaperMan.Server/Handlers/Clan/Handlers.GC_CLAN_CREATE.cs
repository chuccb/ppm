// =============================================================================
// GC_CLAN_CREATE_REQ (585) → GC_CLAN_CREATE_ACK (586)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class ClanHandlers
{
    /// <summary>586 的 result 碼 (sub_54CB90 的 switch 分支)。</summary>
    private enum GC_CLAN_CREATE_ACK_Result : sbyte
    {
        Ok = 0,             // → 續讀 s32 扣費後 GP
        DuplicateName = 1,  // 0x1A7 重名
        NotEnoughGp = 2,    // 0x1A8 GP 不足
        Failed = 3,         // 其他失敗 (3..7 各有訊息)
    }

    // REQ(585) sub_5505F0: str name, str slogan, str intro, u8 emblem
    private static async ValueTask GC_CLAN_CREATE_REQ(Session session, Packet packet, ServerContext context)
    {
        var name = packet.ReadStr();
        var slogan = packet.ReadStr();
        var intro = packet.ReadStr();
        int emblem = packet.Remaining >= 4 ? packet.ReadS32() : 0;    // s32 (廿四輪修正)
        _ = (slogan, intro);                                // schema 暫存於 notice 欄位外

        var result = session.UserId switch
        {
            0 => GC_CLAN_CREATE_ACK_Result.Failed,
            _ => context.Db.CreateClan(session.UserId, name, (byte)Math.Clamp(emblem, 0, 255)) switch
            {
                > 0 => GC_CLAN_CREATE_ACK_Result.Ok,
                _ => GC_CLAN_CREATE_ACK_Result.DuplicateName,
            },
        };

        var ack = new Packet(Opcode.GC_CLAN_CREATE_ACK).WriteS8((sbyte)result);
        if (result is GC_CLAN_CREATE_ACK_Result.Ok)
        {
            var info = context.Db.GetMyInfo(session.UserId);
            ack.WriteS32((int)(info?.Gp ?? 0));             // sub_54DD70 → EE8D18
        }

        await session.SendAsync(ack);
    }
}
