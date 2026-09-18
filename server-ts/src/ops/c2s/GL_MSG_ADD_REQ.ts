/**
 * 419 sends one authored message draft toward a recipient (builder
 * sub_559550): `{u8 raw0, str ownNick, s32 uidContextRaw, str toNick,
 * str body, str title, s16 iconRaw, u8 soundRaw}`.
 *
 * Native sender bounds: ownNick is the truncated login nick (<=24),
 * toNick must be 1..24 bytes, body must be 1..200 bytes; the server
 * side is the sole consumer of the remaining raw fields, which the
 * native reader never revisits (420 reads only str+u8+u8). The TS
 * parser mirrors sender bounds and rejects trailing bytes; identity
 * fields stay raw (no store join) consistent with the 834 context.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

const MSG_ADD_BODY_MAX_BYTES = 200; // native sub_559550 (n200 <= 200)
const MSG_ADD_NICK_MAX_BYTES = 24; // native sub_559550 (n24 <= 24)

export default function GL_MSG_ADD_REQ(r: Reader, connection: Connection): void {
  const raw0 = r.u8();
  const ownNick = r.str();
  const uidContextRaw = r.s32();
  const toNick = r.str();
  const body = r.str();
  const title = r.str();
  const iconRaw = r.u16();
  const soundRaw = r.u8();
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 419`);
  // raw0 / uidContextRaw / title / iconRaw / soundRaw stay parse-only (no proven domain).
  void raw0;
  void uidContextRaw;
  void title;
  void iconRaw;
  void soundRaw;
  if (ownNick.length > MSG_ADD_NICK_MAX_BYTES) {
    throw new RangeError("419 ownNick exceeds the native 24-byte sender bound");
  }
  if (toNick.length === 0 || toNick.length > MSG_ADD_NICK_MAX_BYTES) {
    throw new RangeError("419 toNick must be 1..24 bytes (native sender gate)");
  }
  if (body.length === 0 || body.length > MSG_ADD_BODY_MAX_BYTES) {
    throw new RangeError("419 body must be 1..200 bytes (native sender gate)");
  }
  // TS has no mailbox model; every recipient lookup is impossible, so the only
  // honest answer is the native reader's default (unknown) branch.
  connection.reply(
    "GL_MSG_ADD_ACK",
    toNick,
    6, // xRaw outside the proven switch arms -> generic error resource, no fabricated state
    0,
  );
}
