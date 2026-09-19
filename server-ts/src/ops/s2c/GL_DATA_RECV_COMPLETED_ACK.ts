/** 834 -> 835 is an empty completion acknowledgement (consumer `sub_5831D0`:
 * reads nothing, only forwards to `sub_522440`). */

import { Packet } from "../../packet.ts";

export default function GL_DATA_RECV_COMPLETED_ACK(op: number): Packet {
  return new Packet(op);
}
