// =============================================================================
// GQ_QUEST_ACCEPT_REQ (867) → GQ_QUEST_ACCEPT_ACK (868)
// File and handler entry use the canonical opcode token verbatim. Packet-specific
// helpers retain the request or paired ACK token that defines their wire family.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class QuestHandlers
{
    // =============================================================================
    // 任務 handlers — GQ_QUEST 家族 (八輪全家逐行讀畢, docs/PACKETS.md §3.13)。
    //
    // 線上 13-byte 快照 = {s32 quest_index, s32 progress, u8 state, s32 extra}
    // state: 0=NONE, 1=WORKING, 2=SUCCESS, 3=FAILED
    // (sub_91C7B0 的除錯字串 QUEST_STATE_WORKING/SUCCESS/FAILED 直接印證)
    //
    //   867 ACCEPT_REQ  (sub_91CB80): s32 quest_index
    //   868 ACCEPT_ACK  (sub_91CC70): u8 result; OK → 13B 快照
    //   869 CANCEL_REQ:  s32 quest_index
    //   870 CANCEL_ACK  (sub_91D290): u8 result (!=0 → s32 idx, s32)
    //   864 DATETIME_REQ → 865 ACK (sub_585AB0): s32 unix_time
    // =============================================================================
    /// <summary>線上任務狀態 (sub_91C7B0 除錯字串命名)。</summary>
    private enum GQ_QUEST_ACCEPT_ACK_SnapshotState : byte
    {
        None = 0,
        Working = 1,
        Success = 2,
        Failed = 3,
    }

    // 867 → 868: u8 result (0=OK); OK 時附 13B 快照
    private static async ValueTask GQ_QUEST_ACCEPT_REQ(Session session, Packet packet, ServerContext context)
    {
        int questIndex = packet.ReadS32();
        bool ok = session.UserId != 0 && context.Db.AcceptQuest(session.UserId, questIndex);

        var ack = new Packet(Opcode.GQ_QUEST_ACCEPT_ACK);
        if (ok)
        {
            ack.WriteU8(0);
            WriteGQ_QUEST_ACCEPT_ACK_Snapshot(ack, questIndex, progress: 0, GQ_QUEST_ACCEPT_ACK_SnapshotState.Working);
        }
        else
        {
            ack.WriteU8(1)
               .WriteS32(questIndex);                       // result!=0 → s32 idx
        }

        await session.SendAsync(ack);
    }

    /// <summary>13-byte 線上快照 (sub_592500(a2, &v4, 0xD))。</summary>
    private static void WriteGQ_QUEST_ACCEPT_ACK_Snapshot(Packet ack, int questIndex, int progress, GQ_QUEST_ACCEPT_ACK_SnapshotState state) =>
        ack.WriteS32(questIndex)
           .WriteS32(progress)
           .WriteU8((byte)state)
           .WriteS32(0);                                    // extra
}
