/**
 * Wire packet reader/writer.
 *
 * Frame layout (docs/PACKETS.md §1.2), all little-endian and unaligned:
 *
 *   offset 0  u16  payload size        (word0)
 *   offset 2  u16  opcode              (word1) — the dispatcher switches on this
 *   offset 4  u16  size before encrypt (word2) — written only by the AES layer
 *   offset 6  u16  size before compress(word3) — written on first send
 *   offset 8  ...  payload
 *
 * Strings have no length prefix; they are NUL-terminated inside the payload
 * (`sub_5926F0` writes `lstrlenA + 1`). The Korean client encoding is CP949.
 */

export const HEADER_SIZE = 8;
/** `sub_591DA0`: the payload buffer is 9600 bytes including the 8-byte header. */
export const MAX_PAYLOAD = 9592;
/** `sub_592FB0` refuses to encrypt at or beyond this aligned size. */
export const MAX_ENCRYPTED = 0x2578;

/**
 * Legacy code pages the client actually uses. Note the Korean build is CP949,
 * but the WHATWG label for it is "euc-kr" -- Bun rejects "cp949" outright.
 */
export type AnsiEncoding = "euc-kr" | "shift_jis" | "utf-8";

const decoderCache = new Map<AnsiEncoding, TextDecoder>();
function decoderFor(encoding: AnsiEncoding): TextDecoder {
  let decoder = decoderCache.get(encoding);
  if (!decoder) {
    decoder = new TextDecoder(encoding, { fatal: false });
    decoderCache.set(encoding, decoder);
  }
  return decoder;
}

/** CP949/EUC-KR encoder. Bun's TextEncoder is UTF-8 only, so map via Buffer. */
function encodeAnsi(text: string): Uint8Array {
  // Bun's Buffer supports the legacy codepages through iconv internally for
  // decode only, so encode by round-tripping code points we can represent.
  // ASCII is the overwhelmingly common case and is byte-identical in CP949.
  let ascii = true;
  for (let i = 0; i < text.length; i++) {
    if (text.charCodeAt(i) > 0x7f) {
      ascii = false;
      break;
    }
  }
  if (ascii) return new Uint8Array(Buffer.from(text, "latin1"));
  throw new RangeError(
    "non-ASCII ANSI string encoding is not implemented; use writeWide or pass bytes",
  );
}

export class PacketWriter {
  readonly opcode: number;
  #buf: Uint8Array;
  #len = 0;

  constructor(opcode: number, capacity = 256) {
    this.opcode = opcode & 0xffff;
    this.#buf = new Uint8Array(capacity);
  }

  get length(): number {
    return this.#len;
  }

  #need(extra: number): number {
    const at = this.#len;
    const required = at + extra;
    if (required > MAX_PAYLOAD) {
      throw new RangeError(`payload would exceed ${MAX_PAYLOAD} bytes`);
    }
    if (required > this.#buf.length) {
      // Grow by doubling, but never below `required` -- clamping to
      // MAX_PAYLOAD must not produce a buffer smaller than the write needs.
      let size = Math.max(this.#buf.length * 2, 16);
      while (size < required) size *= 2;
      const grown = new Uint8Array(Math.max(Math.min(size, MAX_PAYLOAD), required));
      grown.set(this.#buf.subarray(0, at));
      this.#buf = grown;
    }
    this.#len = required;
    return at;
  }

  #view(): DataView {
    return new DataView(this.#buf.buffer, this.#buf.byteOffset, this.#buf.byteLength);
  }

  u8(value: number): this {
    const at = this.#need(1);
    this.#buf[at] = value & 0xff;
    return this;
  }

  s8(value: number): this {
    this.#view().setInt8(this.#need(1), value);
    return this;
  }

  u16(value: number): this {
    this.#view().setUint16(this.#need(2), value & 0xffff, true);
    return this;
  }

  s16(value: number): this {
    this.#view().setInt16(this.#need(2), value, true);
    return this;
  }

  u32(value: number): this {
    this.#view().setUint32(this.#need(4), value >>> 0, true);
    return this;
  }

  s32(value: number): this {
    this.#view().setInt32(this.#need(4), value | 0, true);
    return this;
  }

  u64(value: bigint): this {
    this.#view().setBigUint64(this.#need(8), value, true);
    return this;
  }

  f32(value: number): this {
    this.#view().setFloat32(this.#need(4), value, true);
    return this;
  }

  /** `sub_5926F0`: ANSI (CP949) bytes followed by a NUL. */
  str(text: string): this {
    const bytes = encodeAnsi(text);
    const at = this.#need(bytes.length + 1);
    this.#buf.set(bytes, at);
    this.#buf[at + bytes.length] = 0;
    return this;
  }

  /** `sub_592770`: UTF-16LE followed by a 16-bit NUL. */
  wstr(text: string): this {
    const at = this.#need(text.length * 2 + 2);
    const view = this.#view();
    for (let i = 0; i < text.length; i++) {
      view.setUint16(at + i * 2, text.charCodeAt(i), true);
    }
    view.setUint16(at + text.length * 2, 0, true);
    return this;
  }

  raw(bytes: Uint8Array): this {
    // #need may reallocate, so resolve the offset first and re-read #buf
    // afterwards -- `this.#buf.set(x, this.#need(n))` would capture the old
    // buffer before it grows.
    const at = this.#need(bytes.length);
    this.#buf.set(bytes, at);
    return this;
  }

  /** Zero-fill, for documented reserved/padding runs. */
  zeros(count: number): this {
    const at = this.#need(count);
    this.#buf.fill(0, at, this.#len);
    return this;
  }

  /** `sub_5927F0`: embedded packet — u16 opcode, u32 size, then bytes. */
  packet(inner: PacketWriter): this {
    this.u16(inner.opcode);
    this.u32(inner.length);
    return this.raw(inner.payload());
  }

  payload(): Uint8Array {
    return this.#buf.subarray(0, this.#len);
  }
}

export class PacketReader {
  readonly opcode: number;
  readonly #buf: Uint8Array;
  #pos = 0;

  constructor(opcode: number, payload: Uint8Array) {
    this.opcode = opcode;
    this.#buf = payload;
  }

  get offset(): number {
    return this.#pos;
  }

  get remaining(): number {
    return this.#buf.length - this.#pos;
  }

  #take(count: number): number {
    const at = this.#pos;
    if (at + count > this.#buf.length) {
      throw new RangeError(
        `read of ${count} at ${at} exceeds payload length ${this.#buf.length}`,
      );
    }
    this.#pos = at + count;
    return at;
  }

  #view(): DataView {
    return new DataView(this.#buf.buffer, this.#buf.byteOffset, this.#buf.byteLength);
  }

  u8(): number {
    return this.#buf[this.#take(1)]!;
  }

  s8(): number {
    return this.#view().getInt8(this.#take(1));
  }

  u16(): number {
    return this.#view().getUint16(this.#take(2), true);
  }

  s16(): number {
    return this.#view().getInt16(this.#take(2), true);
  }

  u32(): number {
    return this.#view().getUint32(this.#take(4), true);
  }

  s32(): number {
    return this.#view().getInt32(this.#take(4), true);
  }

  u64(): bigint {
    return this.#view().getBigUint64(this.#take(8), true);
  }

  f32(): number {
    return this.#view().getFloat32(this.#take(4), true);
  }

  str(encoding: AnsiEncoding = "euc-kr"): string {
    const start = this.#pos;
    const end = this.#buf.indexOf(0, start);
    if (end < 0) throw new RangeError("unterminated ANSI string");
    this.#pos = end + 1;
    return decoderFor(encoding).decode(this.#buf.subarray(start, end));
  }

  wstr(): string {
    const start = this.#pos;
    let end = start;
    while (end + 1 < this.#buf.length && !(this.#buf[end] === 0 && this.#buf[end + 1] === 0)) {
      end += 2;
    }
    this.#pos = Math.min(end + 2, this.#buf.length);
    let out = "";
    const view = this.#view();
    for (let i = start; i < end; i += 2) out += String.fromCharCode(view.getUint16(i, true));
    return out;
  }

  raw(count: number): Uint8Array {
    return this.#buf.subarray(this.#take(count), this.#pos);
  }

  /** Bytes not yet consumed — useful for logging unmapped tails. */
  rest(): Uint8Array {
    return this.#buf.subarray(this.#pos);
  }
}
