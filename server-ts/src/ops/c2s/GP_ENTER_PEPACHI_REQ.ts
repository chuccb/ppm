/**
 * 698 — Pepachi entry request (ペーパチ).
 *
 * docs/PACKETS.md §3.15d2a: the native UI sends an empty payload. The wallet
 * gates that decide whether the machine is usable at all live client-side
 * (tools/verify_native_gates.py: level >= 10, present box < 200), so the only
 * honest reply without an original-service data source is the consumer-safe
 * "not entered" arm: status 0 keeps the client out of the reel UI and mutates
 * nothing.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GP_ENTER_PEPACHI_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 698`);
  connection.reply("GP_ENTER_PEPACHI_ACK");
}
