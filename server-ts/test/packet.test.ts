import { describe, expect, test } from "bun:test";
import { Packet, PacketStream, Reader, decode } from "../src/packet.ts";

describe("frame header", () => {
  test("words follow docs/PACKETS.md §1.2", () => {
    const frame = new Packet(681).s32(1).encode();
    const view = new DataView(frame.buffer);

    expect(view.getUint16(0, true)).toBe(16); // size, padded to one block
    expect(view.getUint16(2, true)).toBe(681); // opcode
    expect(view.getUint16(4, true)).toBe(4); // size before AES
    expect(view.getUint16(6, true)).toBe(4); // size before LZ
    expect(frame.length).toBe(8 + 16);
  });

  test("an empty payload still costs one encrypted block", () => {
    const frame = new Packet(700).encode();
    expect(new DataView(frame.buffer).getUint16(0, true)).toBe(16);
    expect(frame.length).toBe(24);
  });
});

describe("round trip", () => {
  test("every primitive survives", () => {
    const r = decode(
      new Packet(682)
        .str("alice")
        .str("token123")
        .u64(0x1122334455667788n)
        .u8(2)
        .s32(-7)
        .f32(0.5)
        .u16(65535)
        .wstr("紙片人")
        .zeros(4)
        .encode(),
    );

    expect(r.opcode).toBe(682);
    expect(r.str()).toBe("alice");
    expect(r.str()).toBe("token123");
    expect(r.u64()).toBe(0x1122334455667788n);
    expect(r.u8()).toBe(2);
    expect(r.s32()).toBe(-7);
    expect(r.f32()).toBe(0.5);
    expect(r.u16()).toBe(65535);
    expect(r.wstr()).toBe("紙片人");
    expect(r.raw(4)).toEqual(new Uint8Array(4));
    expect(r.remaining).toBe(0);
  });

  test("the body is actually encrypted", () => {
    const frame = new Packet(100).raw(new Uint8Array(16).fill(0xab)).encode();
    expect(frame.subarray(8)).not.toEqual(new Uint8Array(16).fill(0xab));
  });

  test("an embedded packet carries opcode and size", () => {
    const inner = new Packet(205).s32(42);
    const r = decode(new Packet(204).packet(inner).encode());
    expect(r.u16()).toBe(205);
    expect(r.u32()).toBe(4);
    expect(r.s32()).toBe(42);
  });

  test("every writer survives the buffer growing under it", () => {
    // The buffer reallocates as it fills, and a writer that resolves its
    // DataView before reserving space writes into the copy that gets thrown
    // away. The write under test has to be the one that crosses the boundary,
    // so pad to 63 (any multi-byte write straddles it) and to 64 (even a
    // single byte grows).
    const writers: [string, (p: Packet) => void, (r: Reader) => unknown, unknown][] = [
      ["s8", (p) => p.s8(-1), (r) => r.s8(), -1],
      ["u16", (p) => p.u16(0xbeef), (r) => r.u16(), 0xbeef],
      ["s16", (p) => p.s16(-2), (r) => r.s16(), -2],
      ["u32", (p) => p.u32(0xdeadbeef), (r) => r.u32(), 0xdeadbeef],
      ["s32", (p) => p.s32(-3), (r) => r.s32(), -3],
      ["u64", (p) => p.u64(0x1122334455667788n), (r) => r.u64(), 0x1122334455667788n],
      ["f32", (p) => p.f32(1.5), (r) => r.f32(), 1.5],
      ["str", (p) => p.str("tail"), (r) => r.str(), "tail"],
      ["wstr", (p) => p.wstr("\u5c3e"), (r) => r.wstr(), "\u5c3e"],
      ["raw", (p) => p.raw(new Uint8Array([9])), (r) => r.raw(1)[0], 9],
    ];

    for (const [name, write, read, expected] of writers) {
      for (const pad of [63, 64]) {
        const packet = new Packet(1);
        packet.raw(new Uint8Array(pad));
        write(packet);

        const r = decode(packet.encode());
        r.raw(pad);
        expect(read(r), `${name} lost its value growing at ${pad}`).toEqual(expected);
      }
    }
  });

  test("ANSI strings reject non-ASCII rather than mangling it", () => {
    expect(() => new Packet(1).str("紙")).toThrow(/use wstr/);
  });

  test("UTF-16 strings require a complete terminator", () => {
    expect(() => new Reader(1, new Uint8Array([0x41, 0, 0])).wstr()).toThrow(/unterminated/);
    expect(() => new Reader(1, new Uint8Array([0x41, 0])).wstr()).toThrow(/unterminated/);
  });
});

describe("stream reassembly", () => {
  const frame = (opcode: number, value: number) => new Packet(opcode).s32(value).encode();
  const drain = (stream: PacketStream) => [...stream.drain()].map((r) => [r.opcode, r.s32()]);

  test("several packets in one chunk all arrive", () => {
    const a = frame(1, 11);
    const b = frame(2, 22);
    const merged = new Uint8Array(a.length + b.length);
    merged.set(a);
    merged.set(b, a.length);

    const stream = new PacketStream();
    stream.push(merged);
    expect(drain(stream)).toEqual([
      [1, 11],
      [2, 22],
    ]);
  });

  test("a packet split across chunks is reassembled", () => {
    const whole = frame(3, 33);
    const stream = new PacketStream();

    stream.push(whole.subarray(0, 5)); // partial header
    expect(drain(stream)).toEqual([]);
    stream.push(whole.subarray(5));
    expect(drain(stream)).toEqual([[3, 33]]);
  });
});

describe("scalar domains", () => {
  test("every scalar primitive rejects values outside its C width", () => {
    expect(() => new Packet(1).u8(0x100)).toThrow(/u8 expects/);
    expect(() => new Packet(1).u8(-1)).toThrow(/u8 expects/);
    expect(() => new Packet(1).s8(0x80)).toThrow(/s8 expects/);
    expect(() => new Packet(1).u16(0x1_0000)).toThrow(/u16 expects/);
    expect(() => new Packet(1).s16(0x8000)).toThrow(/s16 expects/);
    expect(() => new Packet(1).s16(-0x8001)).toThrow(/s16 expects/);
    expect(() => new Packet(1).u32(-1)).toThrow(/u32 expects/);
    expect(() => new Packet(1).s32(0x8000_0000)).toThrow(/s32 expects/);
    expect(() => new Packet(1).s32(1.5)).toThrow(/s32 expects/);
    expect(() => new Packet(1).u64(-1n)).toThrow(/u64 expects/);
    expect(() => new Packet(1).u64(1n << 64n)).toThrow(/u64 expects/);
    expect(() => new Packet(1).f32(Number.NaN)).toThrow(/f32 expects/);
    expect(() => new Packet(1).f32(Number.MAX_VALUE)).toThrow(/f32 expects/);
  });


});

describe("validation", () => {
  const corrupt = (mutate: (view: DataView) => void): Uint8Array => {
    const frame = new Packet(200).s32(1).encode();
    mutate(new DataView(frame.buffer));
    return frame;
  };

  test("rejects a size that is not block-aligned", () => {
    expect(() => decode(corrupt((v) => v.setUint16(0, 20, true)))).toThrow(/sub_593110/);
  });

  test("rejects a size that is not align16(sizeBeforeAes)", () => {
    expect(() => decode(corrupt((v) => v.setUint16(4, 99, true)))).toThrow(/align16/);
  });

  test("rejects a frame claiming LZ compression", () => {
    expect(() => decode(corrupt((v) => v.setUint16(6, 0x2580, true)))).toThrow(/compression/);
  });

  test("a payload at the buffer limit is refused by the cipher stage", () => {
    const packet = new Packet(1).raw(new Uint8Array(9592));
    expect(packet.length).toBe(9592);
    expect(() => packet.encode()).toThrow(/encryptable maximum/);
  });

  test("writing past the payload limit fails clearly", () => {
    const packet = new Packet(1).raw(new Uint8Array(9592));
    expect(() => packet.u8(0)).toThrow(/exceed 9592/);
  });

  test("reading past the end throws", () => {
    const r = decode(new Packet(1).u8(1).encode());
    r.u8();
    expect(() => r.u32()).toThrow(RangeError);
  });

  test("fixed-count primitives reject negative lengths", () => {
    expect(() => new Packet(1).zeros(-1)).toThrow(RangeError);
    expect(() => new Reader(1, new Uint8Array()).raw(-1)).toThrow(RangeError);
    expect(() => new Reader(1, new Uint8Array([0])).str(-1)).toThrow(RangeError);
  });
});
