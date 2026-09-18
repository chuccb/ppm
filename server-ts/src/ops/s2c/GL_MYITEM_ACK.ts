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
  /** Native generic raw4 slot; current TS accepts a finite f32 projection only. */
  readonly f1: number;
  /** Native generic raw4 slot; current TS accepts a finite f32 projection only. */
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
    // A negative slot is the native end-of-page sentinel, not a record value.
    if (!Number.isSafeInteger(item.slot) || item.slot < 0 || item.slot > 0x7fff_ffff) {
      throw new RangeError("200 inventory slot must be a non-negative s32");
    }
    // sub_535020 rejects zero before consulting itemdata.pat. Membership is
    // intentionally not enforced here: the catalog proves client lookup only,
    // not this server's ownership or grant authority.
    if (!Number.isSafeInteger(item.itemId) || item.itemId <= 0 || item.itemId > 0x7fff_ffff) {
      throw new RangeError("200 item_id must be a positive s32");
    }
    // Native 200 reads both slots with sub_592AC0 (generic raw4), not the
    // typed sub_592B40 f32 reader. f32() here is only a byte-compatible
    // projection for the current number-based API.
    p.s32(item.slot)
      .s32(item.itemId)
      .label("200 raw4 f32 projection out of range").f32(item.f1)
      .label("200 raw4 f32 projection out of range").f32(item.f2)
      .s32(item.period)
      .u8(item.extra ?? 0) // native u8 after the period; domain unresolved
      .u16(item.durability); // native u16 current/max durability word
  }
  return p.s32(-1);
}
