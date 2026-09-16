/**
 * The opcode catalogue, read from `db/packets.tsv` — 676 names verified
 * against the binary by `verify_dispatcher_coverage.py`.
 *
 * Loaded at runtime rather than copied into source so the two cannot drift.
 * There is deliberately no hand-written list of "opcodes we implement": the
 * filenames under `src/ops/` are that list.
 */

import { readFileSync } from "node:fs";

const TSV = new URL("../../db/packets.tsv", import.meta.url);

const opcodeByName = new Map<string, number>();
const nameByOpcode = new Map<number, string>();

for (const line of readFileSync(TSV, "utf8").split("\n")) {
  const tab = line.indexOf("\t");
  if (tab < 0) continue;
  const opcode = Number.parseInt(line.slice(0, tab), 10);
  if (Number.isNaN(opcode)) continue;
  const name = line.slice(tab + 1).trim();
  opcodeByName.set(name, opcode);
  nameByOpcode.set(opcode, name);
}

export const OPCODE_COUNT = nameByOpcode.size;

export function opcodeName(opcode: number): string {
  return nameByOpcode.get(opcode) ?? `UNKNOWN_${opcode}`;
}

/** Throws if the name is not in the catalogue — used to validate filenames. */
export function opcodeFor(name: string): number {
  const opcode = opcodeByName.get(name);
  if (opcode === undefined) {
    throw new Error(`"${name}" is not an opcode in db/packets.tsv`);
  }
  return opcode;
}
