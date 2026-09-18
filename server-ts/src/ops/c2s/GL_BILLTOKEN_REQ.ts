/**
 * 706 asks for the billing charge token (the client "CHARGE" button
 * handler inside sub_460480 ctor-sites an empty
 * `Packet::possible_ctor_or_dtor_0(v372, 706)` and pipes it straight
 * into the sender `sub_555090` — only a 300 ms timeGetTime() debounce,
 * no field writers).
 *
 * Native 707 consumer `sub_46AD00` reads exactly `str token` into
 * `this + 521173`; the value only feeds the charge-banner UI flow —
 * there is no separate validation arm, so an empty token simply keeps
 * the charge flow inert.
 *
 * TS policy: this server has no billing model, so the honest frame is
 * the empty token string.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_BILLTOKEN_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 706`);
  connection.reply("GL_BILLTOKEN_ACK", "");
}
