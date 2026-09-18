/**
 * 900 — capsule machine start request (カプセル/ガチャ UI).
 *
 * The native request payload is unpinned (no builder recovered — PACKETS.md
 * §3.15d2 only records the consumer-safe reply shape), so nothing is read
 * and any request length is tolerated. The reply is the denial arm:
 * sub_9A1A30 only touches local wallet/reward state when the status byte is
 * zero.
 */

import type { Connection } from "../../connection.ts";
import type { Reader } from "../../packet.ts";

export default function GS_CAPSULEMACHINE_START_REQ(_r: Reader, connection: Connection): void {
  connection.reply("GS_CAPSULEMACHINE_START_ACK");
}
