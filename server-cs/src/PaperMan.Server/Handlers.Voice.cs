// =============================================================================
// 語音自訂 handlers — CVCustomizeManager 系 (卅六輪逐行定案):
//
//   791 GL_VOICEITEMSLOT_REQ (空) → 792 ACK: 單角色語音塊
//   793 GI_VOICEITEMSLOT_ALL_REQ (空) → 794 ACK: 全 20 角色語音塊
//   795 GI_CHANGE_VOICEITEMSLOT_REQ (兩變體) → 796 ACK: u8 err, u8
//
//   語音塊結構 (與 voice_customize_contents.xml 互證 — 3 類×9 句:
//   command/tactics/infomation):
//     全量 = 20 × { s32 char_idx, s16 base_voice, s16, 3×9×{s16, u8} }
//     單角色 = u8 char, u8 base_changed, [s16, s16],
//              3×{u8 n, n×{u8 slot(1..9), s16 item, u8 flag}}
//   796 err≠0 → client 顯示 0x3FB 並用附帶單角色塊回滾;
//   client 有 pending 佇列 (791 進行中的變更自動重送)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class VoiceHandlers
{
    private const int CharacterSlotCount = 20;
    private const int VoiceCategoryCount = 3;               // command/tactics/infomation
    private const int VoicePhraseCount = 9;                 // 每類 9 句

    public static void Register(Registrar add)
    {
        add(Opcode.GL_VOICEITEMSLOT_REQ, SendSingle);
        add(Opcode.GI_VOICEITEMSLOT_ALL_REQ, SendAll);
        add(Opcode.GI_CHANGE_VOICEITEMSLOT_REQ, ApplyChange);
    }

    // 791 (空) → 792: 單角色語音塊 (預設全空 = 用角色原生語音)
    private static async ValueTask SendSingle(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GL_VOICEITEMSLOT_ACK)
            .WriteU8(1)                                     // char_type
            .WriteU8(0);                                    // base_changed=0 → 無 s16 對

        for (int category = 0; category < VoiceCategoryCount; category++)
        {
            ack.WriteU8(0);                                 // 該類變更數 0
        }

        await session.SendAsync(ack);
    }

    // 793 (空) → 794: 全 20 角色語音塊 (預設全 0)
    private static async ValueTask SendAll(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GI_VOICEITEMSLOT_ALL_ACK);

        for (int characterIndex = 0; characterIndex < CharacterSlotCount; characterIndex++)
        {
            ack.WriteS32(characterIndex)
               .WriteS16(0)                                 // base_voice
               .WriteS16(0);

            for (int category = 0; category < VoiceCategoryCount; category++)
            {
                for (int phrase = 0; phrase < VoicePhraseCount; phrase++)
                {
                    ack.WriteS16(0)                         // voice_item 偏移
                       .WriteU8(0);                         // flag
                }
            }
        }

        await session.SendAsync(ack);
    }

    // 795 (兩變體) → 796: 接受變更 (單機直接成功; 進階可落 voice_slots 表)
    private static async ValueTask ApplyChange(Session session, Packet packet, ServerContext context)
    {
        await session.SendAsync(new Packet(Opcode.GI_CHANGE_VOICEITEMSLOT_ACK)
            .WriteU8(0)                                     // err=0 成功
            .WriteU8(0));
    }
}
