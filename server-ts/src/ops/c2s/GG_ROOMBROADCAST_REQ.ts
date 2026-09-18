/**
 * 437 room broadcast (GG_) — legacy/special-tool opcode.
 *
 * Native builder `sub_55B430(flag: u8, blob: ptr, len: usize)`:
 * wire is `{u8 flag, s32 len, raw[len]}` written on the GL lobby socket
 * (`dword_1321D00`). The builder has NO reachable caller in the
 * decompile (message-table / function-pointer only), and the dispatcher
 * has no `case 438` — the client never parses a 438 reply.
 *
 * TS policy: parse and validate the exact wire grammar (flag u8, signed
 * 32-bit length, then exactly `len` raw bytes; primitives already reject
 * negative lengths and over-length reads; trailing bytes rejected) and
 * **deliberately reply nothing** — with no room broadcast target on this
 * server and a client that ignores 438, echoing or inventing an ACK
 * would fabricate behavior neither side consumes.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GG_ROOMBROADCAST_REQ(r: Reader, _connection: Connection): void {
  const flag = r.u8();
  const blob = r.raw(r.s32());
  void flag;
  void blob;
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 437`);
}
