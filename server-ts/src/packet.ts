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
 *
 * Every primitive here is one native helper, dual on both directions
 * (docs/LAYOUTS.md 圖例)`sub_592xxx` —— TS writer ↔ client C2S 寫入 ↔
 * client S2C 讀取:
 *
 *   TS            client 寫入 helper          client 讀取 helper
 *   u8            sub_592920 / sub_592960     sub_592940 / sub_592980
 *   s8            sub_5928E0                  sub_592900 (s8/bool)
 *   u16           sub_5929A0                  sub_592A00
 *   s16           sub_5929E0                  sub_5929C0
 *   s32           sub_592A20                  sub_592A40
 *   u32           sub_592A60                  sub_592A80
 *   u64           sub_592AE0 / sub_592B60     sub_592B00 / sub_592B80
 *   f32           sub_592B20                  sub_592B40
 *   (raw4投影)     sub_592AA0 / sub_592AC0     sub_592AC0 (caller-defined)
 *   str           sub_5926F0                  sub_592730
 *   wstr          sub_592770                  sub_5927B0
 *   raw / zeros   sub_592580 (rawN) 等        sub_592C40 / sub_592500 (定寬 raw)
 *   packet        sub_5927F0 (embedded)       —
 *
 * `sub_592AA0`/`592AC0` 是 caller-defined generic 4B 寫/讀：wire 只有寬度,
 * signedness 由欄位文件決定,因此 TS 端以 u32/s32/f32 的「投影」承接,
 * 註解一律寫 raw4(禁止反向命名成具體型別,見 STYLE.md 權威層級)。
 *
 * `packet(...)`（embedded packet 寫入）行級實證的三類合法觀測位,
 * 每一類都是建構期把一個完整 packet 實體(與 `v121` 同型,~19KB 陣列槽)
 * 嵌進目前的 packet:
 *
 *   類                  觀測位(.c 行級)
 *   user bound packet   707 `sub_46AD00`:
 *   (user_args, v-L/H)  `Packet::possible_ctor_or_dtor_1(v121, a2)` ——
 *                       把呼叫端傳入的 packet `a2` 重綁成 `v121`,再
 *                       `switch (sub_591EE0(v121))` 依 opcode 分派;
 *                       case 707 本體只讀一支 `str token`(→ this+521173)
 *   plugin worker       700 `sub_4618E0`:
 *   packet (v-U 分槽)   `sub_4077C0(&v15, 707)` / `sub_4077C0(&v17, 711)`
 *                       等 4 支 ctor,每支綁定自己的 opcode(693/695/707/711)
 *                       並個別掛 plugin-registered dtor([0x8910] 大小槽)
 *   byte fragment       700 的 v-L 值為兩個 4B int buffer
 *   (STREAM_ASK 細欄)   倒序列印(`u32 long & 0xFFFF` 為 v-U,
 *                       其餘位元為 v-L):wire 上仍是 4B 整數欄
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

/** The Korean client's ANSI strings are CP949 (`euc-kr` in WHATWG). */
const ANSI_DECODER = new TextDecoder("euc-kr");

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

  #checkInt(kind: string, v: number, min: number, max: number): void {
    if (!Number.isSafeInteger(v) || v < min || v > max) {
      throw new RangeError(
        `${kind} expects an integer in ${min}..${max}, got ${v}`,
      );
    }
  }

  constructor(opcode: number) {
    this.opcode = opcode & 0xffff;
  }

  get length(): number {
    return this.#len;
  }

  /**
   * Reserve `byteCount` bytes and return the offset to write at.
   *
   * Callers must resolve this *before* touching `#buf` or `#view()`: it may
   * reallocate, and `this.#view().setX(this.#at(n), ...)` would evaluate the
   * view against the old buffer and write into the copy that gets discarded.
   */
  #at(byteCount: number): number {
    const at = this.#len;
    const needed = at + byteCount;
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
    this.#checkInt("u8", v, 0, 0xff);
    // #at may reallocate, so resolve the offset before touching #buf.
    const at = this.#at(1);
    this.#buf[at] = v;
    return this;
  }
  s8(v: number): this {
    this.#checkInt("s8", v, -0x80, 0x7f);
    const at = this.#at(1);
    this.#view().setInt8(at, v);
    return this;
  }
  u16(v: number): this {
    this.#checkInt("u16", v, 0, 0xffff);
    const at = this.#at(2);
    this.#view().setUint16(at, v, true);
    return this;
  }
  s16(v: number): this {
    this.#checkInt("s16", v, -0x8000, 0x7fff);
    const at = this.#at(2);
    this.#view().setInt16(at, v, true);
    return this;
  }
  u32(v: number): this {
    this.#checkInt("u32", v, 0, 0xffff_ffff);
    const at = this.#at(4);
    this.#view().setUint32(at, v, true);
    return this;
  }
  s32(v: number): this {
    this.#checkInt("s32", v, -0x8000_0000, 0x7fff_ffff);
    const at = this.#at(4);
    this.#view().setInt32(at, v, true);
    return this;
  }
  u64(v: bigint): this {
    if (v < 0n || v > 0xffff_ffff_ffff_ffffn) {
      throw new RangeError(
        `u64 expects an integer in 0..18446744073709551615, got ${v}`,
      );
    }
    const at = this.#at(8);
    this.#view().setBigUint64(at, v, true);
    return this;
  }
  f32(v: number): this {
    if (!Number.isFinite(v) || !Number.isFinite(Math.fround(v))) {
      throw new RangeError(
        `f32 expects a finite f32-representable number, got ${v}`,
      );
    }
    const at = this.#at(4);
    this.#view().setFloat32(at, v, true);
    return this;
  }

  /** ANSI bytes + NUL (`sub_5926F0`). ASCII only; use `wstr` for the rest. */
  str(text: string): this {
    if (typeof text !== "string") throw new TypeError("ANSI string must be a string");
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
    if (typeof text !== "string") throw new TypeError("UTF-16 string must be a string");
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
    if (!Number.isSafeInteger(count) || count < 0) {
      throw new RangeError("zero run length must be a non-negative integer");
    }
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
    const sizeBeforeAes = payload.length;
    const size = align16(sizeBeforeAes);
    if (size >= MAX_ENCRYPTED) {
      throw new RangeError(`payload ${sizeBeforeAes} exceeds the encryptable maximum`);
    }

    const paddedPayload = new Uint8Array(size); // pad to the block size
    paddedPayload.set(payload);

    const frame = new Uint8Array(HEADER_SIZE + size);
    const view = new DataView(frame.buffer);
    view.setUint16(0, size, true);
    view.setUint16(2, this.opcode, true);
    view.setUint16(4, sizeBeforeAes, true);
    view.setUint16(6, sizeBeforeAes, true);
    frame.set(cfbEncrypt(PACKET_ROUND_KEYS, paddedPayload), HEADER_SIZE);
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

  str(maxBytes?: number): string {
    if (maxBytes !== undefined && (!Number.isSafeInteger(maxBytes) || maxBytes < 0)) {
      throw new RangeError("ANSI string limit must be a non-negative integer");
    }
    const start = this.#pos;
    const end = this.#buf.indexOf(0, start);
    if (end < 0) throw new RangeError("unterminated ANSI string");
    if (maxBytes !== undefined && end - start > maxBytes) {
      throw new RangeError(`ANSI string exceeds ${maxBytes} bytes`);
    }
    this.#pos = end + 1;
    return ANSI_DECODER.decode(this.#buf.subarray(start, end));
  }

  wstr(): string {
    const start = this.#pos;
    let end = start;
    while (end + 1 < this.#buf.length && !(this.#buf[end] === 0 && this.#buf[end + 1] === 0)) {
      end += 2;
    }
    if (end + 1 >= this.#buf.length) throw new RangeError("unterminated UTF-16 string");
    this.#pos = end + 2;
    const view = this.#view();
    let out = "";
    for (let i = start; i < end; i += 2) out += String.fromCharCode(view.getUint16(i, true));
    return out;
  }

  raw(count: number): Uint8Array {
    if (!Number.isSafeInteger(count) || count < 0) {
      throw new RangeError("raw read length must be a non-negative integer");
    }
    return this.#buf.subarray(this.#at(count), this.#pos);
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

      const encryptedPayload = buf.subarray(HEADER_SIZE, HEADER_SIZE + size);
      const payload = cfbDecrypt(PACKET_ROUND_KEYS, encryptedPayload).subarray(0, sizeBeforeAes);
      this.#pending = buf.subarray(HEADER_SIZE + size);
      yield new Reader(opcode, payload);
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
