// =============================================================================
// Voice packet-family support
// This contains no receive entry. Its helpers encode or validate structures shared
// by the direct canonical request/ACK-family handler source files in this domain.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class VoiceHandlers
{
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
    // ------------------------------------------------------------------ wire
    private static void WriteVoiceBlock(Packet p, Db.Voice voice)
    {
        p.WriteS16(voice.BaseVoice1)
         .WriteS16(voice.BaseVoice2);

        foreach (var slot in voice.Slots)
        {
            p.WriteS16(slot.Item).WriteU8(slot.Flag);
        }
    }
}
