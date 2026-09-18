/**
 * 878 -> 879 honor-quest completion (consumer sub_91CAA0).
 *
 * Wire: `u8 err`; the success arm additionally carries `str title` and
 * a raw blob of a client-fixed length. A nonzero err only logs an error
 * line on the client and is the proven inert arm. With no honor-quest
 * model this server always emits err = 1 (wire "01").
 */

import { Packet } from "../../packet.ts";

export default function GQ_QUEST_USER_COMPLETE_HONOR_ACK(op: number): Packet {
  return new Packet(op).u8(1);
}
