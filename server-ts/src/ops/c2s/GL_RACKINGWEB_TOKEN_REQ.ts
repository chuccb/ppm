/**
 * 787 asks for the ranking-web auth token (builder sub_581E40: ctor for
 * opcode 787 followed directly by the send call with no accessor writers
 * in between, so the wire payload is empty).
 *
 * Native 788 consumers (three subscriber sites):
 *  - sub_407360 is the only payload reader: `u8 hasToken`, and when
 *    nonzero a `str token` (accessor sub_592730) that gets
 *    strncpy(...) capped at 0x10 bytes into the global ranking-web
 *    token buffer byte_EDDE04 — a token of 16+ chars is discarded.
 *  - CLobbyMainRoom::sub_44BEA0 simply switches the lobby to the
 *    ranking tab (sub_446780(this, 2)) without reading the payload.
 *  - CLobbyTournamentMainRoom::sub_489AD0 touches tournament UI
 *    resources, again without reading the payload.
 *
 * TS policy: this server has no ranking-web integration, so the honest
 * frame is `u8 hasToken = 0` — token absent, buffer untouched.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_RACKINGWEB_TOKEN_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 787`);
  connection.reply("GL_RACKINGWEB_TOKEN_ACK");
}
