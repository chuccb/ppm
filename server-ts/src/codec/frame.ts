/**
 * Frame encode/decode: the two-stage pipeline from docs/PACKETS.md §1.4.
 *
 * Send (`sub_555090` -> `sub_593280`):
 *   1. word3 := word0 on the first send only.
 *   2. Optional LZ compression when word0 >= the negotiated threshold.
 *   3. Always AES-128-CFB over a 16-byte-aligned buffer; word2 := pre-encrypt
 *      size, word0 := aligned size.
 *
 * Receive (`sub_555280`):
 *   1. Reassemble until word0 + 8 bytes are available.
 *   2. AES decrypt, validating word0 >= 16, word0 == align16(word2),
 *      word0 % 16 == 0 and word0 < 0x2578; then word0 := word2.
 *   3. LZ decompress when word3 >= threshold and word0 < word3.
 *
 * Compression is negotiated by opcode 694. The client only lowers its
 * threshold when the value is strictly below 0x2580, so sending 0x2580 keeps
 * compression off in both directions — which is what this server does, and why
 * the LZ stage is not implemented. `decodeFrame` throws rather than guessing if
 * a peer ever sends a compressed frame.
 */

import { PACKET_ROUND_KEYS, cfbDecrypt, cfbEncrypt } from "./aes.ts";
import { HEADER_SIZE, MAX_ENCRYPTED, PacketReader, type PacketWriter } from "./packet.ts";

const BLOCK = 16;

/** Compression is disabled at this value (`n0x2580`, the client's initial). */
export const COMPRESSION_DISABLED = 0x2580;

function align16(size: number): number {
  return size === 0 ? BLOCK : Math.ceil(size / BLOCK) * BLOCK;
}

/** Serialise a packet into a complete, encrypted wire frame. */
export function encodeFrame(packet: PacketWriter): Uint8Array {
  const payload = packet.payload();
  const original = payload.length;
  const aligned = align16(original);

  if (aligned >= MAX_ENCRYPTED) {
    throw new RangeError(`payload ${original} exceeds the encryptable maximum`);
  }

  // Pad to the block size; sub_592FB0 encrypts the whole aligned buffer.
  const staging = new Uint8Array(aligned);
  staging.set(payload);
  const encrypted = cfbEncrypt(PACKET_ROUND_KEYS, staging);

  const frame = new Uint8Array(HEADER_SIZE + aligned);
  const view = new DataView(frame.buffer);
  view.setUint16(0, aligned, true); // word0: on-wire size
  view.setUint16(2, packet.opcode, true); // word1: opcode
  view.setUint16(4, original, true); // word2: size before encryption
  view.setUint16(6, original, true); // word3: size before compression
  frame.set(encrypted, HEADER_SIZE);
  return frame;
}

export interface DecodedFrame {
  readonly reader: PacketReader;
  /** Total bytes consumed from the stream, including the header. */
  readonly consumed: number;
}

/**
 * Try to decode one frame from the head of `buffer`.
 * Returns null when more bytes are required (a partial frame).
 */
export function decodeFrame(buffer: Uint8Array): DecodedFrame | null {
  if (buffer.length < HEADER_SIZE) return null;

  const view = new DataView(buffer.buffer, buffer.byteOffset, buffer.byteLength);
  const onWire = view.getUint16(0, true); // word0
  const opcode = view.getUint16(2, true); // word1
  const preEncrypt = view.getUint16(4, true); // word2
  const preCompress = view.getUint16(6, true); // word3

  // Validate the header before waiting for a body: a corrupt length must fail
  // loudly rather than stall the connection forever waiting for bytes that
  // will never be a valid frame. These are the client's own checks from
  // sub_593110.
  if (onWire < BLOCK || onWire % BLOCK !== 0 || onWire >= MAX_ENCRYPTED) {
    throw new RangeError(`frame size ${onWire} fails the sub_593110 checks`);
  }
  if (onWire !== align16(preEncrypt)) {
    throw new RangeError(`frame size ${onWire} is not align16(${preEncrypt})`);
  }

  const total = HEADER_SIZE + onWire;
  if (buffer.length < total) return null; // wait for the rest

  const decrypted = cfbDecrypt(PACKET_ROUND_KEYS, buffer.subarray(HEADER_SIZE, total));
  const payload = decrypted.subarray(0, preEncrypt);

  // With the threshold pinned at 0x2580 no peer should ever compress.
  if (preCompress >= COMPRESSION_DISABLED && preEncrypt < preCompress) {
    throw new RangeError(
      `frame claims LZ compression (word3=${preCompress}); ` +
        "this server negotiates compression off via opcode 694",
    );
  }

  return { reader: new PacketReader(opcode, payload), consumed: total };
}

/** Incremental stream reassembler, one per connection. */
export class FrameStream {
  #pending: Uint8Array = new Uint8Array(0);

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

  /** Drain every complete frame currently buffered. */
  *drain(): Generator<PacketReader> {
    for (;;) {
      const frame = decodeFrame(this.#pending);
      if (!frame) return;
      this.#pending = this.#pending.subarray(frame.consumed);
      yield frame.reader;
    }
  }
}
