/**
 * 310 GS_BUYCHAR_REQ — shop character purchase (builder sub_572790
 * @167049: ctor(310) then six sub_592A20 writes; accessors proven at
 * their bodies: sub_592A20 = 4 bytes each, so the wire is exactly
 * 6 x s32 = 24 bytes: `s32 char_type, 5 x s32 items`).
 *
 * Native 311 consumer sub_5728A0: `u8 status`; status != 0 reads the
 * 6 x s32 purchase snapshot (slot, char_type, exp, cash, gp, dura),
 * and unconditionally afterwards reads `u8 v26Raw, s32 v33Raw,
 * s32 v29Raw` (the v26Raw wallet switch only fires under status != 0).
 *
 * TS policy: no purchase/catalogue model exists, so the request is
 * parsed structurally and answered status = 0 with zero trailing
 * fields (wire 10 bytes) — the client closes the purchase dialog via
 * sub_4694B0(byte_D70C14, status) with no wallet mutation.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GS_BUYCHAR_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 24) {
    throw new RangeError(`310 expects exactly 6 x s32 (24 bytes), got ${r.remaining}`);
  }
  for (let i = 0; i < 6; i++) r.s32();
  connection.reply("GS_BUYCHAR_ACK");
}
