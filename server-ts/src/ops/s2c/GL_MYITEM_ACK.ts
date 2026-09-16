/**
 * 199 -> 200 inventory page.
 *
 * The current Store has no inventory/catalog model. An empty successful page is
 * nevertheless a complete, client-consumable response: start index 0 followed
 * immediately by the documented negative-slot sentinel.
 */

import { Packet } from "../../packet.ts";

export interface InvItem {
  readonly slot: number;
  readonly itemId: number;
  /** Native first f32; client-side appearance-skill roll projection. */
  readonly f1: number;
  /** Native second f32; client-side rarity/grade projection. */
  readonly f2: number;
  readonly period: number;
  /** Native u8 after the period; its domain is unresolved. */
  readonly extra?: number;
  /** Native u16 current/max durability word. */
  readonly durability: number;
}

export default function GL_MYITEM_ACK(op: number, items: readonly InvItem[] = []): Packet {
  if (items.length > 100) throw new RangeError("200 page cannot contain more than 100 items");

  const p = new Packet(op).u8(1).s32(0);
  for (const item of items) {
    p.s32(item.slot)
      .s32(item.itemId)
      .f32(item.f1)
      .f32(item.f2)
      .s32(item.period)
      .u8(item.extra ?? 0)
      .u16(item.durability);
  }
  return p.s32(-1);
}
