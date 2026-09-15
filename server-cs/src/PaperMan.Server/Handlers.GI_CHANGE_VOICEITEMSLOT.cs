// =============================================================================
// GI_CHANGE_VOICEITEMSLOT_REQ (795) → GI_CHANGE_VOICEITEMSLOT_ACK (796)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class VoiceHandlers
{
    /// <summary>795 全量變體 B 的固定長度 = 20 × (s32 + 2×s16 + 27×(s16+u8))。</summary>
    private const int GI_CHANGE_VOICEITEMSLOT_REQ_FullVariantBytes = 20 * (4 + 4 + Db.VoiceSlotCount * 3);

    // 795 → 796: 接受變更並落地 voice_customize / voice_slots
    private static async ValueTask GI_CHANGE_VOICEITEMSLOT_REQ(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining == GI_CHANGE_VOICEITEMSLOT_REQ_FullVariantBytes)
        {
            ApplyGI_CHANGE_VOICEITEMSLOT_REQ_Full(session.UserId, packet, context.Db);      // 變體 B: 20×全量
        }
        else
        {
            ApplyGI_CHANGE_VOICEITEMSLOT_REQ_Diff(session.UserId, packet, context.Db);      // 變體 A: 單角色差分
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGE_VOICEITEMSLOT_ACK)
            .WriteU8(0)                                         // err=0 成功
            .WriteU8(0));                                       // client 讀第二 byte 但未使用
    }

    private static void ReadGI_CHANGE_VOICEITEMSLOT_REQ_Block(Packet packet, out short base1, out short base2, out Db.VoiceSlot[] slots)
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
    private static void ApplyGI_CHANGE_VOICEITEMSLOT_REQ_Full(long userId, Packet packet, Db db)
    {
        for (int i = 0; i < 20; i++)
        {
            int charIdx = packet.ReadS32();
            ReadGI_CHANGE_VOICEITEMSLOT_REQ_Block(packet, out short base1, out short base2, out var slots);
            if (charIdx is >= 0 and < Db.VoiceCharCount)
            {
                db.SaveVoice(userId, new Db.Voice((byte)charIdx, base1, base2, slots));
            }
        }
    }

    private static void ApplyGI_CHANGE_VOICEITEMSLOT_REQ_Diff(long userId, Packet packet, Db db)
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
