/**
 * 107 -> 108 game-room list (consumer `sub_568CE0`; the full per-field
 * line audit lives in docs/PACKETS.md §3.9).
 *
 * Native grammar (mode != 3, the ordinary list this server can emit):
 *
 *   u8  mode            top-level mode; 3 = tournament tree (delegate
 *                       `sub_580A80`, intentionally unsupported here);
 *                       2 = ordinary list plus per-room team textures
 *   u8  count           room record count read as its own u8
 *   repeat count:
 *     u8   roomNo       native range gate: records with no >= 0xD2 (210)
 *                       are silently dropped by the client
 *     s8raw state       >= 0 -> default title from client string table
 *                       msgtableres 0x135+state (preset phrases);
 *                       < 0 -> followed by `str title` (custom name).
 *                       The state byte itself is read via the byte reader
 *                       and tested as a signed int.
 *     then, in both branches, the 12 documented room fields — native
 *     accessor order proven in `sub_568CE0`:
 *       u8       cur_players   room +105 ("cur/max" first number)
 *       s8       has_pass      room +106
 *       u8       max_players   room +129 (client later recomputes it
 *                              from the slot-mask popcount)
 *       u16      max_slot_mask room +110; bits 0..max-1 = 1;
 *                              sub_53FB10 derives max+per-slot flags
 *       u8       game_mode     factory `sub_53FBB0` id (GAME_MODES)
 *       s8       room_type_a   room +108 ROOMTYPE bit
 *       s8       mode_param_a  -> mode object +12
 *       s8       room_type_b   room +109 ROOMTYPE bit
 *       s8       double_damage room +128
 *       u8       map           room +130 (124/125 = special map ids)
 *       u8       mode_param_b  -> mode object +4 (sub_74F450)
 *       u8       no_skill_bg   room +185 NOSKILLBG
 *     if outer mode == 2, TWO team custom-texture groups follow
 *     (CCustomTexture registration; tex name caps 75/87 bytes):
 *       s32 teamId, s32 texCrc (native reads both as `sub_592A40`,
 *       NOT the u32 reader), str texName, u8 tailByte
 *
 * Mode 3 (tournament) keeps its own grammar in PACKETS §3.9 and is not
 * emittable by this server: the client routes it to a different reader
 * that does not consume the ordinary `count`, so writing it without a
 * bounded tournament model would corrupt the stream.
 *
 * `rooms === []` therefore emits exactly `u8(0), u8(0)` — "there are
 * no rooms" is a complete semantic answer, not padding.
 */

import { Packet } from "../../packet.ts";

/** Native `sub_53FBB0` factory ids; 16 is accepted as-is, others become null. */
export const GAME_MODES = {
  TeamMatch: 0,
  IndividualSurvival: 1,
  DefuseBomb: 2,
  TeamSurvival: 3,
  Steal: 4,
  Practice: 5,
  Tutorial: 6,
  ChattingRoom: 7,
  PulpAndRoll: 8,
  GunShooting: 9,
  Occupy: 10,
  AIMulti: 11,
  TeamSoccer: 12,
  OccupyRenewal: 13,
  WeaponTest: 15,
} as const;

/** One team custom-texture group (only present when the outer mode is 2). */
export interface RoomTeamTexture {
  readonly teamId: number;
  /** Native reads this word via the s32 accessor; it is the texture CRC bytes, not a count. */
  readonly texCrc: number;
  /** Group A is capped at 75 bytes, group B at 87 bytes by the client buffers. */
  readonly texName: string;
  readonly tailByte: number;
}

export interface RoomInfo {
  readonly roomNo: number;
  /** >= 0: preset-title index (msgtableres 0x135+state); < 0: use `title`. */
  readonly state: number;
  /** Custom room title; required when `state < 0`, ignored otherwise. */
  readonly title?: string;
  readonly curPlayers: number;
  readonly hasPass: number;
  readonly maxPlayers: number;
  readonly maxSlotMask: number;
  readonly gameMode: number;
  readonly roomTypeA: number;
  readonly modeParamA: number;
  readonly roomTypeB: number;
  readonly doubleDamage: number;
  readonly map: number;
  readonly modeParamB: number;
  readonly noSkillBg: number;
  /** Exactly two groups; required when the outer mode is 2. */
  readonly teamTextures?: readonly [RoomTeamTexture, RoomTeamTexture];
}

function writeRoom(p: Packet, room: RoomInfo, mode: number): Packet {
  p.u8(room.roomNo).s8(room.state);
  if (room.state < 0) {
    if (room.title === undefined) {
      throw new RangeError("custom-title rooms (state < 0) need `title`");
    }
    p.str(room.title);
  }
  p.u8(room.curPlayers)
    .s8(room.hasPass)
    .u8(room.maxPlayers)
    .u16(room.maxSlotMask)
    .u8(room.gameMode)
    .s8(room.roomTypeA)
    .s8(room.modeParamA)
    .s8(room.roomTypeB)
    .s8(room.doubleDamage)
    .u8(room.map)
    .u8(room.modeParamB)
    .u8(room.noSkillBg);
  if (mode === 2) {
    if (!room.teamTextures) {
      throw new RangeError("mode 2 room records need `teamTextures` (two groups)");
    }
    for (const t of room.teamTextures) {
      p.s32(t.teamId).s32(t.texCrc).str(t.texName).u8(t.tailByte);
    }
  }
  return p;
}

/** `mode`: top-level mode byte; only 0 (plain) and 2 (with team textures) are supported. */
export default function GL_GAMEROOMINFO_ACK(
  op: number,
  rooms: readonly RoomInfo[] = [],
  mode = 0,
): Packet {
  if (mode !== 0 && mode !== 2) {
    throw new RangeError("only outer modes 0 (plain) and 2 (team textures) are supported");
  }
  const p = new Packet(op).u8(mode).u8(rooms.length);
  for (const room of rooms) writeRoom(p, room, mode);
  return p;
}
