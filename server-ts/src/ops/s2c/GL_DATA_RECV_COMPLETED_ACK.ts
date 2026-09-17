/** 834 -> 835 is an empty completion acknowledgement. */

import { Packet } from "../../packet.ts";

export default function GL_DATA_RECV_COMPLETED_ACK(op: number): Packet {
  return new Packet(op);
}
