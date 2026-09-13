#!/usr/bin/env python3
"""
PaperMan 資源檔解密/解包工具 (十四輪逆向, 演算法逐行對應 PaperMan.exe.c)。

支援兩種容器:

1. pmFile 子檔解密 (cfg\\*.pat 從 pmClient.dat 讀出後的內容):
     decrypt : sub_7117D0  (per-byte 滾動 XOR + ROL)
     encrypt : sub_711720  (互逆, 50 組隨機 roundtrip 驗證)

2. data.pat 容器:
     [u32 size(混淆: ROL32(x,9)^0x975E)] [XOR/ROL8 混淆 body]
     → zlib 解壓 → 尾 4B = ~CRC32 校驗
     (載入器 @225227; zlib "1.2.3" 字串直接出現在 sub_A318D0)

用法:
    python3 pmfile.py pat-decrypt <in> <out>     # cfg\\*.pat 解密
    python3 pmfile.py pat-encrypt <in> <out>
    python3 pmfile.py datapat <data.pat> <out>   # data.pat 解容器
"""
from __future__ import annotations

import sys
import zlib

M32 = 0xFFFFFFFF


def _rol8(b: int, n: int) -> int:
    n &= 7
    return ((b << n) | (b >> (8 - n))) & 0xFF


def _ror8(b: int, n: int) -> int:
    n &= 7
    return ((b >> n) | (b << (8 - n))) & 0xFF


def _next_state(state: int, i: int) -> int:
    """sub_7117D0 的 keystream 遞推 (常數 0xFA5387AD/0x0F3A94AA/0x48945B4A/0x1A68DCCF)。"""
    return (
        ((state ^ 0xFA5387AD) & 0x0F3A94AA)
        ^ (((i | state) + 0x48945B4A) & M32)
        ^ 0x1A68DCCF
    ) & M32


def pmfile_decrypt(data: bytes) -> bytes:
    """sub_7117D0: out[i] = ROL8(in[i], i) ^ state; state 初值 = size。"""
    buf = bytearray(data)
    state = len(buf) & M32
    for i in range(len(buf) - 1, -1, -1):
        buf[i] = _rol8(buf[i], i) ^ (state & 0xFF)
        state = _next_state(state, i)
    return bytes(buf)


def pmfile_encrypt(data: bytes) -> bytes:
    """sub_711720: 先 XOR 再 ROR (與 decrypt 互逆)。"""
    buf = bytearray(data)
    state = len(buf) & M32
    for i in range(len(buf) - 1, -1, -1):
        buf[i] ^= state & 0xFF
        state = _next_state(state, i)
        buf[i] = _ror8(buf[i], i)
    return bytes(buf)


def datapat_unpack(raw: bytes) -> bytes:
    """data.pat 容器 → 解出的內容 (含 CRC 校驗)。"""
    if len(raw) < 8:
        raise ValueError("data.pat 太短")

    word = int.from_bytes(raw[:4], "little")
    size = (((word << 9) | (word >> 23)) & M32) ^ 0x975E   # ROL32(w,9)^0x975E

    body = bytearray(raw[4:])
    v1 = len(body)
    for k in range(len(body)):
        body[k] = (v1 ^ _rol8(body[k], 3)) & 0xFF
        v1 -= 1

    plain = zlib.decompress(bytes(body), bufsize=size)
    if len(plain) != size:
        raise ValueError(f"解壓大小不符: {len(plain)} != {size}")

    payload, crc_tail = plain[:-4], plain[-4:]
    expect = (~zlib.crc32(payload)) & M32
    if int.from_bytes(crc_tail, "little") != expect:
        raise ValueError("CRC 校驗失敗")

    return payload


def _selftest() -> None:
    import os

    for n in range(1, 40):
        pt = os.urandom(n)
        assert pmfile_decrypt(pmfile_encrypt(pt)) == pt, n
    print("pmfile encrypt/decrypt roundtrip OK ✔")


def main(argv: list[str]) -> int:
    if len(argv) == 2 and argv[1] == "selftest":
        _selftest()
        return 0
    if len(argv) != 4:
        print(__doc__)
        return 1

    cmd, src, dst = argv[1], argv[2], argv[3]
    data = open(src, "rb").read()

    match cmd:
        case "pat-decrypt":
            out = pmfile_decrypt(data)
        case "pat-encrypt":
            out = pmfile_encrypt(data)
        case "datapat":
            out = datapat_unpack(data)
        case _:
            print(__doc__)
            return 1

    open(dst, "wb").write(out)
    print(f"{cmd}: {src} ({len(data)}B) -> {dst} ({len(out)}B)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
