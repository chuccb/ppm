/**
 * 199 -> 200 inventory page.
 *
 * The current Store has no inventory/catalog model. An empty successful page is
 * nevertheless a complete, client-consumable response: start index 0 followed
 * immediately by the documented negative-slot sentinel.
 */

import { Packet } from "../../packet.ts";

export interface Item {
  readonly slot: number;
  readonly itemId: number;
  readonly firstValue: number;
  readonly secondValue: number;
  readonly period: number;
  readonly durability: number;
}

export default function GL_MYITEM_ACK(op: number, start = 0, items: readonly Item[] = []): Packet {
  if (!Number.isInteger(start) || start < 0) throw new RangeError("200 start must be non-negative");
  if (items.length > 100) throw new RangeError("200 page cannot contain more than 100 items");

  const p = new Packet(op).u8(1).s32(start);
  for (const item of items) {
    p.s32(item.slot)
      .s32(item.itemId)
      .f32(item.firstValue)
      .f32(item.secondValue)
      .s32(item.period)
      .u8(0)
      .u16(item.durability);
  }
  return p.s32(-1);
}
