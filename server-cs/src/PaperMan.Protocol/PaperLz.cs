// =============================================================================
// 自製 LZ — 逐行對應反編譯:
//   壓縮   sub_591600: flag byte 每 8 個 token 一個 (bit 由低到高, n128 128→256 wrap)
//          hash = src[2] ^ (src[1]-13) ^ (src[0]+13) → 256 槽 hash 表存絕對位址低 16 bit
//          match 距離 = (pos - table[h]) & 0x3FF (≤1023), 長度 3..65
//          token: 2 bytes = [(len*4-12) | dist>>8] [dist&0xFF]
//          若輸出將超過 count-17 → 放棄, 原樣拷貝 (回傳 count → caller 視為失敗)
//   解壓   sub_591900: flag bit=1 → 讀 2-byte token; len=(b0>>2)+3,
//          dist=bswap16(u16)&0x3FF, 逐 byte 拷貝 (允許重疊)
// =============================================================================
namespace PaperMan.Protocol;

public static class PaperLz
{
    /// <summary>sub_591600。壓不小/放棄時回傳原資料 (caller 比大小決定是否採用)。</summary>
    public static byte[] Compress(ReadOnlySpan<byte> src)
    {
        int count = src.Length;
        if (count < 66) return src.ToArray();               // 原版尾端至少留 66 bytes literal 空間

        var dst = new byte[count];                          // 壓縮結果必須 < count 才有意義
        var table = new ushort[256];
        int sp = 0, dp = 0, flagPos = 0;
        int mask = 128;

        while (sp < count)
        {
            mask <<= 1;
            if (mask == 256)
            {
                if (dp >= count - 17)                       // dst_1 >= &dst[count-17] → 放棄
                    return src.ToArray();
                mask = 1;
                flagPos = dp;
                dst[dp++] = 0;
            }

            if (sp <= count - 66)
            {
                int h = (byte)(src[sp + 2] ^ (byte)(src[sp + 1] - 13) ^ (byte)(src[sp] + 13));
                int dist = (sp - table[h]) & 0x3FF;
                table[h] = (ushort)sp;
                int mp = sp - dist;

                if (mp >= 0 && mp != sp &&
                    src[sp] == src[mp] && src[sp + 1] == src[mp + 1] && src[sp + 2] == src[mp + 2])
                {
                    dst[flagPos] |= (byte)mask;             // *dst_2 |= n128
                    int len = 3;
                    while (len < 66 && src[sp + len] == src[mp + len]) len++;
                    if (dp + 2 > dst.Length) return src.ToArray();
                    dst[dp++] = (byte)((dist >> 8) | (len * 4 - 12));  // BYTE1(dist) | (4*i-12)
                    dst[dp++] = (byte)dist;
                    sp += len;
                    continue;
                }
            }

            if (dp >= dst.Length) return src.ToArray();
            dst[dp++] = src[sp++];                          // literal
        }
        return dst[..dp];
    }

    /// <summary>sub_591900。origSize = header word3。</summary>
    public static byte[] Decompress(ReadOnlySpan<byte> src, int origSize)
    {
        var dst = new byte[origSize];
        int sp = 0, dp = 0;
        int mask = 128;
        byte flags = 0;

        while (sp < src.Length && dp < origSize)
        {
            mask <<= 1;
            if (mask == 256)
            {
                mask = 1;
                flags = src[sp++];
                if (sp > src.Length) break;
            }

            if ((flags & mask) != 0)
            {
                if (sp + 2 > src.Length) break;
                int b0 = src[sp], b1 = src[sp + 1];
                sp += 2;
                int len = (b0 >> 2) + 3;                    // (*v9 >> 2) + 3
                int dist = (((b0 << 8) | b1) & 0x3FF);      // bswap16 & 0x3FF
                int from = dp - dist;
                if (from < 0) throw new InvalidDataException("lz backref out of range");
                for (int i = 0; i < len && dp < origSize; i++)
                    dst[dp++] = dst[from++];                // 逐 byte, 允許重疊
            }
            else
            {
                dst[dp++] = src[sp++];
            }
        }
        return dst[..dp];
    }
}
