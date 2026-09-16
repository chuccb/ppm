/**
 * The wire format, end to end: header, cipher, reader, writer, reassembly.
 *
 * These live together because you never touch one without the others. The
 * layout is docs/PACKETS.md §1.2 — little-endian, unaligned, no padding:
 *
 *   u16 size            on-wire payload size (word0)
 *   u16 opcode          what the client's dispatcher switches on (word1)
 *   u16 sizeBeforeAes   plaintext length (word2, written by the AES stage)
 *   u16 sizeBeforeLz    pre-compression length (word3, set on first send)
 *   ...  payload
 *
 * Strings are NUL-terminated inline with no length prefix (`sub_5926F0`).
 */

import { PACKET_ROUND_KEYS, cfbDecrypt, cfbEncrypt } from "./aes.ts";

export const HEADER_SIZE = 8;
/** `sub_591DA0`: the 9600-byte buffer, less the header. */
export const MAX_PAYLOAD = 9592;
/** `sub_592FB0` refuses to encrypt at or beyond this aligned size. */
export const MAX_ENCRYPTED = 0x2578;
/** The client only lowers its threshold below this, so it disables LZ. */
export const COMPRESSION_DISABLED = 0x2580;

const BLOCK = 16;

/**
 * Legacy code pages the client uses. The Korean build is CP949, whose WHATWG
 * label is "euc-kr" — Bun rejects "cp949" outright.
 */
export type Encoding = "euc-kr" | "shift_jis" | "utf-8";

const decoders = new Map<Encoding, TextDecoder>();
function decoder(encoding: Encoding): TextDecoder {
  let cached = decoders.get(encoding);
  if (!cached) decoders.set(encoding, (cached = new TextDecoder(encoding)));
  return cached;
}

function align16(size: number): number {
  return size === 0 ? BLOCK : Math.ceil(size / BLOCK) * BLOCK;
}

// ---------------------------------------------------------------- writing

/** Builds a payload. Chainable: `new Packet(op).s32(1).str("x")`. */
export class Packet {
  readonly opcode: number;
  /** Grows on demand; most packets are far smaller than one block. */
  #buf = new Uint8Array(64);
  #len = 0;

  constructor(opcode: number) {
    this.opcode = opcode & 0xffff;
  }

  get length(): number {
    return this.#len;
  }

  /**
   * Reserve `extra` bytes and return the offset to write at.
   *
   * Callers must resolve this *before* touching `#buf` or `#view()`: it may
   * reallocate, and `this.#view().setX(this.#at(n), ...)` would evaluate the
   * view against the old buffer and write into the copy that gets discarded.
   */
  #at(extra: number): number {
    const at = this.#len;
    const needed = at + extra;
    if (needed > MAX_PAYLOAD) throw new RangeError(`payload would exceed ${MAX_PAYLOAD} bytes`);
    if (needed > this.#buf.length) {
      let size = Math.max(this.#buf.length * 2, 16);
      while (size < needed) size *= 2;
      // Clamping must never yield a buffer smaller than this write needs.
      const grown = new Uint8Array(Math.max(Math.min(size, MAX_PAYLOAD), needed));
      grown.set(this.#buf.subarray(0, at));
      this.#buf = grown;
    }
    this.#len = needed;
    return at;
  }

  #view(): DataView {
    return new DataView(this.#buf.buffer, this.#buf.byteOffset, this.#buf.byteLength);
  }

  u8(v: number): this {
    // #at may reallocate, so resolve the offset before touching #buf.
    const at = this.#at(1);
    this.#buf[at] = v & 0xff;
    return this;
  }
  s8(v: number): this {
    const at = this.#at(1);
    this.#view().setInt8(at, v);
    return this;
  }
  u16(v: number): this {
    const at = this.#at(2);
    this.#view().setUint16(at, v & 0xffff, true);
    return this;
  }
  s16(v: number): this {
    const at = this.#at(2);
    this.#view().setInt16(at, v, true);
    return this;
  }
  u32(v: number): this {
    const at = this.#at(4);
    this.#view().setUint32(at, v >>> 0, true);
    return this;
  }
  s32(v: number): this {
    const at = this.#at(4);
    this.#view().setInt32(at, v | 0, true);
    return this;
  }
  u64(v: bigint): this {
    const at = this.#at(8);
    this.#view().setBigUint64(at, v, true);
    return this;
  }
  f32(v: number): this {
    const at = this.#at(4);
    this.#view().setFloat32(at, v, true);
    return this;
  }

  /** ANSI bytes + NUL (`sub_5926F0`). ASCII only; use `wstr` for the rest. */
  str(text: string): this {
    for (let i = 0; i < text.length; i++) {
      if (text.charCodeAt(i) > 0x7f) {
        throw new RangeError(`non-ASCII in ANSI string ${JSON.stringify(text)}; use wstr`);
      }
    }
    const at = this.#at(text.length + 1);
    for (let i = 0; i < text.length; i++) this.#buf[at + i] = text.charCodeAt(i);
    this.#buf[at + text.length] = 0;
    return this;
  }

  /** UTF-16LE + 16-bit NUL (`sub_592770`). */
  wstr(text: string): this {
    const at = this.#at(text.length * 2 + 2);
    const view = this.#view();
    for (let i = 0; i < text.length; i++) view.setUint16(at + i * 2, text.charCodeAt(i), true);
    view.setUint16(at + text.length * 2, 0, true);
    return this;
  }

  raw(bytes: Uint8Array): this {
    const at = this.#at(bytes.length);
    this.#buf.set(bytes, at);
    return this;
  }

  /** Documented reserved/padding runs. */
  zeros(count: number): this {
    const at = this.#at(count);
    this.#buf.fill(0, at, this.#len);
    return this;
  }

  /** Embedded packet: u16 opcode, u32 size, bytes (`sub_5927F0`). */
  packet(inner: Packet): this {
    return this.u16(inner.opcode).u32(inner.length).raw(inner.payload());
  }

  payload(): Uint8Array {
    return this.#buf.subarray(0, this.#len);
  }

  /** Serialise to a complete encrypted frame. */
  encode(): Uint8Array {
    const payload = this.payload();
    const plain = payload.length;
    const aligned = align16(plain);
    if (aligned >= MAX_ENCRYPTED) {
      throw new RangeError(`payload ${plain} exceeds the encryptable maximum`);
    }

    const staging = new Uint8Array(aligned); // pad to the block size
    staging.set(payload);

    const frame = new Uint8Array(HEADER_SIZE + aligned);
    const view = new DataView(frame.buffer);
    view.setUint16(0, aligned, true);
    view.setUint16(2, this.opcode, true);
    view.setUint16(4, plain, true);
    view.setUint16(6, plain, true);
    frame.set(cfbEncrypt(PACKET_ROUND_KEYS, staging), HEADER_SIZE);
    return frame;
  }
}

// ---------------------------------------------------------------- reading

/** Reads a decoded payload. Throws on overrun rather than returning junk. */
export class Reader {
  readonly opcode: number;
  readonly #buf: Uint8Array;
  #pos = 0;

  constructor(opcode: number, payload: Uint8Array) {
    this.opcode = opcode;
    this.#buf = payload;
  }

  get remaining(): number {
    return this.#buf.length - this.#pos;
  }

  #at(count: number): number {
    const at = this.#pos;
    if (at + count > this.#buf.length) {
      throw new RangeError(`read of ${count} at ${at} exceeds payload ${this.#buf.length}`);
    }
    this.#pos = at + count;
    return at;
  }

  #view(): DataView {
    return new DataView(this.#buf.buffer, this.#buf.byteOffset, this.#buf.byteLength);
  }

  u8(): number {
    return this.#buf[this.#at(1)]!;
  }
  s8(): number {
    return this.#view().getInt8(this.#at(1));
  }
  u16(): number {
    return this.#view().getUint16(this.#at(2), true);
  }
  s16(): number {
    return this.#view().getInt16(this.#at(2), true);
  }
  u32(): number {
    return this.#view().getUint32(this.#at(4), true);
  }
  s32(): number {
    return this.#view().getInt32(this.#at(4), true);
  }
  u64(): bigint {
    return this.#view().getBigUint64(this.#at(8), true);
  }
  f32(): number {
    return this.#view().getFloat32(this.#at(4), true);
  }

  str(encoding: Encoding = "euc-kr", maxBytes?: number): string {
    const start = this.#pos;
    const end = this.#buf.indexOf(0, start);
    if (end < 0) throw new RangeError("unterminated ANSI string");
    if (maxBytes !== undefined && end - start > maxBytes) {
      throw new RangeError(`ANSI string exceeds ${maxBytes} bytes`);
    }
    this.#pos = end + 1;
    return decoder(encoding).decode(this.#buf.subarray(start, end));
  }

  wstr(): string {
    const start = this.#pos;
    let end = start;
    while (end + 1 < this.#buf.length && !(this.#buf[end] === 0 && this.#buf[end + 1] === 0)) {
      end += 2;
    }
    this.#pos = Math.min(end + 2, this.#buf.length);
    const view = this.#view();
    let out = "";
    for (let i = start; i < end; i += 2) out += String.fromCharCode(view.getUint16(i, true));
    return out;
  }

  raw(count: number): Uint8Array {
    return this.#buf.subarray(this.#at(count), this.#pos);
  }

  /** Unconsumed bytes, for logging unmapped tails. */
  rest(): Uint8Array {
    return this.#buf.subarray(this.#pos);
  }
}

// ------------------------------------------------------------ reassembly

/**
 * Buffers a TCP stream and yields whole packets. One per connection.
 *
 * Frames are validated with the client's own checks (`sub_593110`) before the
 * cipher sees them, and a bad header throws immediately rather than stalling
 * the connection waiting for bytes that can never form a valid frame.
 */
export class PacketStream {
  #pending = new Uint8Array(0);

  push(chunk: Uint8Array): void {
    if (this.#pending.length === 0) {
      this.#pending = new Uint8Array(chunk); // copy: Bun reuses socket buffers
      return;
    }
    const merged = new Uint8Array(this.#pending.length + chunk.length);
    merged.set(this.#pending);
    merged.set(chunk, this.#pending.length);
    this.#pending = merged;
  }

  /** Every complete packet currently buffered. */
  *drain(): Generator<Reader> {
    for (;;) {
      const buf = this.#pending;
      if (buf.length < HEADER_SIZE) return;

      const view = new DataView(buf.buffer, buf.byteOffset, buf.byteLength);
      const size = view.getUint16(0, true);
      const opcode = view.getUint16(2, true);
      const sizeBeforeAes = view.getUint16(4, true);
      const sizeBeforeLz = view.getUint16(6, true);

      if (size < BLOCK || size % BLOCK !== 0 || size >= MAX_ENCRYPTED) {
        throw new RangeError(`frame size ${size} fails the sub_593110 checks`);
      }
      if (size !== align16(sizeBeforeAes)) {
        throw new RangeError(`frame size ${size} is not align16(${sizeBeforeAes})`);
      }
      if (buf.length < HEADER_SIZE + size) return; // wait for the rest

      // We negotiate compression off via 694, so nobody should ever send it.
      if (sizeBeforeLz >= COMPRESSION_DISABLED && sizeBeforeAes < sizeBeforeLz) {
        throw new RangeError(`frame claims LZ compression (word3=${sizeBeforeLz})`);
      }

      const body = buf.subarray(HEADER_SIZE, HEADER_SIZE + size);
      const plain = cfbDecrypt(PACKET_ROUND_KEYS, body).subarray(0, sizeBeforeAes);
      this.#pending = buf.subarray(HEADER_SIZE + size);
      yield new Reader(opcode, plain);
    }
  }
}

/** Decode a single self-contained frame. Convenience for tests. */
export function decode(frame: Uint8Array): Reader {
  const stream = new PacketStream();
  stream.push(frame);
  const [first] = [...stream.drain()];
  if (!first) throw new RangeError("incomplete frame");
  return first;
}
