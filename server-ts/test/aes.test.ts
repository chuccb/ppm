import { describe, expect, test } from "bun:test";
import {
  PACKET_KEY,
  PACKET_ROUND_KEYS,
  cfbDecrypt,
  cfbEncrypt,
  encryptBlock,
  expandKey,
} from "../src/aes.ts";

const hex = (bytes: Uint8Array): string => Buffer.from(bytes).toString("hex").toUpperCase();
const unhex = (text: string): Uint8Array => Uint8Array.from(Buffer.from(text, "hex"));

describe("AES-128 core", () => {
  // FIPS-197 C.1 — the published appendix vector. If this fails the cipher is
  // wrong in general, independently of anything PaperMan-specific.
  test("FIPS-197 C.1 ECB vector", () => {
    const keys = expandKey(unhex("000102030405060708090a0b0c0d0e0f"));
    const block = unhex("00112233445566778899aabbccddeeff");
    encryptBlock(keys, block);
    expect(hex(block)).toBe("69C4E0D86A7B0430D8CDB78070B4C55A");
  });

  test("the packet key is the EUC-KR literal from sub_403430", () => {
    expect(hex(PACKET_KEY)).toBe("C6AEB7B7C5A9C1A1B7C9C0FCB8D3C1F6");
    // 「트렁크점령전머지」 encoded as EUC-KR / CP949.
    const decoded = new TextDecoder("euc-kr").decode(PACKET_KEY);
    expect(decoded).toBe("트렁크점령전머지");
  });
});

describe("packet-key vectors (docs/PACKETS.md §1.4)", () => {
  // These were verified against real client login and Ping frames.
  test("ECB vector 1", () => {
    const block = unhex("000102030405060708090A0B0C0D0E0F");
    encryptBlock(PACKET_ROUND_KEYS, block);
    expect(hex(block)).toBe("D7F8930CFE8758AD7BF2FEF759EBB845");
  });

  test("ECB vector 2", () => {
    const block = new Uint8Array(Buffer.from("PaperMan-Packet!", "latin1"));
    encryptBlock(PACKET_ROUND_KEYS, block);
    expect(hex(block)).toBe("8B8ABD9B2B743448188ED7E554BD4AA2");
  });

  // The two CFB vectors previously recorded in PACKETS.md were mutually
  // inconsistent: a first-block CFB keystream is AES(IV) and cannot depend on
  // the plaintext, yet those two implied keystreams agreed on only 1 of 16
  // bytes. The doc has been corrected; these are the recomputed values, and
  // they are consistent with a single keystream AES(0).
  test("first-block keystream is AES(IV=0)", () => {
    const zero = new Uint8Array(16);
    encryptBlock(PACKET_ROUND_KEYS, zero);
    expect(hex(zero)).toBe("3AF35BF885F6BC18FF0B59F8DFEC3248");
  });

  test("CFB-128 vector 1", () => {
    const out = cfbEncrypt(PACKET_ROUND_KEYS, unhex("000102030405060708090A0B0C0D0E0F"));
    expect(hex(out)).toBe("3AF259FB81F3BA1FF70253F3D3E13C47");
  });

  test("CFB-128 vector 2", () => {
    const input = new Uint8Array(Buffer.from("PaperMan-Packet!", "latin1"));
    const out = cfbEncrypt(PACKET_ROUND_KEYS, input);
    expect(hex(out)).toBe("6A922B9DF7BBDD76D25B389BB4894669");
  });

  test("both vectors imply the same keystream", () => {
    const p1 = unhex("000102030405060708090A0B0C0D0E0F");
    const p2 = new Uint8Array(Buffer.from("PaperMan-Packet!", "latin1"));
    const ks1 = cfbEncrypt(PACKET_ROUND_KEYS, p1).map((v: number, i: number) => v ^ p1[i]!);
    const ks2 = cfbEncrypt(PACKET_ROUND_KEYS, p2).map((v: number, i: number) => v ^ p2[i]!);
    expect(hex(ks1)).toBe(hex(ks2));
  });

  test("CFB round-trips across multiple blocks", () => {
    const plain = new Uint8Array(64);
    for (let i = 0; i < plain.length; i++) plain[i] = (i * 7) & 0xff;
    const round = cfbDecrypt(PACKET_ROUND_KEYS, cfbEncrypt(PACKET_ROUND_KEYS, plain));
    expect(hex(round)).toBe(hex(plain));
  });

  test("CFB rejects unaligned input", () => {
    expect(() => cfbEncrypt(PACKET_ROUND_KEYS, new Uint8Array(15))).toThrow(RangeError);
  });
});
