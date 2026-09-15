// =============================================================================
// Lobby-reached player configuration: nickname, character/current slot, loadout,
// NewSkill profile, and the deliberately fail-closed weapon-parts boundary.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static partial class LobbyHandlers
{
    // 210 REQ builder @0x572D30: 只有 str nick (u8+str 是 216/262 的格式)
    // → 211 ACK sub_572D80 → sub_41BBB0 (十輪逐分支讀出):
    //   1 = 可用 (訊息 0xE0), 2 = 已被使用 (格式訊息 0xDF 帶名字),
    //   0 = 一般錯誤 (彈窗 0x70/17) — 三種都停在暱稱畫面 (state:=2)
    private static async ValueTask CheckNick(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();

        byte result = (IsValidNick(nick), context.Db.IsNickTaken(nick)) switch
        {
            (false, _) => 0,                                // 非法 → 一般錯誤
            (_, true) => 2,                                 // 重複 → 0xDF 訊息
            _ => 1,                                         // 可用 → 0xE0 訊息
        };

        Console.WriteLine($"[s{session.Id}] GM_CHECKNICK_REQ: nick='{nick}' -> result={result}");
        await session.SendAsync(new Packet(Opcode.GM_CHECKNICK_ACK).WriteU8(result));
    }

    // 212 REQ builder sub_572DC0: 只有 str nick
    // → 213 ACK sub_572E70 → sub_41BD40 (十輪重大更正):
    //   ⚠ 1 = 成功 (拷貝統計欄位, state:=5 進大廳), 0 = 失敗 (state:=4)
    //   — 舊實作成功回 0 會讓 client 卡在失敗畫面!
    private static async ValueTask CreateNick(Session session, Packet packet, ServerContext context)
    {
        var nick = packet.ReadStr();
        byte result = 0;

        if (session.Authenticated && IsValidNick(nick))
        {
            long uid = context.Db.CreateNick(session.AccountId, nick);
            if (uid != 0)
            {
                (session.UserId, session.Nickname, result) = (uid, nick, (byte)1);
            }
        }

        Console.WriteLine($"[s{session.Id}] GM_CREATENICK_REQ: nick='{nick}', accountId={session.AccountId} -> result={result}, userId={session.UserId}");
        await session.SendAsync(new Packet(Opcode.GM_CREATENICK_ACK).WriteU8(result));
    }

    private static bool IsValidNick(string nick) => nick.Length is >= 2 and <= 16;

    // 214 GM_CREATECHAR_REQ (sub_532AA0: u8 char_type, s16 hair, s16 face, s16 coat)
    // → 215 ACK (sub_572F80 / sub_550170): u8 status(0=成功)
    private static async ValueTask CreateChar(Session session, Packet packet, ServerContext context)
    {
        byte charType = packet.Remaining >= 1 ? packet.ReadU8() : (byte)1;
        bool ok = session.UserId != 0 && context.Db.CreateChar(session.UserId, 0, charType);
        await session.SendAsync(new Packet(Opcode.GM_CREATECHAR_ACK).WriteU8(ok ? (byte)0 : (byte)1));
    }

    // 218 GI_CHANGEDATA_REQ (sub_523A00: u8 char_slot)
    // → 219 ACK (sub_573230): u8 status(1=成功)
    private static async ValueTask ChangeData(Session session, Packet packet, ServerContext context)
    {
        byte slotNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        if (session.UserId != 0)
        {
            context.Db.SetCurrentChar(session.UserId, slotNo);
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGEDATA_ACK).WriteU8(1));
    }

    // 312 GI_CHANGESLOT_REQ (sub_523FB0: u8 slot_no)
    // → 313 ACK (sub_573320): u8 slot_no
    private static async ValueTask ChangeSlot(Session session, Packet packet, ServerContext context)
    {
        byte slotNo = packet.Remaining >= 1 ? packet.ReadU8() : (byte)0;
        if (session.UserId != 0)
        {
            context.Db.SetCurrentChar(session.UserId, slotNo);
        }

        await session.SendAsync(new Packet(Opcode.GI_CHANGESLOT_ACK).WriteU8(slotNo));
    }

    // 220 GI_CHANGEWP_REQ (sub_573340): u8 changedCount followed by only the
    // groups whose local profile differs.  221's receiver builds a fresh
    // CClientData and then replaces the global profile, so the server reply
    // must instead carry the authoritative *four-group* snapshot.
    private static async ValueTask ChangeWeapon(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ requires an authenticated player identity.");
        }

        List<Db.WeaponGroup> changedGroups = ReadWeaponGroupChanges(packet);
        if (!context.Db.TryChangeWeaponGroups(session.UserId, changedGroups, out List<Db.WeaponGroup> groups))
        {
            // The 221 client parser exposes no rejection status byte.  Do not
            // forge an all-zero/full snapshot or a nominal success: retain the
            // existing profile and let the malformed/unauthorized request fail.
            throw new InvalidDataException("GI_CHANGEWP_REQ failed wire, ownership, or resource compatibility validation.");
        }

        var acknowledgement = new Packet(Opcode.GI_CHANGEWP_ACK).WriteU8(4);
        foreach (Db.WeaponGroup group in groups)
        {
            WriteWeaponGroup(acknowledgement, group);
        }

        await session.SendAsync(acknowledgement);
    }

    private static List<Db.WeaponGroup> ReadWeaponGroupChanges(Packet packet)
    {
        if (packet.Remaining < 1)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ is missing its group count.");
        }

        byte changedCount = packet.ReadU8();
        if (changedCount is 0 or > 4)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ group count must be 1..4.");
        }

        var groups = new List<Db.WeaponGroup>(changedCount);
        var seenGroupNumbers = new HashSet<byte>();
        for (int i = 0; i < changedCount; i++)
        {
            if (packet.Remaining < 3)
            {
                throw new InvalidDataException("GI_CHANGEWP_REQ has a truncated group prefix.");
            }

            byte groupNumber = packet.ReadU8();
            ushort primaryOffset = packet.ReadU16();
            if (groupNumber >= 4 || !seenGroupNumbers.Add(groupNumber))
            {
                throw new InvalidDataException("GI_CHANGEWP_REQ has an invalid or duplicate group number.");
            }

            ushort secondaryOffset = 0;
            ushort meleeOffset = 0;
            ushort throwOffset = 0;
            if (groupNumber != 3)
            {
                if (packet.Remaining < 6)
                {
                    throw new InvalidDataException("GI_CHANGEWP_REQ has a truncated non-switch group.");
                }

                secondaryOffset = packet.ReadU16();
                meleeOffset = packet.ReadU16();
                throwOffset = packet.ReadU16();
            }

            var parts = new int[8];
            if (primaryOffset != 0)
            {
                if (packet.Remaining < 32)
                {
                    throw new InvalidDataException("GI_CHANGEWP_REQ has a truncated primary part array.");
                }

                for (int part = 0; part < parts.Length; part++)
                {
                    parts[part] = packet.ReadS32();
                }
            }

            groups.Add(new Db.WeaponGroup(
                groupNumber, primaryOffset, secondaryOffset, meleeOffset, throwOffset, parts));
        }

        if (packet.Remaining != 0)
        {
            throw new InvalidDataException("GI_CHANGEWP_REQ has trailing bytes.");
        }

        return groups;
    }

    private static void WriteWeaponGroup(Packet packet, Db.WeaponGroup group)
    {
        packet.WriteU8(group.GroupNo)
              .WriteU16(group.PrimaryOffset);
        if (group.GroupNo != 3)
        {
            packet.WriteU16(group.SecondaryOffset)
                  .WriteU16(group.MeleeOffset)
                  .WriteU16(group.ThrowOffset);
        }

        if (group.PrimaryOffset != 0)
        {
            foreach (int part in group.Parts)
            {
                packet.WriteS32(part);
            }
        }
    }

    // 466 → 467 (sub_5738A0 / sub_573A70): target profile, a conditional
    // previous-profile seven-id save, then an authoritative raw32 profile
    // metadata record. This is unrelated to the 9×s32 sub_527550 item block.
    private static async ValueTask ChangeSkillSlot(Session session, Packet packet, ServerContext context)
    {
        NewSkillProfileChange request = NewSkillProfileWire.ReadChangeRequest(packet);
        if (session.UserId == 0)
        {
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ requires an authenticated player identity.");
        }

        Db.NewSkillProfile? selectedRecord = context.Db.ChangeNewSkillProfile(
            session.UserId,
            request.TargetProfile,
            request.HasPreviousProfileUpdate,
            request.PreviousProfile,
            request.PreviousProfilePuzzleItemIds);
        if (selectedRecord is null)
        {
            // The client parser reveals no server rejection-code mapping for
            // 467. Do not forge a nominal success or replace the server-owned
            // raw32 record with zeros; reject without state mutation instead.
            throw new InvalidDataException("GI_CHANGE_SKILLITEMSLOT_REQ failed NewSkill ownership, profile, or expiry validation.");
        }

        var profile = new NewSkillProfileRecord(
            selectedRecord.PuzzleItemIds,
            selectedRecord.ExpiresAtPackedMinute);
        // sub_573A70 unconditionally reads and discards these two raw header
        // bytes. The original success/error meanings are still unobserved;
        // retain the server's established zero convention, but never call it
        // a semantic success flag.
        Packet acknowledgement = NewSkillProfileWire.CreateChangeAcknowledgement(
            resultRaw: 0,
            unknownHeaderRaw: 0,
            profileIndex: request.TargetProfile,
            profile: profile);
        await session.SendAsync(acknowledgement);
    }

    // 912/913: the exact three operation forms are now known, but the native
    // corpus does not disclose original-server failure values or its complete
    // ownership/expiry mutation contract.  Parse strictly and fail closed;
    // never retain the former non-persistent fake 913 success.
    private static ValueTask ChangeWeaponParts(Session session, Packet packet, ServerContext context)
    {
        if (session.UserId == 0 || packet.Remaining < 1)
        {
            throw new InvalidDataException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ requires an authenticated operation.");
        }

        byte operation = packet.ReadU8();
        if (operation is not (0 or 1 or 2))
        {
            throw new InvalidDataException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ has an unknown operation.");
        }

        int requiredOperandBytes = operation == 2 ? 12 : 8;
        if (packet.Remaining != requiredOperandBytes)
        {
            throw new InvalidDataException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ has an invalid operation shape.");
        }

        _ = packet.ReadS32(); // weaponId — retained only after the mutation contract is complete.
        _ = packet.ReadS32(); // partId
        if (operation == 2)
        {
            _ = packet.ReadS32(); // oldPartId
        }

        // sub_95B180 consumes no body at all when 913.errorRaw != 0, but the
        // original nonzero code values are unresolved.  Sending an invented
        // error or a success ACK is both less faithful than rejecting with no
        // state mutation and no fabricated packet.
        throw new NotSupportedException("GL_WEAPONPARTS_EQUIP_CHANGE_REQ mutation and 913 failure values remain unresolved.");
    }
}
