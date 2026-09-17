#!/usr/bin/env python3
"""
PaperMan wire-protocol Packet 參考實作 (Python 3)。

逐函數對應 PaperMan.exe.c 的反編譯結果:

  Packet(opcode)             <- Packet::possible_ctor_or_dtor_0 (0x591B40)
  header 佈局                <- sub_591DA0 / sub_591F00 / sub_591EE0 / sub_5923B0 / sub_591F70
  write_*/read_*             <- sub_592580 / sub_592500 及其 wrapper 家族
  seal()/unseal()            <- sub_5923D0 / sub_592420 (popcount checksum + XOR)
  to_bytes()/from_stream()   <- sub_555090 (WSASend size+8) / sub_555280

⚠️ 二次深挖後的重要更正 (詳見 docs/PACKETS.md §1.4):
  seal()/unseal() (sub_5923D0/sub_592420) 是 **死碼** — 全 exe 無任何
  呼叫者, 真實傳輸管線只有 LZ 壓縮 + AES-128-ECB 加密:
    送出 sub_593280: w3=原始大小 → (w0≥門檻時) LZ → 一律 AES
    接收 sub_5930C0: AES 解密 → (條件) LZ 解壓
  本模組保留 seal/unseal 僅作歷史參考, 與真客戶端互通請以
  server-ts/src/packet.ts 為準。
"""
from __future__ import annotations

import struct

MAX_PAYLOAD = 9592          # sub_591DA0: buffer 9600, header 8
HEADER_SIZE = 8

# AES-128 金鑰 (sub_403430 的 EUC-KR 字串「트렁크점령전머지」; 過測試向量:
#   ECB(key, 000102..0F) = D7F8930CFE8758AD7BF2FEF759EBB845 )
AES_KEY = bytes.fromhex("C6AEB7B7C5A9C1A1B7C9C0FCB8D3C1F6")

FLAG_COMPRESSED = 0x01      # this+19252 bit0 (sub_592D30)
FLAG_ENCRYPTED = 0x04       # this+19252 bit2 (sub_592FB0)


def _popcount_checksum(data: bytes) -> int:
    """sub_592220: 每 byte popcount 總和 (mod 65536)。"""
    return sum(bin(b).count('1') for b in data[:9592]) & 0xFFFF


class Packet:
    """一個 wire packet: 8-byte header + payload (小端, 無對齊)。"""

    def __init__(self, opcode: int = 0):
        self.opcode = opcode & 0xFFFF        # header word1 (sub_591EC0)
        self.checksum = 0                    # header word2 (sub_592390)
        self.orig_size = 0                   # header word3 (sub_591F90)
        self.buf = bytearray()               # payload
        self.rpos = 0                        # read cursor (this+19236)
        self.flags = 0                       # this+19252

    # ------------------------------------------------------------------ write
    def _w(self, data: bytes) -> 'Packet':
        """sub_592580"""
        if len(self.buf) + len(data) > MAX_PAYLOAD:
            raise OverflowError('payload > 9592')
        self.buf += data
        return self

    def write_u8(self, v):   return self._w(struct.pack('<B', v & 0xFF))          # sub_592920 / sub_592960
    def write_s8(self, v):   return self._w(struct.pack('<b', v))                 # sub_5928E0
    def write_u16(self, v):  return self._w(struct.pack('<H', v & 0xFFFF))        # sub_5929A0
    def write_s16(self, v):  return self._w(struct.pack('<h', v))                 # sub_5929E0
    def write_u32(self, v):  return self._w(struct.pack('<I', v & 0xFFFFFFFF))    # sub_592A60
    def write_s32(self, v):  return self._w(struct.pack('<i', v))                 # sub_592A20
    def write_u64(self, v):  return self._w(struct.pack('<Q', v))                 # sub_592AE0
    def write_f32(self, v):  return self._w(struct.pack('<f', v))                 # sub_592B20

    def write_str(self, s: str, enc='cp949') -> 'Packet':
        """sub_5926F0: ANSI 字串 + NUL, 無長度前綴 (韓服編碼 CP949)。"""
        return self._w(s.encode(enc, errors='replace') + b'\x00')

    def write_wstr(self, s: str) -> 'Packet':
        """sub_592770: UTF-16LE 字串 + 雙 NUL。"""
        return self._w(s.encode('utf-16-le') + b'\x00\x00')

    def write_raw(self, data: bytes) -> 'Packet':
        return self._w(bytes(data))

    def write_packet(self, inner: 'Packet') -> 'Packet':
        """sub_5927F0: u16 opcode + u32 size + payload。"""
        self.write_u16(inner.opcode)
        self.write_u32(len(inner.buf))
        return self._w(bytes(inner.buf))

    # ------------------------------------------------------------------- read
    def _r(self, n: int) -> bytes:
        """sub_592500 (越界回傳失敗 → 這裡拋例外)"""
        if self.rpos + n > len(self.buf):
            raise EOFError(f'read {n} at {self.rpos}/{len(self.buf)}')
        out = bytes(self.buf[self.rpos:self.rpos + n])
        self.rpos += n
        return out

    def read_u8(self):  return self._r(1)[0]                                      # sub_592940 / sub_592980
    def read_s8(self):  return struct.unpack('<b', self._r(1))[0]                 # sub_592900
    def read_u16(self): return struct.unpack('<H', self._r(2))[0]                 # sub_592A00
    def read_s16(self): return struct.unpack('<h', self._r(2))[0]                 # sub_5929C0
    def read_u32(self): return struct.unpack('<I', self._r(4))[0]                 # sub_592A80
    def read_s32(self): return struct.unpack('<i', self._r(4))[0]                 # sub_592A40
    def read_u64(self): return struct.unpack('<Q', self._r(8))[0]                 # sub_592B00
    def read_f32(self): return struct.unpack('<f', self._r(4))[0]                 # sub_592AC0

    def read_str(self, enc='cp949') -> str:
        """sub_592730: 讀到 NUL 為止。"""
        end = self.buf.find(b'\x00', self.rpos)
        if end < 0:
            raise EOFError('unterminated string')
        out = bytes(self.buf[self.rpos:end]).decode(enc, errors='replace')
        self.rpos = end + 1
        return out

    def read_wstr(self) -> str:
        i = self.rpos
        while i + 1 < len(self.buf) and self.buf[i:i + 2] != b'\x00\x00':
            i += 2
        out = bytes(self.buf[self.rpos:i]).decode('utf-16-le', errors='replace')
        self.rpos = i + 2
        return out

    def read_raw(self, n: int) -> bytes:
        return self._r(n)

    # ------------------------------------------------- checksum / obfuscation
    def seal(self) -> 'Packet':
        """sub_5923D0: 算 popcount checksum → XOR payload。傳送前呼叫一次。"""
        self.checksum = _popcount_checksum(self.buf)
        key = self.checksum & 0xFF
        self.buf = bytearray(b ^ key for b in self.buf)          # sub_592470
        return self

    def unseal(self) -> bool:
        """sub_592420: XOR 還原後驗 checksum。"""
        key = self.checksum & 0xFF
        self.buf = bytearray(b ^ key for b in self.buf)
        return _popcount_checksum(self.buf) == self.checksum

    # ------------------------------------------------------------------- wire
    def to_bytes(self) -> bytes:
        """sub_555090 傳送格式: [size u16][opcode u16][checksum u16][orig u16][payload]"""
        return struct.pack('<HHHH', len(self.buf), self.opcode,
                           self.checksum, self.orig_size) + bytes(self.buf)

    @classmethod
    def from_bytes(cls, data: bytes) -> 'Packet':
        if len(data) < HEADER_SIZE:
            raise EOFError('short header')
        size, opcode, checksum, orig = struct.unpack_from('<HHHH', data)
        if len(data) < HEADER_SIZE + size:
            raise EOFError('short payload')
        p = cls(opcode)
        p.checksum = checksum
        p.orig_size = orig
        p.buf = bytearray(data[HEADER_SIZE:HEADER_SIZE + size])
        return p

    @staticmethod
    def frame_length(header8: bytes) -> int:
        """recv 重組: 需要的總長度 = payload_size + 8 (sub_555280)。"""
        return struct.unpack_from('<H', header8)[0] + HEADER_SIZE

    def __repr__(self):
        return f'<Packet op={self.opcode} len={len(self.buf)} rpos={self.rpos}>'


# ---------------------------------------------------------------------- self-test
if __name__ == '__main__':
    # round-trip: GL_LOGIN_REQ(682) 的形狀 (sub_43E0F0 附近)
    p = Packet(682)
    p.write_str('alice').write_str('token123')
    p.write_u64(0x1122334455667788)
    p.write_u8(2)
    p.write_raw(b'\x00' * 24)
    p.seal()
    wire = p.to_bytes()

    q = Packet.from_bytes(wire)
    assert q.opcode == 682
    assert q.unseal(), 'checksum mismatch'
    assert q.read_str() == 'alice'
    assert q.read_str() == 'token123'
    assert q.read_u64() == 0x1122334455667788
    assert q.read_u8() == 2
    assert q.read_raw(24) == b'\x00' * 24
    assert Packet.frame_length(wire[:8]) == len(wire)

    # GL_MYITEM_ACK(200) 條目形狀 (sub_524B70)
    a = Packet(200)
    a.write_s8(1)            # success
    a.write_s32(0)           # start index
    a.write_s32(3)           # inv_slot
    a.write_s32(1001)        # item_id
    a.write_f32(1.0).write_f32(0.5)
    a.write_s32(30)          # period
    a.write_u16(100)         # durability
    a.write_s32(-1)          # sentinel
    a.seal()
    b = Packet.from_bytes(a.to_bytes())
    assert b.unseal()
    assert (b.read_s8(), b.read_s32(), b.read_s32(), b.read_s32()) == (1, 0, 3, 1001)

    # 語音 792 (GL_VOICEITEMSLOT_ACK) 86B 單角色塊 (sub_876B00 讀序)
    v792 = Packet(792)
    v792.write_u8(1)         # char_idx = 1 (nari/tina)
    v792.write_s16(10).write_s16(20)  # base1, base2
    for i in range(27):
        v792.write_s16(500 + i).write_u8(1 + (i % 9))
    assert len(v792.buf) == 86, f'expected 86B, got {len(v792.buf)}'
    v792.seal()
    v792_dec = Packet.from_bytes(v792.to_bytes())
    assert v792_dec.unseal()
    assert v792_dec.read_u8() == 1
    assert (v792_dec.read_s16(), v792_dec.read_s16()) == (10, 20)
    for i in range(27):
        assert (v792_dec.read_s16(), v792_dec.read_u8()) == (500 + i, 1 + (i % 9))
    assert v792_dec.rpos == len(v792_dec.buf)

    # 語音 794 (GI_VOICEITEMSLOT_ALL_ACK) 1291B 全 15 角色塊 (sub_876C90 讀序)
    v794 = Packet(794)
    v794.write_u8(15)        # count = 15
    for c in range(15):
        v794.write_u8(c)
        v794.write_s16(c).write_s16(c * 2)
        for i in range(27):
            v794.write_s16(0).write_u8(0)
    assert len(v794.buf) == 1 + 15 * 86 == 1291, f'expected 1291B, got {len(v794.buf)}'

    # 378/379 RadioMsg (sub_5593A0 / sub_74C500)
    r378 = Packet(378)
    r378.write_u8(0)         # team
    r378.write_u8(3)         # face (0..26)
    r378.write_u8(1)         # slot
    r378.write_u8(4)         # len
    r378.write_raw("Help".encode('utf-16-le'))
    assert len(r378.buf) == 4 + 8 == 12

    # sub_885D00 嵌入 114 成員負載尾塊 (85B: base1, base2, 27*(item, flag))
    v885 = Packet(114)
    v885.write_s16(1).write_s16(2)
    for i in range(27):
        v885.write_s16(i).write_u8(1)
    assert len(v885.buf) == 85

    # 系統/角色/商城/任務/投票 (686, 705, 371, 132, 720, 424, 454, 803, 877, 699, 901)
    p686 = Packet(686).write_s32(5)
    assert p686.read_s32() == 5

    p705 = Packet(705).write_s32(50).write_f32(1.0).write_s32(30)
    assert p705.read_s32() == 50 and p705.read_f32() == 1.0 and p705.read_s32() == 30

    p371 = Packet(371).write_u8(1).write_u8(2).write_str("127.0.0.1").write_s32(10000).write_u8(0)
    assert p371.read_u8() == 1 and p371.read_u8() == 2 and p371.read_str() == "127.0.0.1"

    p132 = Packet(132).write_u8(1).write_u8(3)
    assert p132.read_u8() == 1 and p132.read_u8() == 3

    p720 = Packet(720).write_s32(2).write_s32(1).write_s32(0).write_s32(30).write_u8(0)
    assert p720.read_s32() == 2 and p720.read_s32() == 1

    p424 = Packet(424).write_u8(1).write_str("101")
    assert p424.read_u8() == 1 and p424.read_str() == "101"

    p454 = Packet(454).write_u8(1).write_s32(10).write_s32(20)
    assert p454.read_u8() == 1 and p454.read_s32() == 10 and p454.read_s32() == 20

    p803 = Packet(803).write_u8(0).write_u8(0).write_s32(1000).write_s32(500).write_u8(1).write_s32(5).write_s32(0)
    assert p803.read_u8() == 0 and p803.read_u8() == 0

    p877 = Packet(877).write_u8(0).write_s32(0)
    assert p877.read_u8() == 0 and p877.read_s32() == 0

    p699 = Packet(699).write_u8(1).write_s32(100).write_s32(50)
    assert p699.read_u8() == 1 and p699.read_s32() == 100

    p901 = Packet(901).write_u8(1).write_s32(1001).write_s32(99)
    assert p901.read_u8() == 1 and p901.read_s32() == 1001

    print('packet.py self-test OK ✔')
