/**
 * 105 -> 106 user waiting list (consumer `sub_56A250`; wire audit in
 * docs/PACKETS.md §3.8).
 *
 * Native grammar:
 *
 *   raw2  gate        client only tests zero/nonzero; the nonzero
 *                     domain itself is unresolved, so 1 is the honest
 *                     value whenever records follow
 *   if gate != 0:
 *     u8  flags       bit0 clears progress and sets a mode-specific
 *                     native list-state flag; bit2 clears that flag;
 *                     the exact UI policy is unresolved (keep 0)
 *     u8  count
 *     repeat count:
 *       raw4 userKey   client user/profile table key; only a positive
 *                      key can carry a custom emblem
 *       str  nick
 *       s32  exp       ⚠ experience, NOT a status byte — `sub_588560`
 *                      maps it through `sub_403360` to a Class index
 *                      that `CUIWaiterList` renders as the Class icon
 *       if userKey > 0:
 *         raw4 texKey  registered into `EMBLEM` via the custom-texture
 *                      registry after a positive table lookup
 *         str  texName (native buffer cap 64)
 *
 * `users === []` emits exactly `u16(0)` — the honest "no users"
 * answer, identical to the previous constant.
 */

import { Packet } from "../../packet.ts";

export interface UserListRecord {
  readonly userKey: number;
  readonly nick: string;
  /** Experience value; the client renders the derived Class icon. */
  readonly exp: number;
  /** Custom-emblem texture key; required when `userKey > 0`. */
  readonly texKey?: number;
  /** Custom-emblem texture name (native buffer cap 64); required when `userKey > 0`. */
  readonly texName?: string;
}

export default function GL_USERLIST_ACK(
  op: number,
  users: readonly UserListRecord[] = [],
  flags = 0,
): Packet {
  if (users.length === 0) return new Packet(op).u16(0);
  const p = new Packet(op).u16(1).u8(flags).u8(users.length);
  for (const u of users) {
    p.s32(u.userKey).str(u.nick).s32(u.exp);  // userKey is a signed raw4 the client tests with > 0
    if (u.userKey > 0) {
      if (u.texKey === undefined || u.texName === undefined) {
        throw new RangeError("userKey > 0 needs `texKey` and `texName` (native reads them unconditionally)");
      }
      p.s32(u.texKey).str(u.texName);
    }
  }
  return p;
}
