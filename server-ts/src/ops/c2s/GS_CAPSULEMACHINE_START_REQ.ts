/**
 * 900 GS_CAPSULEMACHINE_START_REQ — capsule machine start (カプセル/
 * ガチャ UI; builder sub_99CFA0, called by sub_99D0A0 line-level).
 *
 * Wire: exactly `{u8 raw0, s32 raw1}` — five bytes; the caller/UI mapping
 * of the pair (observed {3,1}, {1,10}, {1,1}, {2,1} across selector and
 * draw-count roles) is documented in docs/PACKETS.md §3.15d2, while the
 * widths are native Fact. The older "no builder recovered" note was wrong:
 * the pair is consumed structurally and stays UNRESOLVED by domain.
 *
 * The reply is the failure arm: sub_9A1A30 switches on the first u8 — a
 * nonzero value shows the native 0x4B4 banner and closes the gacha
 * compound (no wallet/award touches), while zero is the success arm that
 * would need real award rows and fresh wallet words.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GS_CAPSULEMACHINE_START_REQ(r: Reader, connection: Connection): void {
  const raw0 = r.u8();
  const raw1 = r.s32();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 900`);
  void raw0; // selector/draw-count pair: domain UNRESOLVED (docs/PACKETS.md §3.15d2)
  void raw1;
  connection.reply("GS_CAPSULEMACHINE_START_ACK");
}
