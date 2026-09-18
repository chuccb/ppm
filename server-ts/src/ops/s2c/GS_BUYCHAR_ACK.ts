/**
 * 310 -> 311 GS_BUYCHAR_ACK (consumer sub_5728A0, re-read line-level).
 *
 * Native wire: `u8 status`; status != 0 additionally carries the 6 x s32
 * purchase snapshot {slot, char_type, exp, cash, gp, dura}; afterwards
 * the client UNCONDITIONALLY reads `u8 v26Raw, s32 v33Raw, s32 v29Raw`
 * (the v26Raw wallet switch only fires under status != 0). With no
 * purchase model this server always emits status = 0 with zero trailing
 * fields — 10 bytes — which closes the dialog and mutates nothing.
 */

import { Packet } from "../../packet.ts";

export default function GS_BUYCHAR_ACK(op: number): Packet {
  return new Packet(op).u8(0).u8(0).s32(0).s32(0);
}
