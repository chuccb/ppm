/**
 * 220 GI_CHANGEWP_REQ — player weapon-change broadcast (native writer
 * `sub_573340` @L167433, per-slot writer `sub_524A50` @L128752; all
 * line level re-read 2026-09-19).
 *
 * Wire shape (writer side):
 *   - empty arm: bare opcode frame, NO payload — the client has no
 *     `sub_525680`-qualified slot at the moment and just refreshed the
 *     local UI (`sub_4BD120`/`sub_522440`).
 *   - otherwise: `u8 count` (count == number of qualifying slots, max
 *     native guard is 4 — see `sub_524880`'s cap), then per slot:
 *       u8   slot        (n3; native guard `< 4` — `>=224` would
 *                          corrupt the roster via `22*n3` arithmetic)
 *       u16 raw0        (roster +72103; native sign-extends an s8
 *                          roster byte → wire raw2; TS reads it as s16)
 *       [slot != 3]     3 x u16 extras  (roster +72104/+72105/+72106;
 *                          the +72105 byte doubles as the ext-active
 *                          presence byte for the variable-tail dialect)
 *       [raw0 != 0]     8 x raw4 slot dwords (roster +36054, the
 *                          11-dword-per-slot table)
 *
 * A roster byte governs a second native writer (`sub_524540`): when
 * `+72105 <= 0` it emits only `u8 slot`. The consumer-proven full
 * dialect above is what this server accepts (see below for the Level-1
 * agreement that fixed this choice).
 *
 * * Level-1 wire agreement (self "律師" review, 2026-09-19):
 *   ① Writer (sub_573340): per-slot tail = u8 + u16 + (3x u16 if
 *      slot!=3) + (8x raw4 if raw0!=0) — PROVEN line level.
 *   ② Consumer (sub_5735F0 -> sub_524880): per-slot tail = u8 + u16 +
 *      (3x u16 if slot!=3) + (8x raw4 if v8[0]!=0) and v8[0] IS raw0 —
 *      PROVEN IDENTICAL to ① (byte-for-byte).
 *   ③ Therefore the native count-based protocol is byte-aligned and a
 *      server parser following ②'s rule survives BOTH writer modes.
 *   ④ Open residual: the u8-only arm of sub_524540 would look like
 *      "count of the NEXT slot" to a strict ② parser when embedded in
 *      a multi-slot frame. That sub is only reachable through
 *      `sub_524FD0` whose opcode is UNVERIFIED (never linked to 220 in
 *      any dispatcher table) — keeping the strict shape is the agreed
 *      Level-1 stance; revisit only if the unverified path is proven
 *      to carry 220 traffic.
 *
 * The ACK (221) consumer (`sub_57C270`, full line level): `u8 count`,
 * count x {s32 actionId -> p_action[i] via `sub_47BAA0`, applied by
 * `sub_526CA0`}, trailing s8 snapshot gate (`sub_524010/sub_524660/
 * sub_527550/sub_527D00`), then `sub_4BD240` rebroadcast and, when any
 * apply matched a custom-model slot, a fresh 220 `u8(4)` rebroadcast
 * cycle. None of that routes back here — this server consumes the
 * proven writer grammar read-only and stays silent.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GI_CHANGEWP_REQ(r: Reader, connection: Connection): void {
  // Empty arm: a bare 220 frame carries no count at all.
  if (r.remaining === 0) {
    void connection;
    return;
  }
  const count = r.u8();
  for (let i = 0; i < count; i++) {
    const slot = r.u8();
    if (slot >= 4) throw new RangeError(`220 slot ${slot} outside native <4 guard`);
    const raw0 = r.s16(); // native s8 roster byte sign-extended into a raw2
    if (slot !== 3) {
      void r.s16(); void r.s16(); void r.s16(); // +72104/+72105/+72106 extras
    }
    if (raw0 !== 0) {
      for (let k = 0; k < 8; k++) void r.s32(); // 8 x raw4 slot dwords
    }
  }
  if (r.remaining !== 0) throw new RangeError(`${r.remaining} trailing bytes in 220`);
  void connection; // read-only: no supported flow replies to 220 yet
}
