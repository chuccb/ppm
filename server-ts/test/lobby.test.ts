import { describe, expect, test } from "bun:test";
import { decode } from "../src/packet.ts";
import { build } from "../src/ops/registry.ts";
import { Store } from "../src/store.ts";

describe("lobby bootstrap packets", () => {
  test("creates one canonical starter and reuses it", async () => {
    const store = new Store();
    const account = await store.createAccount("alice", "pw");

    const first = store.ensurePlayer(account.id);
    const second = store.ensurePlayer(account.id);
    expect(first).not.toBeNull();
    expect(second).toEqual(first);
    expect(first?.nickname).toBe("alice");
    expect(first?.currentCharacter).toBe(0);
    expect(first?.characters).toEqual([
      { slot: 0, type: 1, appearance: [1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0] },
    ]);
    store.close();
  });

  test("198 writes a successful minimal but complete CClientData", async () => {
    const store = new Store();
    const account = await store.createAccount("bob", "pw");
    const player = store.ensurePlayer(account.id);
    expect(player).not.toBeNull();

    const reader = decode(build("GL_MYINFO_ACK", player).encode());
    expect(reader.u8()).toBe(1);
    expect(reader.s32()).toBe(player!.id);
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
    for (let i = 0; i < 7; i++) expect(reader.s32()).toBe(0);
    expect(reader.u16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.u8()).toBe(0);
    expect(reader.remaining).toBe(0);
    const publicPlayer = store.getPlayerByNickname("bob");
    expect(publicPlayer).toEqual(player);
    const publicInfo = decode(build("GL_CLIENTINFO_ACK", publicPlayer).encode());
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
