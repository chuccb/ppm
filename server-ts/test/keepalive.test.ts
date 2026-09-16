import { describe, expect, test } from "bun:test";
import { decode } from "../src/packet.ts";
import { opcodeFor } from "../src/opcodes.ts";
import { Registry } from "../src/ops/registry.ts";

const ops = Registry.load();
const GT_PING_ACK = () => ops.build("GT_PING_ACK");

describe("keepalive", () => {
  test("102 is the server-initiated heartbeat, with an empty payload", () => {
    const r = decode(GT_PING_ACK().encode());
    expect(r.opcode).toBe(opcodeFor("GT_PING_ACK"));
    expect(r.remaining).toBe(0);
  });

  test("an empty heartbeat is still a full encrypted block on the wire", () => {
    // sub_592FB0 pads even an empty payload to one 16-byte block.
    expect(GT_PING_ACK().encode().length).toBe(8 + 16);
  });

  test("the direction is 102 out, 101 back", () => {
    // Guards against "fixing" the apparent REQ/ACK inversion: the client's
    // dispatcher handles 102 by building 101, and has no 102 builder at all.
    expect(opcodeFor("GT_PING_ACK")).toBe(102);
    expect(opcodeFor("GT_PING_REQ")).toBe(101);
    expect(GT_PING_ACK().opcode).toBe(102);
  });
});
