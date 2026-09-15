/**
 * Cross-check the TypeScript codec against the existing Python implementation
 * in `server/packet.py`, which the Python verifier suite already exercises.
 *
 * The two only overlap on the unencrypted body layout: packet.py predates the
 * discovery that `seal()` is dead code and does not implement the AES stage.
 * So the shared contract is "same payload bytes for the same writes", which is
 * what an independent reimplementation should agree on.
 */
import { describe, expect, test } from "bun:test";
import { PacketWriter } from "../src/codec/packet.ts";

const repoRoot = new URL("../../", import.meta.url).pathname;

async function pythonPayload(script: string): Promise<string> {
  const proc = Bun.spawn(["python3", "-c", script], {
    cwd: repoRoot,
    stdout: "pipe",
    stderr: "pipe",
  });
  const [out, err, code] = await Promise.all([
    new Response(proc.stdout).text(),
    new Response(proc.stderr).text(),
    proc.exited,
  ]);
  if (code !== 0) throw new Error(`python failed: ${err}`);
  return out.trim();
}

const hex = (bytes: Uint8Array): string => Buffer.from(bytes).toString("hex");

describe("payload layout matches server/packet.py", () => {
  test("login-shaped packet", async () => {
    const expected = await pythonPayload(`
import sys; sys.path.insert(0, 'server')
from packet import Packet
p = Packet(682)
p.write_str('alice').write_str('token123')
p.write_u64(0x1122334455667788)
p.write_u8(2)
p.write_raw(b'\\x00' * 24)
print(bytes(p.buf).hex())
`);

    const packet = new PacketWriter(682)
      .str("alice")
      .str("token123")
      .u64(0x1122334455667788n)
      .u8(2)
      .zeros(24);

    expect(hex(packet.payload())).toBe(expected);
  });

  test("inventory-entry-shaped packet", async () => {
    const expected = await pythonPayload(`
import sys; sys.path.insert(0, 'server')
from packet import Packet
a = Packet(200)
a.write_s8(1); a.write_s32(0); a.write_s32(3); a.write_s32(1001)
a.write_f32(1.0); a.write_f32(0.5)
a.write_s32(30); a.write_u16(100); a.write_s32(-1)
print(bytes(a.buf).hex())
`);

    const packet = new PacketWriter(200)
      .s8(1)
      .s32(0)
      .s32(3)
      .s32(1001)
      .f32(1.0)
      .f32(0.5)
      .s32(30)
      .u16(100)
      .s32(-1);

    expect(hex(packet.payload())).toBe(expected);
  });

  test("signed and float edge values agree", async () => {
    const expected = await pythonPayload(`
import sys; sys.path.insert(0, 'server')
from packet import Packet
p = Packet(1)
p.write_s8(-128); p.write_s16(-32768); p.write_s32(-2147483648)
p.write_u16(65535); p.write_u32(4294967295)
p.write_f32(-0.0); p.write_f32(3.4028234663852886e38)
p.write_wstr('紙片人')
print(bytes(p.buf).hex())
`);

    const packet = new PacketWriter(1)
      .s8(-128)
      .s16(-32768)
      .s32(-2147483648)
      .u16(65535)
      .u32(4294967295)
      .f32(-0)
      .f32(3.4028234663852886e38)
      .wstr("紙片人");

    expect(hex(packet.payload())).toBe(expected);
  });
});
