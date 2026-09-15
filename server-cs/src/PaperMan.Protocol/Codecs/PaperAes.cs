// =============================================================================
// AES-128 層 — 對應反編譯:
//   sub_403430 : key schedule — 金鑰是 EUC-KR 字串字面量「트렁크점령전머지」
//                (「後車廂佔領戰merge」) = C6 AE B7 B7 C5 A9 C1 A1
//                                          B7 C9 C0 FC B8 D3 C1 F6
//                大端組字 w[0..3] 後做標準 RotWord/SubWord/rcon 展開 (10 輪);
//   sub_403DE0 : 單 block 加密
//   sub_4042A0 / sub_404470 : 多 block driver (n2=2 代表 128-bit CFB 模式, IV=0)
//
// 驗證:
//   CFB(key, IV=0, 000102..0F) 逐位驗證通過真實客戶端封包
// =============================================================================
using System.Security.Cryptography;

namespace PaperMan.Protocol;

public sealed class PaperAes : IDisposable
{
    /// <summary>
    /// 客戶端硬編碼金鑰 — sub_403430 的 EUC-KR 字串「트렁크점령전머지」。
    /// </summary>
    public static ReadOnlySpan<byte> DefaultKey =>
    [
        0xC6, 0xAE, 0xB7, 0xB7, 0xC5, 0xA9, 0xC1, 0xA1,
        0xB7, 0xC9, 0xC0, 0xFC, 0xB8, 0xD3, 0xC1, 0xF6,
    ];

    private readonly Aes _aes;

    public PaperAes(ReadOnlySpan<byte> key16)
    {
        if (key16.Length != 16)
        {
            throw new ArgumentException("AES-128 key must be 16 bytes", nameof(key16));
        }

        _aes = Aes.Create();
        _aes.Key = key16.ToArray();
    }

    /// <summary>
    /// sub_4042A0 (n2_4=2): 128-bit CFB 加密 (IV 初始為全零)。
    /// 每 16 位元組 block 以 AES-ECB 加密目前 IV, 與明文 XOR 產生密文,
    /// 並將該密文作為下一輪 IV。
    /// </summary>
    public void EncryptCfb(Span<byte> data)
    {
        Span<byte> iv = stackalloc byte[16];
        iv.Clear();
        Span<byte> encIv = stackalloc byte[16];

        for (int i = 0; i < data.Length; i += 16)
        {
            iv.CopyTo(encIv);
            _aes.EncryptEcb(encIv, encIv, PaddingMode.None);
            var block = data.Slice(i, 16);
            for (int k = 0; k < 16; k++)
            {
                block[k] ^= encIv[k];
            }
            block.CopyTo(iv);
        }
    }

    /// <summary>
    /// sub_404470 (n2_4=2): 128-bit CFB 解密 (IV 初始為全零)。
    /// 每 16 位元組 block 以 AES-ECB 加密目前 IV, 與密文 XOR 還原明文,
    /// 並將原始密文作為下一輪 IV。
    /// </summary>
    public void DecryptCfb(Span<byte> data)
    {
        Span<byte> iv = stackalloc byte[16];
        iv.Clear();
        Span<byte> encIv = stackalloc byte[16];
        Span<byte> nextIv = stackalloc byte[16];

        for (int i = 0; i < data.Length; i += 16)
        {
            iv.CopyTo(encIv);
            _aes.EncryptEcb(encIv, encIv, PaddingMode.None);
            var block = data.Slice(i, 16);
            block.CopyTo(nextIv);
            for (int k = 0; k < 16; k++)
            {
                block[k] ^= encIv[k];
            }
            nextIv.CopyTo(iv);
        }
    }

    public void Dispose() => _aes.Dispose();
}
