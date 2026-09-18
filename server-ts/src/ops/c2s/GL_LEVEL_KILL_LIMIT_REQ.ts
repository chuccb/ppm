/**
 * 704 asks for the active level/kill-limit notice configuration
 * (builder sub_582570: empty payload).
 *
 * Native 705 consumer sub_55C9B0 reads exactly 12 bytes:
 * `s32 killLimit (n11_0), f32 expRate (flt_BEFEE8), s32 maxLevelLimit
 * (dword_BEFEE0)`. When killLimit == 0 the client takes the proven
 * silent arm with no limit-notice text; any nonzero value selects a
 * localized notice from a nonzero-arm table.
 *
 * TS policy: this server has no level/kill-limit restriction model,
 * so the honest frame is the all-zero (silent) configuration.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GL_LEVEL_KILL_LIMIT_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 704`);
  connection.reply("GL_LEVEL_KILL_LIMIT_ACK", 0, 0, 0);
}
