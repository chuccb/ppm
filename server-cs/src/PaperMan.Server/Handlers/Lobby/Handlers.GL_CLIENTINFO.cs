// =============================================================================
// GL_CLIENTINFO_REQ (246) → GL_CLIENTINFO_ACK (247)
// File and handler entry use the native opcode catalog token verbatim. Helpers
// retain that request token or its paired ACK token when they parse or construct packets.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 246 GL_CLIENTINFO_REQ: str nick → 247 ACK (sub_573EB0):
    //   u8 ok(==1) + sub_523BF0 基本資料塊 + sub_524360 單角色外觀
    //   (十一輪: 與 198 首段同構 — 重用 CreateGL_MYINFO_ACK 的統計佈局)
    private static async ValueTask GL_CLIENTINFO_REQ(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        var info = context.Db.GetMyInfoByNick(nick);

        if (info is null)
        {
            await session.SendAsync(new Packet(Opcode.GL_CLIENTINFO_ACK).WriteU8(0));
            return;
        }

        var chars = context.Db.GetCharacters(info.UserId);
        var ack = CreateGL_CLIENTINFO_ACK(info, chars);
        await session.SendAsync(ack);
    }

    /// <summary>247 = sub_523BF0 統計塊 + sub_524360 單角色外觀。</summary>
    private static Packet CreateGL_CLIENTINFO_ACK(Db.MyInfo info, List<Db.CharSlot> chars)
    {
        var st = info.Stats;
        // 247 uses the same sub_523BF0 block as 198: CClientData+88 is the
        // selected CHARSLOT list index, not a character type.
        byte selectedCharacterSlot = info.CurrentChar;
        var ack = new Packet(Opcode.GL_CLIENTINFO_ACK)
            .WriteU8(1)
            // sub_523BF0 — 與 198 首段完全同構 (佈局見 CreateGL_MYINFO_ACK)
            .WriteStr(info.Nickname)
            .WriteU8(selectedCharacterSlot)
            .WriteS32(info.Level)
            .WriteS32((int)info.Exp)
            .WriteS32(0)
            .WriteS32((int)st.PlayCount)
            .WriteS32((int)st.RoundCount)
            .WriteS32((int)st.Criticals)
            .WriteS32((int)st.Wins)
            .WriteS32((int)st.Losses)
            .WriteS32((int)st.Kills)
            .WriteS32((int)st.Deaths)
            .WriteS32((int)st.Disconnects)
            .WriteS32((int)st.Hearts)
            .WriteS32((int)st.Headshots)
            .WriteS32((int)st.DoubleKill)
            .WriteS32((int)st.TripleKill)
            .WriteS32((int)st.Combos)
            .WriteS32((int)st.MultiKill)
            .WriteS32((int)st.UltraKill)
            .WriteS32((int)st.ZKill)
            .WriteS32((int)st.KKill)
            .WriteS32((int)st.DdKill)
            .WriteU8(0).WriteU8(0).WriteU8(0)
            .WriteS32(info.Cash)
            .WriteS32(0).WriteS32(0)
            .WriteRaw(new byte[48])                         // [28],[29] 後的 48B 保留區 (零)
            .WriteU8(info.CurrentChar);

        // sub_524360: u8 slot + u8 char_type + 12×u16 外觀
        var slot = chars.FirstOrDefault(c => c.SlotNo == info.CurrentChar) ?? chars.FirstOrDefault();
        ack.WriteU8(slot?.SlotNo ?? (byte)0)
           .WriteU8(slot?.CharType ?? (byte)1);
        for (int i = 0; i < 12; i++)
        {
            ack.WriteU16(slot?.Equip.ElementAtOrDefault(i) ?? (ushort)0);
        }

        return ack;
    }

}
