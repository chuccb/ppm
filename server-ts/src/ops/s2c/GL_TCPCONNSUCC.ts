/**
 * Channel-server greeting, and the trigger for the channel handshake.
 *
 * Empty payload. The client shows message 0xFF and immediately builds its UDP
 * start request (`sub_57CAE0` -> `sub_555C60`), so this is the channel-side
 * equivalent of the login server's account-connect packet:
 *
 *   login server    GL_ACCOUNTCONNSUCC -> GL_LOGIN_REQ    -> GL_LOGIN_ACK
 *   channel server  GL_TCPCONNSUCC     -> PM_UDPSTART_REQ -> PM_UDPSTART_ACK
 *
 * The client opens a *second* TCP connection for the channel after login,
 * using the host and port from the login reply's server list.
 * (docs/PACKETS.md §3.15d)
 */

import { Packet } from "../../packet.ts";

export default function (op: number): Packet {
  return new Packet(op, 0);
}
