/**
 * 485 GL_GET_GAMEROOM_PROGRESSTIME_REQ — query a room's elapsed match
 * time (builder sub_56AD60 @163685: one sub_592920 byte room_no -> send;
 * wire = exactly 1 byte).
 *
 * Native 486 consumer sub_56AE30 arms (line-level re-read):
 *  - n3 == 2: reads 4 more bytes;
 *  - n3 in {0, 1, 4}: reads a mode-dependent row (9 or 11 bytes), then
 *    an n11-gated 1-2 byte tail plus 2 final bytes, and finally calls
 *    the lobby progress renderer;
 *  - ANY OTHER n3: the code normalises to n3 = 3 and SKIPS the renderer
 *    unconditionally -> the proven fully silent arm.
 *
 * TS policy: no room-session model, so the answer is n3 = 3 (wire
 * 1 byte "03") — the native unknown-arm: nothing further consumed or
 * rendered.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_GET_GAMEROOM_PROGRESSTIME_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`485 expects exactly 1 byte (u8 room_no), got ${r.remaining}`);
  }
  r.u8(); // room_no
  connection.reply("GL_GET_GAMEROOM_PROGRESSTIME_ACK");
}
