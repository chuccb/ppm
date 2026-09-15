#!/usr/bin/env python3
"""從 db/packets.tsv 重新產生 src/PaperMan.Protocol/Generated/Opcode.cs。

用法: python3 server-cs/tools/gen_opcodes.py
(packets.tsv 來源: PaperMan.exe.c 的 sub_9D2050 封包名稱註冊表)
"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TSV = ROOT / "db" / "packets.tsv"
OUT = ROOT / "server-cs" / "src" / "PaperMan.Protocol" / "Generated" / "Opcode.cs"

rows = []
for line in TSV.read_text(encoding="utf-8").splitlines():
    line = line.strip()
    if not line or line.startswith("#") or line.startswith("opcode\t"):
        continue
    op, name = line.split("\t")[:2]
    rows.append((int(op), name))
rows.sort()

lines = [
    "// =============================================================================",
    "// 由 tools/gen_opcodes.py 從 db/packets.tsv 產生 — 請勿手改。",
    "// 名稱與編號出自 PaperMan.exe.c 封包註冊表 sub_9D2050。",
    "// =============================================================================",
    "namespace PaperMan.Protocol;",
    "",
    "public enum Opcode : ushort",
    "{",
]
seen = set()
for op, name in rows:
    if name in seen:  # 同名不同號: 以編號區分
        name = f"{name}_{op}"
    seen.add(name)
    lines.append(f"    {name} = {op},")
lines.append("}")
OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"wrote {OUT} ({len(rows)} opcodes)")
