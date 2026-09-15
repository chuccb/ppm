// =============================================================================
// 語音自訂存取 — voice_customize / voice_slots (791/792, 793/794, 795/796)。
//
//   wire 語音塊 (sub_876B00/sub_876C90 讀序定案):
//     s16 base_voice1, s16 base_voice2,
//     3 類 (command/tactics/infomation) × 9 句 × {s16 voice_item, u8 flag}
//   char_idx = 0..14 (0=maru…14=devilgirl, sub_8859B0; models/type1..15 = idx+1);
//   DB characters.char_type 為 1..15 (1=maru…15=devilgirl, spy_11/robotgirl_12
//   名字即內嵌其 char_type 11/12), 故 char_idx = char_type - 1。
//   voice_item = 語音表偏移 (unk_EAFC40 起, 0=未選用預設);
//   flag = 該槽位置 1..9 (UI 寫入 slot+2, 0=未自訂/預設) — server 原樣儲存。
// =============================================================================
using Microsoft.Data.Sqlite;

namespace PaperMan.Server;

public sealed partial class Db
{
    public const int VoiceCharCount = 15;                   // char_idx 0..14
    public const int VoiceCategoryCount = 3;                // command/tactics/infomation
    public const int VoicePhraseCount = 9;                  // 每類 9 句
    public const int VoiceSlotCount = VoiceCategoryCount * VoicePhraseCount;   // 27

    /// <summary>單一語音槽 — Item 0 = 用角色原生語音, Flag = 槽位置 1..9。</summary>
    public readonly record struct VoiceSlot(short Item, byte Flag);

    /// <summary>單角色語音設定 (Slots 恆為 27 個, 未自訂者 Item=0/Flag=0)。</summary>
    public sealed record Voice(byte CharIdx, short BaseVoice1, short BaseVoice2, VoiceSlot[] Slots);

    /// <summary>目前角色的語音 char_idx (0..14); 無角色回 0 (maru)。</summary>
    public byte GetCurrentVoiceChar(long userId)
    {
        lock (_gate)
        {
            using var cmd = Cmd("""
                SELECT c.char_type FROM users u
                JOIN characters c ON c.user_id = u.user_id AND c.slot_no = u.current_char
                WHERE u.user_id = @u
                """, ("@u", userId));
            using var r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return 0;
            }

            int type = r.GetInt32(0);                       // char_type 1..15 → char_idx 0..14
            return (byte)(type is >= 1 and <= VoiceCharCount ? type - 1 : 0);
        }
    }

    /// <summary>讀取單角色語音 (無記錄 → 全 0 = 角色原生)。</summary>
    public Voice GetVoice(long userId, byte charIdx)
    {
        lock (_gate)
        {
            short base1 = 0;
            short base2 = 0;
            using (var cmd = Cmd("""
                SELECT base_voice1, base_voice2 FROM voice_customize
                WHERE user_id=@u AND char_idx=@c
                """, ("@u", userId), ("@c", (int)charIdx)))
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    base1 = (short)r.GetInt32(0);
                    base2 = (short)r.GetInt32(1);
                }
            }

            var slots = new VoiceSlot[VoiceSlotCount];
            using (var cmd = Cmd("""
                SELECT slot_no, item_id, flag FROM voice_slots
                WHERE user_id=@u AND char_idx=@c
                """, ("@u", userId), ("@c", (int)charIdx)))
            using (var r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    int no = r.GetInt32(0);
                    if (no is >= 0 and < VoiceSlotCount)
                    {
                        slots[no] = new((short)r.GetInt32(1), (byte)r.GetInt32(2));
                    }
                }
            }

            return new(charIdx, base1, base2, slots);
        }
    }

    /// <summary>整筆覆寫單角色語音 (base + 全 27 槽, 原子)。</summary>
    public void SaveVoice(long userId, Voice voice)
    {
        lock (_gate)
        {
            using var tx = _conn.BeginTransaction();
            try
            {
                SqliteCommand TxCmd(string sql, params ReadOnlySpan<(string, object?)> args)
                {
                    var cmd = _conn.CreateCommand();
                    cmd.Transaction = tx;
                    cmd.CommandText = sql;
                    foreach (var (name, value) in args)
                    {
                        cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
                    }

                    return cmd;
                }

                using (var cmd = TxCmd("""
                    INSERT INTO voice_customize (user_id, char_idx, base_voice1, base_voice2)
                    VALUES (@u, @c, @b1, @b2)
                    ON CONFLICT(user_id, char_idx)
                    DO UPDATE SET base_voice1=@b1, base_voice2=@b2
                    """, ("@u", userId), ("@c", (int)voice.CharIdx),
                    ("@b1", (int)voice.BaseVoice1), ("@b2", (int)voice.BaseVoice2)))
                {
                    cmd.ExecuteNonQuery();
                }

                for (int i = 0; i < voice.Slots.Length; i++)
                {
                    using var cmd = TxCmd("""
                        INSERT INTO voice_slots (user_id, char_idx, slot_no, item_id, flag)
                        VALUES (@u, @c, @s, @i, @f)
                        ON CONFLICT(user_id, char_idx, slot_no)
                        DO UPDATE SET item_id=@i, flag=@f
                        """, ("@u", userId), ("@c", (int)voice.CharIdx), ("@s", i),
                        ("@i", (int)voice.Slots[i].Item), ("@f", (int)voice.Slots[i].Flag));
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
