import { describe, expect, test } from "bun:test";
import { Database } from "bun:sqlite";
import { mkdtempSync, rmSync } from "node:fs";
import { join } from "node:path";
import { tmpdir } from "node:os";
import { decode } from "../src/packet.ts";
import { build } from "../src/ops/registry.ts";
import { Store } from "../src/store.ts";

describe("lobby bootstrap packets", () => {
  test("creates one canonical starter and reuses it", async () => {
    const store = new Store();
    await expect(store.createAccount("a".repeat(24), "pw")).rejects.toThrow(/client-representable/);
    const account = await store.createAccount("alice", "pw");

    const first = store.ensurePlayerIdentity(account.id);
    const second = store.ensurePlayerIdentity(account.id);
    expect(first).not.toBeNull();
    expect(second).toEqual(first);
    expect(first?.nickname).toBe("alice");
    expect(first?.selectedCharIndex).toBe(0);
    expect(first?.characters).toEqual([
      { slotNo: 0, charType: 1, appearance: [1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0] },
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
    expect(() =>
      build("GL_INVENIN_ACK", first!.userId, 7, { ...snapshot, selectedProfile: Number.NaN }),
    ).toThrow(/u8 expects/);
    store.close();
  });

  test("repairs only proven normal appearance defaults", async () => {
    const directory = mkdtempSync(join(tmpdir(), "paperman-appearance-"));
    const path = join(directory, "state.sqlite");
    const store = new Store(path);
    const account = await store.createAccount("repair", "pw");
    const initial = store.ensurePlayerIdentity(account.id);
    expect(initial).not.toBeNull();

    const db = new Database(path);
    db.query(
      `UPDATE player_character
          SET character_type = 2,
              appearance0 = 2, appearance1 = 77, appearance2 = 0,
              appearance3 = 0, appearance4 = 0, appearance5 = 0,
              appearance6 = 0, appearance7 = 88
        WHERE player_id = $p AND slot = 0`,
    ).run({ "$p": initial!.userId });
    const repaired = store.getMyInfo(initial!.userId);
    expect(repaired?.characters[0]?.appearance).toEqual([
      2, 77, 10, 22, 12, 12, 0, 88, 0, 0, 0, 0,
    ]);

    const persisted = db
      .query<{ appearance0: number; appearance1: number; appearance2: number; appearance3: number; appearance4: number; appearance5: number }, { "$p": number }>(
        `SELECT appearance0, appearance1, appearance2, appearance3, appearance4, appearance5
           FROM player_character WHERE player_id = $p AND slot = 0`,
      )
      .get({ "$p": initial!.userId });
    expect(persisted).toEqual({ appearance0: 2, appearance1: 77, appearance2: 10, appearance3: 22, appearance4: 12, appearance5: 12 });

    db.query(
      `UPDATE player_character SET appearance0 = 99, appearance1 = 0, appearance2 = 0,
              appearance3 = 0, appearance4 = 0, appearance5 = 0
        WHERE player_id = $p AND slot = 0`,
    ).run({ "$p": initial!.userId });
    expect(store.getMyInfo(initial!.userId)?.characters[0]?.appearance).toEqual([
      99, 0, 0, 0, 0, 0, 0, 88, 0, 0, 0, 0,
    ]);

    db.close();
    store.close();
    rmSync(directory, { recursive: true, force: true });
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
          ? [11012201, 11022201, 11031101, 11041101, 11051101, 11060001, 11060002]
          : [0, 0, 0, 0, 0, 0, 0],
        expiresAtPackedMinute: profile === 1 ? 0x12345678 : 0,
      })),
    };
    const wireInfo = {
      ...myInfo!,
      stats: {
        ...myInfo!.stats,
        wins: 101,
        losses: 102,
        kills: 103,
        deaths: 104,
        headshots: 106,
        combos: 107,
        hearts: 108,
        doubleKill: 109,
        tripleKill: 110,
        multiKill: 111,
        ultraKill: 112,
        zKill: 113,
        kKill: 114,
        ddKill: 115,
        criticals: 116,
        playTimeSeconds: 119,
      },
    };
    const reader = decode(build("GL_MYINFO_ACK", wireInfo, selectedSnapshot).encode());
    expect(reader.u8()).toBe(1);
    expect(reader.s32()).toBe(wireInfo.userId);
    expect(reader.str()).toBe("bob");
    expect(reader.u8()).toBe(0); // selected character-list index
    expect(reader.s32()).toBe(1); // level
    expect(reader.s32()).toBe(0); // exp
    expect(reader.s32()).toBe(0); // native +108 raw/unknown task-condition input
    expect(Array.from({ length: 18 }, () => reader.s32())).toEqual([
      0, 0, 0, // native [34..36] reserved words
      101, 102, // wins, losses
      103, 104, // kills, deaths
      106, 107, 108, // headshots, combos, hearts (+164/+168/+172)
      109, 110, 116, // doubleKill, tripleKill, criticals (wire slots +180/+184/+176)
      111, 112, 113, 114, 115, // multi..dd (+188..+204)
    ]);
    expect(reader.raw(3)).toEqual(new Uint8Array(3));
    expect(reader.s32()).toBe(0); // cash
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.s32()).toBe(119); // blob [52], cumulative play seconds
    expect(reader.raw(44)).toEqual(new Uint8Array(44));
    expect(reader.u8()).toBe(0); // selected character-list index
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
      11012201,
      11022201,
      11031101,
      11041101,
      11051101,
      11060001,
      11060002,
    ]);
    expect(reader.u16()).toBe(0);
    expect(reader.s32()).toBe(0);
    expect(reader.u8()).toBe(0);
    expect(reader.remaining).toBe(0);
    expect(() =>
      build("GL_MYINFO_ACK", { ...wireInfo, nickname: "n".repeat(24) }, selectedSnapshot),
    ).toThrow(/native char\[24\]/);
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

    const sparseCharacters = {
      ...myInfo!,
      selectedCharIndex: 1,
      characters: [
        { slotNo: 7, charType: 1, appearance: [] },
        { slotNo: 9, charType: 2, appearance: [] },
      ],
    };
    const sparsePublicInfo = decode(build("GL_CLIENTINFO_ACK", sparseCharacters).encode());
    sparsePublicInfo.u8(); // success
    sparsePublicInfo.str();
    expect(sparsePublicInfo.u8()).toBe(1); // basic selected wire index
    for (let i = 0; i < 21; i++) sparsePublicInfo.s32();
    sparsePublicInfo.raw(3);
    for (let i = 0; i < 3; i++) sparsePublicInfo.s32();
    sparsePublicInfo.raw(48);
    expect(sparsePublicInfo.u8()).toBe(1); // basic slot_current
    expect(sparsePublicInfo.u8()).toBe(1); // 247 character-list index, not slotNo 9
    expect(sparsePublicInfo.u8()).toBe(2);
    expect(sparsePublicInfo.remaining).toBe(24); // 12 u16 appearance values
    const invalidSelected = decode(
      build("GL_CLIENTINFO_ACK", { ...sparseCharacters, selectedCharIndex: 20 }).encode(),
    );
    expect(invalidSelected.u8()).toBe(0);
    expect(invalidSelected.remaining).toBe(0);
    expect(() =>
      build("GL_CLIENTINFO_ACK", {
        ...sparseCharacters,
        characters: sparseCharacters.characters.map((character, index) =>
          index === 1 ? { ...character, charType: 0x100 } : character,
        ),
      }),
    ).toThrow(/247 char_type expected/);
    store.close();
  });

  test("empty projections use the exact terminal shapes", () => {
    const items = decode(build("GL_MYITEM_ACK").encode());
    expect(items.u8()).toBe(1);
    expect(items.s32()).toBe(0);
    expect(items.s32()).toBe(-1);
    expect(items.remaining).toBe(0);
    expect(() =>
      build("GL_MYITEM_ACK", [{ slot: 0, itemId: 1, f1: Number.MAX_VALUE, f2: 0, period: 0, durability: 0 }]),
    ).toThrow(/raw4 f32 projection out of range/);

    const item = decode(build("GL_MYITEM_ACK", [{
      slot: 12,
      itemId: 12100027,
      f1: 1.25,
      f2: -0.5,
      period: -7,
      extra: 9,
      durability: 0x1234,
    }]).encode());
    expect(item.u8()).toBe(1);
    expect(item.s32()).toBe(0);
    expect(item.s32()).toBe(12);
    expect(item.s32()).toBe(12100027);
    expect(item.f32()).toBeCloseTo(1.25);
    expect(item.f32()).toBeCloseTo(-0.5);
    expect(item.s32()).toBe(-7);
    expect(item.u8()).toBe(9);
    expect(item.u16()).toBe(0x1234);
    expect(item.s32()).toBe(-1);
    expect(item.remaining).toBe(0);

    // The shipped itemdata.pat catalog is not a server grant authority yet;
    // the wire boundary remains the native non-negative s32 domain.
    const uncatalogued = decode(build("GL_MYITEM_ACK", [{
      slot: 0,
      itemId: 0x7fff_ffff,
      f1: 0,
      f2: 0,
      period: 0,
      durability: 0,
    }]).encode());
    uncatalogued.u8();
    uncatalogued.s32(); // start index
    uncatalogued.s32(); // inventory slot
    expect(uncatalogued.s32()).toBe(0x7fff_ffff);

    const users = decode(build("GL_USERLIST_ACK").encode());
    expect(users.u16()).toBe(0);
    expect(users.remaining).toBe(0);

    const rooms = decode(build("GL_GAMEROOMINFO_ACK").encode());
    expect(rooms.u8()).toBe(0);
    expect(rooms.u8()).toBe(0);
    expect(rooms.remaining).toBe(0);

    expect(decode(build("GL_SHOPIN_ACK").encode()).remaining).toBe(0);
    expect(decode(build("GL_DATA_RECV_COMPLETED_ACK").encode()).remaining).toBe(0);

    const friends = decode(build("GL_FRIEND_LIST_ACK", "alice").encode());
    expect(friends.u16()).toBe(0);
    expect(friends.str()).toBe("alice");
    expect(friends.u8()).toBe(0);
    expect(friends.remaining).toBe(0);
    expect(() => build("GL_FRIEND_LIST_ACK", "a".repeat(21))).toThrow(/char\[21\]/);

    const messages = decode(build("GL_MSG_RECVLIST_ACK", "alice").encode());
    expect(messages.u16()).toBe(0);
    expect(messages.str()).toBe("alice");
    expect(messages.u8()).toBe(0);
    expect(messages.remaining).toBe(0);
    expect(() => build("GL_MSG_RECVLIST_ACK", "a".repeat(21))).toThrow(/char\[21\]/);
  });
});
