// =============================================================================
// 語音自訂 handlers — CVCustomizeManager 系 (wire 佈局逐函數定案):
//
//   791 GL_VOICEITEMSLOT_REQ    (空) → 792 ACK: 目前角色語音塊
//   793 GI_VOICEITEMSLOT_ALL_REQ(空) → 794 ACK: 全 15 角色語音塊
//   795 GI_CHANGE_VOICEITEMSLOT_REQ (兩變體) → 796 ACK: u8 err, u8
//
//   語音塊 (sub_876B00/sub_876C90 讀序):
//     s16 base_voice1, s16 base_voice2,
//     3 類 (command/tactics/infomation) × 9 句 × {s16 voice_item, u8 flag}
//   voice_item = 語音表偏移 (voice_customize_contents.xml; 0=角色原生);
//   flag = 該槽位置 1..9 (0=未自訂/預設), server 原樣回傳不詮釋。
//
//   795 兩變體 (依長度判別 — B 固定 20×89=1780B, A ≤117B):
//     A (sub_885F10, 單角色差分): u8 char_idx, u8 base_changed,
//        [s16,s16], 3×{u8 n, n×{u8 slot(1..9), s16 item, u8 flag}}
//     B (sub_886330, 全量, client 死碼未用): 20×{s32 char_idx, s16, s16,
//        27×{s16,u8}} — 保留 15..19 槽位不落地
//   796 err≠0 → client 顯示 0x3FB「ボイスカスタマイズ設定保存に失敗…」
//   並重拉 792 回滾; 私服一律成功 (err=0)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class VoiceHandlers
{
    /// <summary>795 全量變體 B 的固定長度 = 20 × (s32 + 2×s16 + 27×(s16+u8))。</summary>
    private const int FullVariantBytes = 20 * (4 + 4 + Db.VoiceSlotCount * 3);

    public static void Register(Registrar add)
    {
        add(Opcode.GL_VOICEITEMSLOT_REQ, SendSingle);
        add(Opcode.GI_VOICEITEMSLOT_ALL_REQ, SendAll);
        add(Opcode.GI_CHANGE_VOICEITEMSLOT_REQ, ApplyChange);
    }

    // 791 (空) → 792: 目前角色語音塊
    private static async ValueTask SendSingle(Session session, Packet packet, ServerContext context)
    {
        var voice = context.Db.GetVoice(session.UserId, context.Db.GetCurrentVoiceChar(session.UserId));

        var ack = new Packet(Opcode.GL_VOICEITEMSLOT_ACK).WriteU8(voice.CharIdx);
        WriteBlock(ack, voice);
        await session.SendAsync(ack);
    }

    // 793 (空) → 794: 全 15 角色語音塊 (count + count×(char_idx + 塊))
    private static async ValueTask SendAll(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GI_VOICEITEMSLOT_ALL_ACK).WriteU8(Db.VoiceCharCount);
        for (byte charIdx = 0; charIdx < Db.VoiceCharCount; charIdx++)
        {
            ack.WriteU8(charIdx);
            WriteBlock(ack, context.Db.GetVoice(session.UserId, charIdx));
        }

        await session.SendAsync(ack);
    }

    // 795 → 796: 接受變更並落地 voice_customize / voice_slots
    private static async ValueTask ApplyChange(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining == FullVariantBytes)
        {
            ApplyFull(session.UserId, packet, context.Db);      // 變體 B: 20×全量
        }
        else
        {
            ApplyDiff(session.UserId, packet, context.Db);      // 變體 A: 單角色差分
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGE_VOICEITEMSLOT_ACK)
            .WriteU8(0)                                         // err=0 成功
            .WriteU8(0));                                       // client 讀第二 byte 但未使用
    }

    // ------------------------------------------------------------------ wire
    private static void WriteBlock(Packet p, Db.Voice voice)
    {
        p.WriteS16(voice.BaseVoice1)
         .WriteS16(voice.BaseVoice2);

        foreach (var slot in voice.Slots)
        {
            p.WriteS16(slot.Item).WriteU8(slot.Flag);
        }
    }

    private static void ReadBlock(Packet packet, out short base1, out short base2, out Db.VoiceSlot[] slots)
    {
        base1 = packet.ReadS16();
        base2 = packet.ReadS16();

        slots = new Db.VoiceSlot[Db.VoiceSlotCount];
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new(packet.ReadS16(), packet.ReadU8());
        }
    }

    // ------------------------------------------------------------------ 795
    private static void ApplyFull(long userId, Packet packet, Db db)
    {
        for (int i = 0; i < 20; i++)
        {
            int charIdx = packet.ReadS32();
            ReadBlock(packet, out short base1, out short base2, out var slots);
            if (charIdx is >= 0 and < Db.VoiceCharCount)
            {
                db.SaveVoice(userId, new Db.Voice((byte)charIdx, base1, base2, slots));
            }
        }
    }

    private static void ApplyDiff(long userId, Packet packet, Db db)
    {
        byte charIdx = packet.ReadU8();
        if (charIdx >= Db.VoiceCharCount)
        {
            return;                                             // 保留槽位 (15..19) 不落地
        }

        bool baseChanged = packet.ReadBool();
        var voice = db.GetVoice(userId, charIdx);

        short base1 = voice.BaseVoice1;
        short base2 = voice.BaseVoice2;
        if (baseChanged)
        {
            base1 = packet.ReadS16();
            base2 = packet.ReadS16();
        }

        var slots = (Db.VoiceSlot[])voice.Slots.Clone();
        for (int category = 0; category < Db.VoiceCategoryCount; category++)
        {
            byte count = packet.ReadU8();
            for (int k = 0; k < count; k++)
            {
                byte slot = packet.ReadU8();                    // 1..9
                short item = packet.ReadS16();
                byte flag = packet.ReadU8();
                if (slot is >= 1 and <= Db.VoicePhraseCount)
                {
                    slots[category * Db.VoicePhraseCount + (slot - 1)] = new(item, flag);
                }
            }
        }

        db.SaveVoice(userId, new Db.Voice(charIdx, base1, base2, slots));
    }
}
