/**
 * 918 GR_AI_GET_REWARD_ITEM_REQ — PVE reward draw (builder sub_761A70
 * @393375: ctor(918) -> sub_592920 one byte reward_idx -> send; wire =
 * exactly 1 byte).
 *
 * Native 919 consumer sub_761B20 (line-level re-read):
 *   u8 idx, u8 statusRaw
 *   statusRaw == 0 (success): s32 item_id, [item_id != 0: u8 slot
 *     type-checked via sub_67DF00], s32 count, u8 flag
 *   statusRaw != 0: u8 n255; ONLY the sentinel 0xFF terminates the arm
 *     cleanly (n255 != 0xFF continues reading s32, u8, s32)
 *
 * TS policy: no PVE session/loot model — always the failure arm with
 * the terminating sentinel: `u8 idx echo, u8 statusRaw = 1, u8 0xFF`
 * (3 bytes).
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GR_AI_GET_REWARD_ITEM_REQ(r: Reader, connection: Connection): void {
  if (r.remaining !== 1) {
    throw new RangeError(`918 expects exactly 1 byte (u8 reward_idx), got ${r.remaining}`);
  }
  const rewardIdx = r.u8();
  connection.reply("GR_AI_GET_REWARD_ITEM_ACK", rewardIdx);
}
