/**
 * 702 -> 703: empty Pepachi catalog.
 *
 * The consumer reads `{s32 normalCount, s32 rareCount, (n+r)×s32 itemId}`;
 * negative counts trip its "Invalid DB Pepachi Data" diagnostic, so the safe
 * catalog is exactly two zero counts with no item records.
 */

import { Packet } from "../../packet.ts";

export default function GP_PEPACHI_LIST_ACK(op: number): Packet {
  return new Packet(op).s32(0).s32(0);
}
