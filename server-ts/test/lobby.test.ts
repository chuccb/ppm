import { describe, expect, test } from "bun:test";
import { decode } from "../src/packet.ts";
import { build } from "../src/ops/registry.ts";
import { Store } from "../src/store.ts";

describe("lobby bootstrap packets", () => {
  test("creates one canonical starter and reuses it", async () => {
    const store = new Store();
    const account = await store.createAccount("alice", "pw");

    const first = store.ensurePlayerIdentity(account.id);
    const second = store.ensurePlayerIdentity(account.id);
    expect(first).not.toBeNull();
    expect(second).toEqual(first);
    expect(first?.nickname).toBe("alice");
    expect(first?.currentChar).toBe(0);
    expect(first?.characters).toEqual([
      { slotNo: 0, charType: 1, equip: [1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0] },
    ]);

    const snapshot = store.getNewSkillProfileSnapshot(first!.userId);
    expect(snapshot.selectedProfile).toBe(0);
    expect(snapshot.profiles).toHaveLength(5);
    expect(snapshot.profiles).toEqual(
      Array.from({ length: 5 }, () => ({
        puzzleItemIds: [0, 0, 0, 0, 0, 0, 0],
        expiresAtPackedMinute: 0,
      })),
    );

    const inventoryEnter = decode(build("GL_INVENIN_ACK", first!.userId, 7, snapshot).encode());
    expect(inventoryEnter.u8()).toBe(1);
    expect(inventoryEnter.s32()).toBe(first!.userId);
    expect(inventoryEnter.u8()).toBe(7);
    expect(inventoryEnter.u8()).toBe(0);
    expect(inventoryEnter.u8()).toBe(0);
    for (let profile = 0; profile < 5; profile++) {
      for (let slot = 0; slot < 7; slot++) expect(inventoryEnter.s32()).toBe(0);
      expect(inventoryEnter.s32()).toBe(0);
    }
    expect(inventoryEnter.remaining).toBe(0);
    store.close();
  });

  test("198 writes a successful minimal but complete CClientData", async () => {
    const store = new Store();
    const account = await store.createAccount("bob", "pw");
    const myInfo = store.ensurePlayerIdentity(account.id);
    expect(myInfo).not.toBeNull();
    expect(store.getMyInfo(myInfo!.userId)).toEqual(myInfo);

    const selectedSnapshot = {
      selectedProfile: 1,
      profiles: Array.from({ length: 5 }, (_, profile) => ({
        puzzleItemIds: profile === 1
          ? [11010001, 11020001, 11030001, 11040001, 11050001, 11060001, 11060002]
          : [0, 0, 0, 0, 0, 0, 0],
        expiresAtPackedMinute: profile === 1 ? 0x12345678 : 0,
      })),
    };
    const reader = decode(build("GL_MYINFO_ACK", myInfo, selectedSnapshot).encode());
    expect(reader.u8()).toBe(1);
    expect(reader.s32()).toBe(myInfo!.userId);
    expect(reader.str()).toBe("bob");
    expect(reader.u8()).toBe(0); // selected character-list index
    expect(reader.s32()).toBe(1); // level
    expect(reader.s32()).toBe(0); // exp
    for (let i = 0; i < 19; i++) expect(reader.s32()).toBe(0);
    expect(reader.raw(3)).toEqual(new Uint8Array(3));
    expect(reader.s32()).toBe(0); // cash
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.raw(48)).toEqual(new Uint8Array(48));
    expect(reader.u8()).toBe(0); // current character slot
    expect(reader.u8()).toBe(1); // character count
    expect(reader.u8()).toBe(1); // canonical type 1
    expect(Array.from({ length: 12 }, () => reader.u16())).toEqual([
      1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
    ]);
    expect(reader.u8()).toBe(4); // weapon group count
    for (let group = 0; group < 4; group++) {
      expect(reader.u8()).toBe(group);
      expect(reader.u16()).toBe(0);
      if (group !== 3) {
        expect(reader.u16()).toBe(0);
        expect(reader.u16()).toBe(0);
        expect(reader.u16()).toBe(0);
      }
    }
    for (let i = 0; i < 9; i++) expect(reader.s32()).toBe(0);
    expect(reader.u8()).toBe(5);
    expect(Array.from({ length: 7 }, () => reader.s32())).toEqual([
      11010001,
      11020001,
      11030001,
      11040001,
      11050001,
      11060001,
      11060002,
    ]);
    expect(reader.u16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.u8()).toBe(0);
    expect(reader.remaining).toBe(0);
    const publicMyInfo = store.getMyInfoByNickname("bob");
    expect(publicMyInfo).toEqual(myInfo);
    const publicInfo = decode(build("GL_CLIENTINFO_ACK", publicMyInfo).encode());
    expect(publicInfo.u8()).toBe(1);
    expect(publicInfo.str()).toBe("bob");
    expect(publicInfo.u8()).toBe(0);
    expect(publicInfo.s32()).toBe(1);
    expect(publicInfo.s32()).toBe(0);
    for (let i = 0; i < 19; i++) expect(publicInfo.s32()).toBe(0);
    expect(publicInfo.raw(3)).toEqual(new Uint8Array(3));
    expect(publicInfo.s32()).toBe(0);
    expect(publicInfo.s32()).toBe(0);
    expect(publicInfo.s32()).toBe(0);
    expect(publicInfo.raw(48)).toEqual(new Uint8Array(48));
    expect(publicInfo.u8()).toBe(0);
    expect(publicInfo.u8()).toBe(0);
    expect(publicInfo.u8()).toBe(1);
    expect(Array.from({ length: 12 }, () => publicInfo.u16())).toEqual([
      1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
    ]);
    expect(publicInfo.remaining).toBe(0);
    store.close();
  });

  test("empty projections use the exact terminal shapes", () => {
    const items = decode(build("GL_MYITEM_ACK").encode());
    expect(items.u8()).toBe(1);
    expect(items.s32()).toBe(0);
    expect(items.s32()).toBe(-1);
    expect(items.remaining).toBe(0);

    const users = decode(build("GL_USERLIST_ACK").encode());
    expect(users.u16()).toBe(0);
    expect(users.remaining).toBe(0);

    const rooms = decode(build("GL_GAMEROOMINFO_ACK").encode());
    expect(rooms.u8()).toBe(0);
    expect(rooms.u8()).toBe(0);
    expect(rooms.remaining).toBe(0);

    expect(decode(build("GL_SHOPIN_ACK").encode()).remaining).toBe(0);

    const friends = decode(build("GL_FRIEND_LIST_ACK", "alice").encode());
    expect(friends.u16()).toBe(0);
    expect(friends.str()).toBe("alice");
    expect(friends.u8()).toBe(0);
    expect(friends.remaining).toBe(0);

    const messages = decode(build("GL_MSG_RECVLIST_ACK", "alice").encode());
    expect(messages.u16()).toBe(0);
    expect(messages.str()).toBe("alice");
    expect(messages.u8()).toBe(0);
    expect(messages.remaining).toBe(0);
  });
});
