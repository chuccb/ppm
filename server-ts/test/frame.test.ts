import { describe, expect, test } from "bun:test";
import { PacketWriter } from "../src/codec/packet.ts";
import { FrameStream, decodeFrame, encodeFrame } from "../src/codec/frame.ts";

describe("frame header", () => {
  test("header words follow docs/PACKETS.md §1.2", () => {
    const packet = new PacketWriter(681);
    packet.s32(1); // login result
    const frame = encodeFrame(packet);
    const view = new DataView(frame.buffer);

    expect(view.getUint16(0, true)).toBe(16); // word0: aligned to one block
    expect(view.getUint16(2, true)).toBe(681); // word1: opcode
    expect(view.getUint16(4, true)).toBe(4); // word2: pre-encrypt size
    expect(view.getUint16(6, true)).toBe(4); // word3: pre-compress size
    expect(frame.length).toBe(8 + 16);
  });

  test("an empty payload still occupies one encrypted block", () => {
    const frame = encodeFrame(new PacketWriter(700));
    expect(new DataView(frame.buffer).getUint16(0, true)).toBe(16);
    expect(frame.length).toBe(24);
  });
});

describe("round trip", () => {
  test("every primitive survives encode/decode", () => {
    const packet = new PacketWriter(682);
    packet
      .str("alice")
      .str("token123")
      .u64(0x1122334455667788n)
      .u8(2)
      .s32(-7)
      .f32(0.5)
      .u16(65535)
      .wstr("紙片人")
      .zeros(4);

    const decoded = decodeFrame(encodeFrame(packet));
    expect(decoded).not.toBeNull();
    const reader = decoded!.reader;

    expect(reader.opcode).toBe(682);
    expect(reader.str()).toBe("alice");
    expect(reader.str()).toBe("token123");
    expect(reader.u64()).toBe(0x1122334455667788n);
    expect(reader.u8()).toBe(2);
    expect(reader.s32()).toBe(-7);
    expect(reader.f32()).toBe(0.5);
    expect(reader.u16()).toBe(65535);
    expect(reader.wstr()).toBe("紙片人");
    expect(reader.raw(4)).toEqual(new Uint8Array(4));
    expect(reader.remaining).toBe(0);
  });

  test("ciphertext is not the plaintext", () => {
    const packet = new PacketWriter(100);
    packet.raw(new Uint8Array(16).fill(0xab));
    const frame = encodeFrame(packet);
    expect(frame.subarray(8)).not.toEqual(new Uint8Array(16).fill(0xab));
  });
});

describe("stream reassembly", () => {
  const build = (opcode: number, value: number): Uint8Array =>
    encodeFrame(new PacketWriter(opcode).s32(value));

  test("a split frame waits for the remainder", () => {
    const frame = build(141, 9);
    expect(decodeFrame(frame.subarray(0, 4))).toBeNull(); // partial header
    expect(decodeFrame(frame.subarray(0, frame.length - 1))).toBeNull(); // partial body
    expect(decodeFrame(frame)).not.toBeNull();
  });

  test("several frames in one chunk all drain", () => {
    const stream = new FrameStream();
    const a = build(1, 11);
    const b = build(2, 22);
    const merged = new Uint8Array(a.length + b.length);
    merged.set(a);
    merged.set(b, a.length);

    stream.push(merged);
    const seen = [...stream.drain()].map((r) => [r.opcode, r.s32()]);
    expect(seen).toEqual([
      [1, 11],
      [2, 22],
    ]);
  });

  test("a frame split across chunks is reassembled", () => {
    const stream = new FrameStream();
    const frame = build(3, 33);
    stream.push(frame.subarray(0, 5));
    expect([...stream.drain()]).toHaveLength(0);
    stream.push(frame.subarray(5));

    const readers = [...stream.drain()];
    expect(readers).toHaveLength(1);
    expect(readers[0]!.opcode).toBe(3);
    expect(readers[0]!.s32()).toBe(33);
  });
});

describe("validation", () => {
  const corrupt = (mutate: (view: DataView) => void): Uint8Array => {
    const frame = encodeFrame(new PacketWriter(200).s32(1));
    mutate(new DataView(frame.buffer));
    return frame;
  };

  test("rejects a non-block-aligned size", () => {
    expect(() => decodeFrame(corrupt((v) => v.setUint16(0, 20, true)))).toThrow(RangeError);
  });

  test("rejects word0 that is not align16(word2)", () => {
    expect(() => decodeFrame(corrupt((v) => v.setUint16(4, 99, true)))).toThrow(RangeError);
  });

  test("rejects a frame claiming LZ compression", () => {
    // word3 >= 0x2580 with word2 < word3 is the compressed case.
    const frame = encodeFrame(new PacketWriter(200).s32(1));
    const view = new DataView(frame.buffer);
    view.setUint16(6, 0x2580, true);
    expect(() => decodeFrame(frame)).toThrow(/compression/);
  });

  test("a payload at the buffer limit is still refused by the cipher stage", () => {
    // MAX_PAYLOAD (9592) is buildable but >= MAX_ENCRYPTED, so sub_592FB0
    // would refuse it; encodeFrame must refuse it too.
    const packet = new PacketWriter(1);
    packet.raw(new Uint8Array(9592));
    expect(packet.length).toBe(9592);
    expect(() => encodeFrame(packet)).toThrow(/encryptable maximum/);
  });

  test("writing past the payload limit is rejected with a clear error", () => {
    const packet = new PacketWriter(1);
    packet.raw(new Uint8Array(9592));
    expect(() => packet.u8(0)).toThrow(/exceed 9592/);
  });
});
