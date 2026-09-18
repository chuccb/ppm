/**
 * 928 GR_AI_CONTINUE_START_REQ — PVE continue / revive (builder
 * sub_761DB0 @393520): a single `sub_592A20` (4-byte write accessor,
 * width re-verified) with the literal 0 — the client always sends
 * continue_count = 0. Wire = 4 zero bytes.
 *
 * 929 consumer sub_761E90 (full body): reads `u8 statusRaw`; only
 * statusRaw == 1 continues into the revival payload (u8, s32, string
 * via sub_592730, s32, s32); any other status is logged and ends the
 * frame immediately. With no PVE session/revival model the TS answer
 * is always `u8 status = 0` — the one-byte terminating arm (wire
 * "00"), not the old docs "s32 continue_count" shape.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_CONTINUE_START_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 4) {
    throw new RangeError(`928 expects exactly 4 bytes (s32 continue_count), got ${r.remaining}`);
  }
  const continueCount = r.s32();
  if (continueCount !== 0) {
    throw new RangeError(`928 native builder always sends continue_count 0, got ${continueCount}`);
  }
  connection.reply("GR_AI_CONTINUE_START_ACK");
}
