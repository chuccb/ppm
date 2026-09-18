/**
 * 702 — Pepachi catalog list request.
 *
 * docs/PACKETS.md §3.15d2a: no request payload. The ACK is the machine's
 * published item list; empty counts build an empty local list client-side
 * and never grant a reward.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GP_PEPACHI_LIST_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 702`);
  connection.reply("GP_PEPACHI_LIST_ACK");
}
