/**
 * Opcode catalogue, loaded from the reverse-engineering source of truth at
 * `db/packets.tsv` (676 named opcodes, produced by the verifier tooling).
 *
 * Kept as a runtime load rather than a generated constant so the catalogue and
 * the server cannot drift apart: `verify_dispatcher_coverage.py` validates the
 * TSV against the binary, and this reads that same file.
 */

import { readFileSync } from "node:fs";

const TSV_PATH = new URL("../../../db/packets.tsv", import.meta.url);

function loadCatalogue(): ReadonlyMap<number, string> {
  const raw = readFileSync(TSV_PATH, "utf8");
  const map = new Map<number, string>();
  for (const line of raw.split("\n")) {
    if (!line) continue;
    const tab = line.indexOf("\t");
    if (tab < 0) continue;
    const opcode = Number.parseInt(line.slice(0, tab), 10);
    if (Number.isNaN(opcode)) continue;
    map.set(opcode, line.slice(tab + 1).trim());
  }
  return map;
}

export const OPCODE_NAMES: ReadonlyMap<number, string> = loadCatalogue();

export function opcodeName(opcode: number): string {
  return OPCODE_NAMES.get(opcode) ?? `UNKNOWN_${opcode}`;
}

/**
 * Opcodes this server references by name. Values are asserted against the
 * catalogue at startup so a typo or a catalogue change fails loudly.
 */
export const Op = {
  /** Ping keepalive. */
  GT_PING_REQ: 101,
  GT_PING_ACK: 102,

  /** Login. 694 is also the trigger that makes the client send 682. */
  GL_LOGIN_REQ: 682,
  GL_LOGIN_ACK: 681,
  GL_ACCOUNTCONNSUCC: 694,
} as const satisfies Record<string, number>;

export function assertCatalogue(): void {
  for (const [name, opcode] of Object.entries(Op)) {
    const actual = OPCODE_NAMES.get(opcode);
    if (actual === undefined) {
      throw new Error(`opcode ${opcode} (${name}) is absent from db/packets.tsv`);
    }
    if (actual !== name) {
      throw new Error(`opcode ${opcode} is ${actual} in the catalogue, not ${name}`);
    }
  }
}
