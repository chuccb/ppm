/**
 * 702 -> 703: empty Pepachi catalog (consumer `CLobbyShop::sub_46AD00`,
 * case 703).
 *
 * Line-level in PaperMan.exe.c: the case reads
 * `{s32 countA, s32 countB, (countA+countB)×raw4 entry}` directly on the
 * packet (`sub_592A40` ×2, then a `sub_592AC0` raw4 loop into a fixed
 * array); negative counts trip the "Invalid DB Pepachi Data"
 * diagnostic. The s32 itemId below is the raw4's documented projection.
 * The safe catalog is exactly two zero counts with no item records.
 */

import { Packet } from "../../packet.ts";

export default function GP_PEPACHI_LIST_ACK(op: number): Packet {
  return new Packet(op).s32(0).s32(0);
}
