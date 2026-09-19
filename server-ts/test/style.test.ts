/**
 * Static style invariants for src/ops (STYLE.md 「op module 註解與程式碼的
 * 一致性 invariant」). These run on the source text, so they catch the
 * comment-versus-code drift classes that the runtime and the wire snapshots
 * cannot see. Every check is designed to have zero false positives on the
 * current tree; a failure here means the source really broke the rule.
 */

import { describe, expect, test } from "bun:test";
import { Glob } from "bun";

const opsRoot = new URL("../src/ops/", import.meta.url).pathname;

const tsv = await Bun.file(new URL("../../db/packets.tsv", import.meta.url)).text();
const officialNameByOpcode = new Map<number, string>();
for (const line of tsv.split("\n")) {
  const tab = line.indexOf("\t");
  if (tab < 0) continue;
  officialNameByOpcode.set(Number(line.slice(0, tab)), line.slice(tab + 1).trim());
}

const modules: { dir: "c2s" | "s2c"; name: string; source: string }[] = [];
for (const dir of ["c2s", "s2c"] as const) {
  for (const file of new Glob("*.ts").scanSync({ cwd: opsRoot + dir })) {
    const name = file.slice(0, -3);
    modules.push({
      dir,
      name,
      source: await Bun.file(`${opsRoot}${dir}/${file}`).text(),
    });
  }
}

/** All parameter names of every function declared in the module. */
function declaredParams(source: string): Set<string> {
  const params = new Set<string>();
  for (const match of source.matchAll(/function\s+\w+\s*\(([^)]*)\)/gs)) {
    for (const piece of match[1]!.split(",")) {
      const name = /([A-Za-z_$][\w$]*)\s*[:=)]?\s*$/.exec(piece.trim())?.[1];
      if (name && !/^(readonly|\.\.\.)/.test(piece.trim())) params.add(name.replace(/^\.\.\./, ""));
    }
  }
  return params;
}

describe("ops module filename is an official packet name", () => {
  for (const mod of modules) {
    test(`${mod.dir}/${mod.name}`, () => {
      expect([...officialNameByOpcode.values()]).toContain(mod.name);
    });
  }
});

describe("header pair comments quote real opcodes and names", () => {
  for (const mod of modules) {
    const pair = /(\d+)\s*->\s*(\d+)\s+([A-Z][A-Z0-9_]+)/.exec(mod.source);
    if (!pair) continue; // greeting / singleton modules carry no pair; fine
    test(`${mod.dir}/${mod.name}: ${pair[1]} -> ${pair[2]} ${pair[3]}`, () => {
      const [, from, to, name] = pair;
      expect(officialNameByOpcode.get(Number(from)), `opcode ${from} unknown`).toBeDefined();
      expect(officialNameByOpcode.get(Number(to)), `opcode ${to} unknown`).toBeDefined();
      expect(officialNameByOpcode.get(Number(to))).toBe(name);
      const own = mod.dir === "c2s" ? Number(from) : Number(to);
      expect(officialNameByOpcode.get(own)).toBe(mod.name);
    });
  }
});

describe("docs only reference parameters that exist", () => {
  for (const mod of modules) {
    const mentions = [...mod.source.matchAll(/`([A-Za-z_$][\w$]*)`\s*(?:argument|parameter|參數)/g)];
    if (mentions.length === 0) continue;
    const params = declaredParams(mod.source);
    test(`${mod.dir}/${mod.name}`, () => {
      for (const mention of mentions) {
        expect(params.has(mention[1]!), `doc references missing parameter ${mention[1]}`).toBe(true);
      }
    });
  }
});

describe("no orphan doc blocks", () => {
  // Orphan = a doc block separated from what it documents by a blank line.
  // Field/interface-member docs are attached (no blank line); the module doc
  // at file top is the one sanctioned block that may precede imports.
  for (const mod of modules) {
    test(`${mod.dir}/${mod.name}`, () => {
      const blocks = [...mod.source.matchAll(/\/\*\*[\s\S]*?\*\//g)];
      for (const [index, block] of blocks.entries()) {
        const after = mod.source.slice(block.index! + block[0].length);
        if (!/^\n\s*\n/.test(after)) continue; // attached to its declaration
        if (index === 0 && /^\n\s*\nimport\b/.test(after)) continue; // module doc
        throw new Error(`${mod.dir}/${mod.name}: doc block ${index + 1} is detached by a blank line`);
      }
    });
  }
});

describe("serialization-only vocabulary stays out of src/ops", () => {
  for (const mod of modules) {
    test(`${mod.dir}/${mod.name}`, () => {
      expect(mod.source.includes(".label(")).toBe(false);
      expect(mod.source.includes("strMax")).toBe(false);
      expect(mod.source.includes("\n\n\n")).toBe(false);
    });
  }
});
