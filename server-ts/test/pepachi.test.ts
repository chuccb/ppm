import { describe, expect, test } from "bun:test";
import { Packet, decode } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import { build as buildPacket, handlerFor, type OutboundArgs, type OutboundName } from "../src/ops/registry.ts";
import enterPepachi from "../src/ops/c2s/GP_ENTER_PEPACHI_REQ.ts";
import startGame from "../src/ops/c2s/GP_START_GAME_REQ.ts";
import pepachiList from "../src/ops/c2s/GP_PEPACHI_LIST_REQ.ts";
import capsuleStart from "../src/ops/c2s/GS_CAPSULEMACHINE_START_REQ.ts";

const build = <N extends OutboundName>(name: N, ...args: OutboundArgs<N>) =>
  decode(buildPacket(name, ...args).encode());

const reread = (packet: Packet) => decode(packet.encode());

/** Minimal Connection stub that only records reply names. */
function stubConnection() {
  const replies: string[] = [];
  const connection = { reply: (name: string) => replies.push(name) } as unknown as Parameters<
    typeof enterPepachi
  >[1];
  return { connection, replies };
}

describe("698 -> 699 Pepachi entry, consumer-safe arm", () => {
  test("empty request replies with a non-granting three-word ACK", () => {
    const { connection, replies } = stubConnection();
    enterPepachi(reread(new Packet(opcodeFor("GP_ENTER_PEPACHI_REQ"))), connection);
    expect(replies).toEqual(["GP_ENTER_PEPACHI_ACK"]);

    const ack = build("GP_ENTER_PEPACHI_ACK");
    expect(ack.remaining).toBe(9);
    expect(ack.u8()).toBe(0); // status: only 1 would open the reel UI
    expect(ack.s32()).toBe(0);
    expect(ack.s32()).toBe(0);
    expect(ack.remaining).toBe(0);
  });

  test("rejects a payload the native client never sends", () => {
    const { connection } = stubConnection();
    const request = reread(new Packet(opcodeFor("GP_ENTER_PEPACHI_REQ")).u8(1));
    expect(() => enterPepachi(request, connection)).toThrow(/trailing bytes in 698/);
  });
});

describe("700 -> 701 Pepachi spin, consumer-safe arm", () => {
  test("consumes exactly {u8 machine, s32 coinType} and never opens award decoding", () => {
    const { connection, replies } = stubConnection();
    const request = reread(new Packet(opcodeFor("GP_START_GAME_REQ")).u8(0).s32(1));
    startGame(request, connection);
    expect(replies).toEqual(["GP_START_GAME_ACK"]);

    const ack = build("GP_START_GAME_ACK");
    expect(ack.remaining).toBe(2);
    expect(ack.u8()).toBe(0); // sub_84A490 only decodes awards when byte 0 is 1
    expect(ack.u8()).toBe(0); // rawError
    expect(ack.remaining).toBe(0);
  });

  test("rejects short or padded requests", () => {
    const { connection } = stubConnection();
    const short = reread(new Packet(opcodeFor("GP_START_GAME_REQ")).u8(0));
    expect(() => startGame(short, connection)).toThrow(RangeError);
    const long = reread(new Packet(opcodeFor("GP_START_GAME_REQ")).u8(0).s32(1).u8(0));
    expect(() => startGame(long, connection)).toThrow(/trailing bytes in 700/);
  });
});

describe("702 -> 703 Pepachi catalog, consumer-safe arm", () => {
  test("replies with two zero counts and no item records", () => {
    const { connection, replies } = stubConnection();
    pepachiList(reread(new Packet(opcodeFor("GP_PEPACHI_LIST_REQ"))), connection);
    expect(replies).toEqual(["GP_PEPACHI_LIST_ACK"]);

    const ack = build("GP_PEPACHI_LIST_ACK");
    expect(ack.remaining).toBe(8);
    expect(ack.s32()).toBe(0); // normalCount — negative would trip the DB diagnostic
    expect(ack.s32()).toBe(0); // rareCount
    expect(ack.remaining).toBe(0);
  });

  test("rejects trailing bytes", () => {
    const { connection } = stubConnection();
    const request = reread(new Packet(opcodeFor("GP_PEPACHI_LIST_REQ")).s32(0));
    expect(() => pepachiList(request, connection)).toThrow(/trailing bytes in 702/);
  });
});

describe("900 -> 901 capsule start, consumer-safe arm", () => {
  test("tolerates any request length and answers the denial arm", () => {
    const { connection, replies } = stubConnection();
    capsuleStart(reread(new Packet(opcodeFor("GS_CAPSULEMACHINE_START_REQ"))), connection);
    capsuleStart(
      reread(new Packet(opcodeFor("GS_CAPSULEMACHINE_START_REQ")).u8(2).s32(0)),
      connection,
    );
    expect(replies).toEqual([
      "GS_CAPSULEMACHINE_START_ACK",
      "GS_CAPSULEMACHINE_START_ACK",
    ]);

    const ack = build("GS_CAPSULEMACHINE_START_ACK");
    expect(ack.remaining).toBe(17);
    expect(ack.u8()).toBe(1); // nonzero status: no wallet/reward mutation in sub_9A1A30
    expect(ack.s32()).toBe(0); // count 0: no award records
    expect(ack.s32()).toBe(0);
    expect(ack.s32()).toBe(0);
    expect(ack.s32()).toBe(0);
    expect(ack.remaining).toBe(0);
  });
});

describe("registry wiring", () => {
  test("all four flows resolve through the opcode catalogue and handler table", () => {
    for (const name of [
      "GP_ENTER_PEPACHI_REQ",
      "GP_START_GAME_REQ",
      "GP_PEPACHI_LIST_REQ",
      "GS_CAPSULEMACHINE_START_REQ",
    ]) {
      expect(handlerFor(opcodeFor(name))).toBeDefined();
    }
    expect(opcodeFor("GP_ENTER_PEPACHI_REQ")).toBe(698);
    expect(opcodeFor("GP_START_GAME_REQ")).toBe(700);
    expect(opcodeFor("GP_PEPACHI_LIST_REQ")).toBe(702);
    expect(opcodeFor("GS_CAPSULEMACHINE_START_REQ")).toBe(900);
  });
});
