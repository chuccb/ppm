// =============================================================================
// AI / PVE 防衛戰模式 handlers (docs/PACKETS.md §3.15h):
//
// 支援 AI 模式獎勵抽取 (918/919)、護盾受損 (922/923)、彈藥補給 (924-927)、
// 接關 Continue (928/929)、狂暴/Fever 模式 (935/936)、波次推進 (939/940)、
// 房內槽位重設 (944/945)。
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class AiHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GR_AI_GET_REWARD_ITEM_REQ, GetRewardItem);
        add(Opcode.GR_AI_DAMAGE_SHIELD_REQ, DamageShield);
        add(Opcode.GR_AI_RECHARGE_MAGAZINE_START_REQ, RechargeMagazineStart);
        add(Opcode.GR_AI_RECHARGE_MAGAZINE_END_REQ, RechargeMagazineEnd);
        add(Opcode.GR_AI_CONTINUE_START_REQ, ContinueStart);
        add(Opcode.GR_AI_FEVER_START_REQ, FeverStart);
        add(Opcode.GR_AI_GO_NEXT_WAVE_REQ, GoNextWave);
        add(Opcode.GR_RESET_GAMEROOMSLOT_REQ, ResetGameRoomSlot);
    }

    // 918 GR_AI_GET_REWARD_ITEM_REQ (sub_761AC0: u8 reward_idx)
    // → 919 ACK (sub_761B20): u8 idx, u8 status(0=成功), s32 item_id, u8 slot, s32 count, u8 flag
    private static async ValueTask GetRewardItem(Session session, Packet packet, ServerContext context)
    {
        byte idx = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        int rewardItemId = 10001; // 預設獎勵道具
        int count = 1;
        byte slot = session.SlotNo ?? 0;

        var ack = new Packet(Opcode.GR_AI_GET_REWARD_ITEM_ACK)
            .WriteU8(idx)
            .WriteU8(0)                  // 0 = 成功
            .WriteS32(rewardItemId)
            .WriteU8(slot)
            .WriteS32(count)
            .WriteU8(0);

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            await RoomManager.BroadcastAsync(room, ack);
        }
        else
        {
            await session.SendAsync(ack);
        }
    }

    // 922 GR_AI_DAMAGE_SHIELD_REQ (sub_7616B0: s16 shield_id, s16 damage, s16 remain, f32 unk)
    // → 923 ACK (sub_761710): 同步給房間內所有玩家
    private static async ValueTask DamageShield(Session session, Packet packet, ServerContext context)
    {
        short shieldId = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        short damage = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        short remain = packet.Remaining >= 2 ? packet.ReadS16() : (short)0;
        float unk = packet.Remaining >= 4 ? packet.ReadF32() : 0f;

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_DAMAGE_SHIELD_ACK)
                .WriteS16(shieldId)
                .WriteS16(damage)
                .WriteS16(remain)
                .WriteF32(unk);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }

    // 924 GR_AI_RECHARGE_MAGAZINE_START_REQ (sub_558350: u8 slot, u8 team, u8 unk)
    // → 925 ACK (sub_558550): 同步給房間內所有玩家
    private static async ValueTask RechargeMagazineStart(Session session, Packet packet, ServerContext context)
    {
        byte slot = packet.Remaining >= 1 ? packet.ReadU8() : (session.SlotNo ?? 0);
        byte team = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        byte unk = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_RECHARGE_MAGAZINE_START_ACK)
                .WriteU8(slot)
                .WriteU8(team)
                .WriteU8(unk);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }

    // 926 GR_AI_RECHARGE_MAGAZINE_END_REQ (sub_5586B0: u8 slot, u8 team, s8 status)
    // → 927 ACK (sub_558880): 同步給房間內所有玩家
    private static async ValueTask RechargeMagazineEnd(Session session, Packet packet, ServerContext context)
    {
        byte slot = packet.Remaining >= 1 ? packet.ReadU8() : (session.SlotNo ?? 0);
        byte team = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        sbyte status = packet.Remaining >= 1 ? (sbyte)packet.ReadU8() : (sbyte)0;

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_RECHARGE_MAGAZINE_END_ACK)
                .WriteU8(slot)
                .WriteU8(team)
                .WriteU8((byte)status);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }

    // 928 GR_AI_CONTINUE_START_REQ (sub_761DB0: s32 continue_count)
    // → 929 ACK (sub_761E90): u8 status(1=成功), s32 continue_count
    private static async ValueTask ContinueStart(Session session, Packet packet, ServerContext context)
    {
        int count = packet.Remaining >= 4 ? packet.ReadS32() : 1;
        var ack = new Packet(Opcode.GR_AI_CONTINUE_START_ACK)
            .WriteU8(1)               // status 1 = 成功
            .WriteS32(count);

        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            await RoomManager.BroadcastAsync(room, ack);
        }
        else
        {
            await session.SendAsync(ack);
        }
    }

    // 935 GR_AI_FEVER_START_REQ (sub_7622C0: 空)
    // → 936 ACK (sub_7623A0): u8 status(1), u8 unk(0), s32 time(10000), u8 fever_type(1)
    private static async ValueTask FeverStart(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            var ack = new Packet(Opcode.GR_AI_FEVER_START_ACK)
                .WriteU8(1)           // status
                .WriteU8(0)
                .WriteS32(10000)      // duration ms
                .WriteU8(1);          // fever mode type
            await RoomManager.BroadcastAsync(room, ack);
        }
    }

    // 939 GR_AI_GO_NEXT_WAVE_REQ (sub_75CE40: 空)
    // → 940 ACK (sub_7613D0): u8 next_wave, s32 wave_time
    private static async ValueTask GoNextWave(Session session, Packet packet, ServerContext context)
    {
        if (session.RoomNo is { } rno && context.Rooms.Find(rno) is { } room)
        {
            byte nextWave = 1; // 下一波
            var ack = new Packet(Opcode.GR_AI_GO_NEXT_WAVE_ACK)
                .WriteU8(nextWave)
                .WriteS32(0);
            await RoomManager.BroadcastAsync(room, ack);
        }
    }

    // 944 GR_RESET_GAMEROOMSLOT_REQ (sub_585E90: 空) → 945 ACK (sub_585F30): u8 status(1)
    private static async ValueTask ResetGameRoomSlot(Session session, Packet packet, ServerContext context)
    {
        var ack = new Packet(Opcode.GR_RESET_GAMEROOMSLOT_ACK).WriteU8(1);
        await session.SendAsync(ack);
    }
}
