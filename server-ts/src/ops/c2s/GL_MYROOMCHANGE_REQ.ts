/**
 * 487 GL_MYROOMCHANGE_REQ -> 488 GL_MYROOMCHANGE_ACK.
 *
 * Native builder `sub_57C450(char a1)`: ctor(487) then `sub_592920(a1)` — one
 * byte. The proven value space on the GM side (`/clby`, 1..5 clamped into
 * 0..4) and the my-room window carousel (`MYROOM_%d`, slot + 1) bound the
 * domain to five room states; the docs table records u8(0..4).
 *
 * Acceptance requires a signed-in lobby identity and a slot inside the proven
 * 0..4 domain. On success the chosen slot is projected into the lobby
 * session state (the client mirrors it into `*sub_417D00()` itself from the
 * ACK echo), and the ACK carries status 1 + the echoed slot. Any other slot
 * answers the single reject arm (status 0), which restores the client's
 * MyRoom UI — nothing else is fabricated (status 11's no-op arm is for the
 * client's own flow control, not for a server OK/NACK).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";
import { MyRoomChangeStatus } from "../s2c/GL_MYROOMCHANGE_ACK.ts";

/** Proven my-room slot domain: /clby clamps 1..5 into 0..4; MYROOM_%d = slot+1. */
export const MYROOM_SLOT_COUNT = 5;

export default function GL_MYROOMCHANGE_REQ(r: Reader, connection: Connection): void {
  const slot = r.u8();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 487`);

  const identity = connection.accountId === null
    ? null
    : connection.config.store.ensurePlayerIdentity(connection.accountId);

  if (identity === null || slot >= MYROOM_SLOT_COUNT) {
    connection.reply("GL_MYROOMCHANGE_ACK", MyRoomChangeStatus.Rejected, 0);
    return;
  }
  connection.reply("GL_MYROOMCHANGE_ACK", MyRoomChangeStatus.Success, slot);
}
