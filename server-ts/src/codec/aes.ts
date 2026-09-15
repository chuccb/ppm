/**
 * AES-128 in the exact shape the PaperMan client uses.
 *
 * Evidence (docs/PACKETS.md §1.4):
 *   - `sub_403430` is a standard FIPS-197 AES-128 key schedule (10 rounds,
 *     16-byte block), verified word-by-word against the spec.
 *   - The packet pipeline (`sub_592FB0` encrypt / `sub_593110` decrypt) always
 *     passes mode selector `n2 = 2`, which is 128-bit CFB with a zero IV.
 *   - The key is the EUC-KR literal 「트렁크점령전머지」:
 *     C6 AE B7 B7 C5 A9 C1 A1 B7 C9 C0 FC B8 D3 C1 F6
 *
 * Only the pieces the protocol actually needs are implemented: a block
 * encryptor (CFB never invokes the inverse cipher) plus CFB-128 both ways.
 * Test vectors from the same section are asserted in test/aes.test.ts.
 */

const BLOCK = 16;
const ROUNDS = 10;

/** The client's hardcoded AES-128 key. */
export const PACKET_KEY: Uint8Array = Uint8Array.from([
  0xc6, 0xae, 0xb7, 0xb7, 0xc5, 0xa9, 0xc1, 0xa1, 0xb7, 0xc9, 0xc0, 0xfc, 0xb8,
  0xd3, 0xc1, 0xf6,
]);

const SBOX: Uint8Array = /* @__PURE__ */ (() => {
  // Generated rather than table-pasted so it is self-evidently FIPS-197.
  const sbox = new Uint8Array(256);
  const inv = new Uint8Array(256);

  // Multiplicative inverse in GF(2^8) via exp/log over generator 0x03.
  const exp = new Uint8Array(512);
  const log = new Uint8Array(256);
  let x = 1;
  for (let i = 0; i < 255; i++) {
    exp[i] = x;
    log[x] = i;
    x ^= (x << 1) ^ (x & 0x80 ? 0x11b : 0); // xtime + reduce
    x &= 0xff;
  }
  for (let i = 255; i < 512; i++) exp[i] = exp[i - 255]!;
  inv[0] = 0;
  for (let i = 1; i < 256; i++) inv[i] = exp[255 - log[i]!]!;

  for (let i = 0; i < 256; i++) {
    const c = inv[i]!;
    // Affine transform: s = c ^ rotl(c,1) ^ rotl(c,2) ^ rotl(c,3) ^ rotl(c,4) ^ 0x63
    let s = c ^ 0x63;
    for (const r of [1, 2, 3, 4]) s ^= ((c << r) | (c >>> (8 - r))) & 0xff;
    sbox[i] = s & 0xff;
  }
  return sbox;
})();

function xtime(a: number): number {
  return ((a << 1) ^ (a & 0x80 ? 0x1b : 0)) & 0xff;
}

function mul(a: number, b: number): number {
  let result = 0;
  let x = a;
  let y = b;
  while (y) {
    if (y & 1) result ^= x;
    x = xtime(x);
    y >>>= 1;
  }
  return result & 0xff;
}

/** Expanded encryption round keys: 11 * 16 bytes. */
export type RoundKeys = Uint8Array;

export function expandKey(key: Uint8Array): RoundKeys {
  if (key.length !== BLOCK) throw new RangeError("AES-128 requires a 16-byte key");
  const out = new Uint8Array(BLOCK * (ROUNDS + 1));
  out.set(key, 0);

  let rcon = 1;
  for (let i = BLOCK; i < out.length; i += 4) {
    let a = out[i - 4]!;
    let b = out[i - 3]!;
    let c = out[i - 2]!;
    let d = out[i - 1]!;

    if (i % BLOCK === 0) {
      // RotWord + SubWord + rcon
      [a, b, c, d] = [SBOX[b]! ^ rcon, SBOX[c]!, SBOX[d]!, SBOX[a]!];
      rcon = xtime(rcon);
    }

    out[i] = out[i - BLOCK]! ^ a;
    out[i + 1] = out[i - BLOCK + 1]! ^ b;
    out[i + 2] = out[i - BLOCK + 2]! ^ c;
    out[i + 3] = out[i - BLOCK + 3]! ^ d;
  }
  return out;
}

/** Encrypt one 16-byte block in place. */
export function encryptBlock(roundKeys: RoundKeys, block: Uint8Array): void {
  const s = block;
  for (let i = 0; i < BLOCK; i++) s[i] = s[i]! ^ roundKeys[i]!;

  for (let round = 1; round <= ROUNDS; round++) {
    // SubBytes
    for (let i = 0; i < BLOCK; i++) s[i] = SBOX[s[i]!]!;

    // ShiftRows (column-major state: byte index = 4*col + row)
    for (let row = 1; row < 4; row++) {
      const tmp = [s[row]!, s[row + 4]!, s[row + 8]!, s[row + 12]!];
      for (let col = 0; col < 4; col++) s[4 * col + row] = tmp[(col + row) % 4]!;
    }

    // MixColumns (skipped in the final round)
    if (round !== ROUNDS) {
      for (let col = 0; col < 4; col++) {
        const o = 4 * col;
        const a0 = s[o]!;
        const a1 = s[o + 1]!;
        const a2 = s[o + 2]!;
        const a3 = s[o + 3]!;
        s[o] = mul(a0, 2) ^ mul(a1, 3) ^ a2 ^ a3;
        s[o + 1] = a0 ^ mul(a1, 2) ^ mul(a2, 3) ^ a3;
        s[o + 2] = a0 ^ a1 ^ mul(a2, 2) ^ mul(a3, 3);
        s[o + 3] = mul(a0, 3) ^ a1 ^ a2 ^ mul(a3, 2);
      }
    }

    // AddRoundKey
    const base = round * BLOCK;
    for (let i = 0; i < BLOCK; i++) s[i] = s[i]! ^ roundKeys[base + i]!;
  }
}

/**
 * AES-128-CFB with a 128-bit feedback window and a zero IV, matching
 * `sub_4042A0(..., n2 = 2)`. Input length must be a multiple of 16; the packet
 * layer is responsible for the padding decision.
 */
export function cfbEncrypt(roundKeys: RoundKeys, data: Uint8Array): Uint8Array {
  if (data.length % BLOCK !== 0) {
    throw new RangeError(`CFB input must be block-aligned, got ${data.length}`);
  }
  const out = new Uint8Array(data.length);
  const feedback = new Uint8Array(BLOCK); // IV = 0
  const keystream = new Uint8Array(BLOCK);

  for (let off = 0; off < data.length; off += BLOCK) {
    keystream.set(feedback);
    encryptBlock(roundKeys, keystream);
    for (let i = 0; i < BLOCK; i++) out[off + i] = data[off + i]! ^ keystream[i]!;
    feedback.set(out.subarray(off, off + BLOCK)); // ciphertext feedback
  }
  return out;
}

export function cfbDecrypt(roundKeys: RoundKeys, data: Uint8Array): Uint8Array {
  if (data.length % BLOCK !== 0) {
    throw new RangeError(`CFB input must be block-aligned, got ${data.length}`);
  }
  const out = new Uint8Array(data.length);
  const feedback = new Uint8Array(BLOCK); // IV = 0
  const keystream = new Uint8Array(BLOCK);

  for (let off = 0; off < data.length; off += BLOCK) {
    keystream.set(feedback);
    encryptBlock(roundKeys, keystream);
    for (let i = 0; i < BLOCK; i++) out[off + i] = data[off + i]! ^ keystream[i]!;
    feedback.set(data.subarray(off, off + BLOCK)); // ciphertext feedback
  }
  return out;
}

/** Round keys for the packet key, expanded once. */
export const PACKET_ROUND_KEYS: RoundKeys = expandKey(PACKET_KEY);
