#!/usr/bin/env python3
"""
PaperMan wire-protocol Packet 參考實作 (Python 3)。

逐函數對應 PaperMan.exe.c 的反編譯結果:

  Packet(opcode)             <- Packet::possible_ctor_or_dtor_0 (0x591B40)
  header 佈局                <- sub_591DA0 / sub_591F00 / sub_591EE0 / sub_5923B0 / sub_591F70
  write_*/read_*             <- sub_592580 / sub_592500 及其 wrapper 家族
  seal()/unseal()            <- sub_5923D0 / sub_592420 (popcount checksum + XOR)
  to_bytes()/from_stream()   <- sub_555090 (WSASend size+8) / sub_555280

注意: 正式客戶端在 sub_593280 對大包多做一層 LZ 壓縮 (sub_591600) 與
16-byte 區塊加密 (sub_4042A0, 表在 dword_23199F8)。本模組先實作
輕量層 (checksum+XOR)，壓縮/加密層留 hook — 兩者只在 payload
超過門檻或連線協商後才啟用。
"""
from __future__ import annotations

import struct

MAX_PAYLOAD = 9592          # sub_591DA0: buffer 9600, header 8
HEADER_SIZE = 8

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

    def write_u8(self, v):   return self._w(struct.pack('<B', v & 0xFF))          # sub_592920
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

    def read_u8(self):  return self._r(1)[0]                                      # sub_592940
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
    print('packet.py self-test OK ✔')
