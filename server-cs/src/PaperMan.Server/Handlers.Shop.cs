// =============================================================================
// Shop request boundary
//
// The client wire shapes below are direct consumer/builder evidence.  The
// original service's catalog, price, entitlement, reward, and probability
// policies are not present in the client or Extracted resources.  Therefore
// all purchase, sale, gift, bag/package, Pepachi, and capsule paths fail
// closed: a structurally valid failure ACK, no request-dependent decoding, and
// no wallet/inventory/gift mutation.  Do not turn any of these into success
// paths without evidence for both the server policy and its success payload.
// =============================================================================
using System.Buffers.Binary;
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class ShopHandlers
{
    // `206` has no name in the native opcode-name registry. `sub_571620`
    // constructs it immediately before the 207 consumer, so this local name
    // is an implementation label rather than a recovered native symbol.
    private const Opcode BuyWeaponPartsRequestOpcode = (Opcode)206;

    public static void Register(Registrar add)
    {
        add(Opcode.GS_CASH_REQ, Cash);
        add(Opcode.GS_BUYITEM_REQ, BuyItems);
        add(BuyWeaponPartsRequestOpcode, BuyWeaponParts);
        add(Opcode.GS_SELLITEM_REQ, SellItem);
        add(Opcode.GS_GIVEGIFT_REQ, GiveGift);
        add(Opcode.GS_BUYCHAR_REQ, BuyCharacter);
        add(Opcode.GS_BUYCASHITEM_REQ, BuyCashItems);
        add(Opcode.GS_DELETEGIFT_REQ, DeleteGift);
        add(Opcode.GS_BUY_HUKUBUKURO_REQ, BuyHukubukuro);
        add(Opcode.GS_GET_HUKUBUKURO_REQ, GetHukubukuro);
        add(Opcode.GS_BUY_ONCEITEM_REQ, BuyOnceItem);
        add(Opcode.GS_GET_PRESENTPACKAGE_REQ, GetPresentPackage);
        add(Opcode.GS_DESTROYITEM_REQ, DestroyItem);
        add(Opcode.GS_HIDDEN_ITEM_LIST_REQ, HiddenItemList);
        add(Opcode.GP_ENTER_PEPACHI_REQ, EnterPepachi);
        add(Opcode.GP_START_GAME_REQ, StartPepachi);
        add(Opcode.GP_PEPACHI_LIST_REQ, PepachiList);
        add(Opcode.GS_CAPSULEMACHINE_START_REQ, StartCapsuleMachine);
    }

    // 356 → 357. `sub_572380` sends an empty request; `sub_572420` always
    // reads {u8 status, s32 rawCash}. A success status/balance would assert
    // unverified billing state.
    private static ValueTask Cash(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_CASH_ACK)
            .WriteU8(0)
            .WriteS32(0));
    }

    // 204 → 205. `sub_571910` reads this full count==0 failure arm before its
    // unconditional seven-s32 trailer. Its two error bytes are raw; zero is
    // only a structurally neutral value, not an asserted original error code.
    private static ValueTask BuyItems(Session session, Packet packet, ServerContext context)
    {
        if (!IsBulkPurchaseRequest(packet, requireHukubukuroItem: false))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(NewBulkPurchaseFailure());
    }

    // 206 is exactly {s32 itemId,s32 rawContext,u8 itemKind,s32 rawPeriod}.
    // In `sub_571B60`, raw result zero enters the success decoder and mutates
    // the local parts/wallet cache. Any nonzero result has no tail.
    private static ValueTask BuyWeaponParts(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 13)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUY_WEAPONPARTS_ACK).WriteU8(1));
    }

    // 208 is exactly one s32. `sub_572B80` reads a byte and only a nonzero
    // value consumes the item/wallet tail and removes a local inventory record.
    private static ValueTask SellItem(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 4)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_SELLITEM_ACK).WriteU8(0));
    }

    // 296 has a six-byte compact form and a NUL-terminated-string form. Result
    // zero has a five-s32 success-only balance tail; result one is a no-tail
    // client error arm.
    private static ValueTask GiveGift(Session session, Packet packet, ServerContext context)
    {
        if (!IsGiftRequest(packet))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_GIVEGIFT_ACK).WriteU8(1));
    }

    // 310 is exactly six s32 values. Although the client accepts a success
    // appearance vector, the source resources do not establish original
    // slot/payment entitlement. A failure has an always-read account-update pair.
    private static ValueTask BuyCharacter(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 24)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUYCHAR_ACK)
            .WriteU8(0)
            .WriteU8(0)
            .WriteS32(0));
    }

    // 358 is {u8 count, count×{s32 itemId,s32 clientCalculatedPrice}}.
    // `sub_5725D0` always reads {u8 resultCount, s32 rawHeader}; zero result
    // count has no item records and does not mutate client state.
    private static ValueTask BuyCashItems(Session session, Packet packet, ServerContext context)
    {
        if (!IsCashPurchaseRequest(packet))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUYCASHITEM_ACK)
            .WriteU8(0)
            .WriteS32(0));
    }

    // 453 is exactly {s32 giftId, s32 itemId}; `sub_57BCF0` always reads the
    // same identifiers from 454. A status other than one preserves the native
    // client's cached gifts. The original selection/deletion policy is not
    // recovered, so this handler echoes only its non-mutating failure arm.
    private static ValueTask DeleteGift(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 8)
        {
            return ValueTask.CompletedTask;
        }

        int giftId = packet.ReadS32();
        int itemId = packet.ReadS32();
        return session.SendAsync(new Packet(Opcode.GS_DELETEGIFT_ACK)
            .WriteU8(0)
            .WriteS32(giftId)
            .WriteS32(itemId));
    }

    // `sub_571100` starts from the normal 204 bulk-purchase body and switches
    // its opcode to 468 only when it contains a Hukubukuro-range item. The 469
    // consumer reads just this status when it is nonzero; its success tail is
    // wallet/item state and is deliberately not fabricated.
    private static ValueTask BuyHukubukuro(Session session, Packet packet, ServerContext context)
    {
        if (!IsBulkPurchaseRequest(packet, requireHukubukuroItem: true))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_BUY_HUKUBUKURO_ACK).WriteU8(1));
    }

    // `sub_57B2E0` sends 470 for Hukubukuro-range item IDs. 471's nonzero
    // status consumes no list and only displays the client's error.
    private static ValueTask GetHukubukuro(Session session, Packet packet, ServerContext context)
    {
        if (!IsPackageDetailRequest(packet, IsHukubukuroItemId))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_GET_HUKUBUKURO_ACK).WriteU8(1));
    }

    // 695 has multiple native request forms. Do not parse a presumed common
    // request shape. `sub_571D70` reads {u8 rawResult,s32 rawItemOrClass}; a
    // zero rawResult then unconditionally consumes one additional raw s32.
    private static ValueTask BuyOnceItem(Session session, Packet packet, ServerContext context) =>
        session.SendAsync(new Packet(Opcode.GS_BUY_ONCEITEM_ACK)
            .WriteU8(0)
            .WriteS32(0)
            .WriteS32(0));

    // `sub_57B2E0` routes the two PresentPackage ranges to 780. 781's
    // nonzero status has no list tail, unlike its successful item list.
    private static ValueTask GetPresentPackage(Session session, Packet packet, ServerContext context)
    {
        if (!IsPackageDetailRequest(packet, IsPresentPackageItemId))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_GET_PRESENTPACKAGE_ACK).WriteU8(1));
    }

    // 802's request layout remains unresolved. `sub_895EE0` proves this exact
    // no-mutation 803 failure arm; no request bytes are consumed.
    private static ValueTask DestroyItem(Session session, Packet packet, ServerContext context) =>
        session.SendAsync(new Packet(Opcode.GS_DESTROYITEM_ACK)
            .WriteU8(1)
            .WriteU8(0)
            .WriteU8(0));

    // 806 is exactly one signed category selector. Both recovered 807 readers
    // consume a byte, a u16 count, and a u16 category before their record loops.
    // A count of zero skips every unverified server-controlled record and still
    // lets the client resolve its resource-backed base shop/parts view. The
    // first u8 has no recovered reader use; zero is only a structural value.
    private static ValueTask HiddenItemList(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return ValueTask.CompletedTask;
        }

        short category = packet.ReadS16();
        if (!IsNativeHiddenItemCategory(category))
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_HIDDEN_ITEM_LIST_ACK)
            .WriteU8(0)
            .WriteU16(0)
            .WriteU16((ushort)category));
    }

    // 698 is empty. In CLobbyShop::sub_46AD00, only status==1 is the entry
    // success branch; all three fields are read before that branch.
    private static ValueTask EnterPepachi(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GP_ENTER_PEPACHI_ACK)
            .WriteU8(0)
            .WriteS32(0)
            .WriteS32(0));
    }

    // 700 is exactly {u8 selector,s32 selectedCharacterId}. The four selector
    // values below are direct caller values; the item-id range is the native
    // character-body family from which that writer derives its second field.
    // `sub_84A490` uses this complete two-byte failure arm for 701. Only a
    // first byte of exactly one opens the award/reel decoder; do not forge it.
    private static ValueTask StartPepachi(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 5)
        {
            return ValueTask.CompletedTask;
        }

        byte selector = packet.ReadU8();
        int selectedCharacterId = packet.ReadS32();
        if (selector is not (1 or 2 or 4 or 5)
            || selectedCharacterId is < 19_900_001 or > 19_900_015)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GP_START_GAME_ACK)
            .WriteU8(0)
            .WriteU8(0));
    }

    // 702 is empty. 703 is {s32 start,s32 count,(start+count)×s16}; `{0,0}`
    // is its structurally empty list and never a reward grant.
    private static ValueTask PepachiList(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 0)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GP_PEPACHI_LIST_ACK)
            .WriteS32(0)
            .WriteS32(0));
    }

    // 900 is exactly {u8 paymentSelector,u8 drawCount}. The listed pairs are
    // the direct caller combinations, including code paths whose XML buttons
    // are commented out in this resource revision. `sub_9A1A30` always reads
    // count and three trailing s32s even for failure. Count zero prevents
    // per-award reads and nonzero status avoids wallet/reward updates.
    private static ValueTask StartCapsuleMachine(Session session, Packet packet, ServerContext context)
    {
        if (packet.Remaining != 2)
        {
            return ValueTask.CompletedTask;
        }

        byte paymentSelector = packet.ReadU8();
        byte drawCount = packet.ReadU8();
        bool isNativeCallerPair = (paymentSelector, drawCount) is (1, 1) or (1, 10) or (2, 1) or (3, 1);
        if (!isNativeCallerPair)
        {
            return ValueTask.CompletedTask;
        }

        return session.SendAsync(new Packet(Opcode.GS_CAPSULEMACHINE_START_ACK)
            .WriteU8(1)
            .WriteS32(0)
            .WriteS32(0)
            .WriteS32(0)
            .WriteS32(0));
    }

    private static Packet NewBulkPurchaseFailure() =>
        new Packet(Opcode.GS_BUYITEM_ACK)
            .WriteU8(0)
            .WriteU8(0)
            .WriteU8(0)
            .WriteS32(0).WriteS32(0).WriteS32(0).WriteS32(0)
            .WriteS32(0).WriteS32(0).WriteS32(0);

    // 204 and 468 have one body grammar. `sub_571100` changes the opcode to
    // 468 iff at least one selected ID is in a Hukubukuro range. This checks
    // only native framing/routing, not purchase authority or resource price.
    private static bool IsBulkPurchaseRequest(Packet packet, bool requireHukubukuroItem)
    {
        ReadOnlySpan<byte> payload = packet.Payload;
        if (payload.Length < 1 || payload[0] == 0)
        {
            return false;
        }

        int offset = 1;
        bool hasHukubukuroItem = false;
        for (int i = 0; i < payload[0]; i++)
        {
            if (payload.Length - offset < 7)
            {
                return false;
            }

            int itemId = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(offset, 4));
            byte itemKind = payload[offset + 4];
            offset += 7;
            hasHukubukuroItem |= IsHukubukuroItemId(itemId);

            if (itemKind is 12 or 13 or 17)
            {
                if (payload.Length - offset < 2
                    || BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, 2)) >= 0)
                {
                    return false;
                }

                offset += 2;
            }
        }

        return offset == payload.Length && hasHukubukuroItem == requireHukubukuroItem;
    }

    private static bool IsCashPurchaseRequest(Packet packet)
    {
        ReadOnlySpan<byte> payload = packet.Payload;
        return payload.Length >= 1 && payload.Length == 1 + payload[0] * 8;
    }

    private static bool IsGiftRequest(Packet packet)
    {
        ReadOnlySpan<byte> payload = packet.Payload;
        if (payload.Length == 6)
        {
            return true;
        }

        int recipientLength = payload.IndexOf((byte)0);
        if (recipientLength <= 0)
        {
            return false;
        }

        int offset = recipientLength + 1;
        if (offset >= payload.Length)
        {
            return false;
        }

        byte messageLengthRaw = payload[offset++];
        if (messageLengthRaw != 0)
        {
            int messageLength = payload[offset..].IndexOf((byte)0);
            if (messageLength <= 0)
            {
                return false;
            }

            offset += messageLength + 1;
        }

        if (payload.Length - offset < 6)
        {
            return false;
        }

        byte itemKind = payload[offset + 4];
        offset += 6;
        if (itemKind is 12 or 13 or 17)
        {
            if (payload.Length - offset != 2
                || BinaryPrimitives.ReadInt16LittleEndian(payload.Slice(offset, 2)) >= 0)
            {
                return false;
            }

            offset += 2;
        }

        return offset == payload.Length;
    }

    private static bool IsPackageDetailRequest(Packet packet, Func<int, bool> hasExpectedItemRange)
    {
        if (packet.Remaining != 10)
        {
            return false;
        }

        int itemId = BinaryPrimitives.ReadInt32LittleEndian(packet.Payload.Slice(4, 4));
        short encodedVariant = BinaryPrimitives.ReadInt16LittleEndian(packet.Payload.Slice(8, 2));
        return encodedVariant < 0 && hasExpectedItemRange(itemId);
    }

    // Native shop UI emits 1..13 and 15..24; 14 has no recovered sender.
    // CLobbyPartsUpRoom independently emits 25 during initialization.
    private static bool IsNativeHiddenItemCategory(short category) =>
        category is >= 1 and <= 13
            or >= 15 and <= 25;

    private static bool IsHukubukuroItemId(int itemId) =>
        itemId is >= 15_301_001 and <= 15_302_000
            or >= 15_310_001 and <= 15_320_000;

    private static bool IsPresentPackageItemId(int itemId) =>
        itemId is >= 15_302_001 and <= 15_304_000
            or >= 15_320_001 and <= 15_330_000;
}
