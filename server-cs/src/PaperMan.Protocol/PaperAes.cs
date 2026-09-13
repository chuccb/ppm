// =============================================================================
// AES-128 層 — 對應反編譯 (新導出 Hex-Rays 9.4 已完整展開金鑰來源):
//   sub_403430 : key schedule — 金鑰是 EUC-KR 字串字面量「트렁크점령전머지」
//                (「後車廂佔領戰merge」) = C6 AE B7 B7 C5 A9 C1 A1
//                                          B7 C9 C0 FC B8 D3 C1 F6
//                大端組字 w[0..3] 後做標準 RotWord/SubWord/rcon 展開
//                (S-box byte_B66C08, rcon unk_B69E08, n10=10 輪 → AES-128);
//                加密輪金鑰存 dword_23199F8, 解密(逆序+InvMixColumns 預處理)
//                存 dword_2319D18
//   sub_403DE0 / sub_403650 : 單 block 加密 (T-table dword_B68E08..B69A08)
//   sub_4042A0 / sub_404470 : 多 block driver — n2==1 → CBC 加密,
//                n2==2 → CBC 解密, 其他 → ECB;
//                封包路徑 sub_592FB0/sub_593110 傳 n2_4 (未初始化全域=0) → ECB
//
// 三重驗證: (1) 金鑰排程逐位對照 FIPS-197; (2) 純 Python AES 過 C.1 test
// vector; (3) PaperMan key 測試向量 (見 SelfTest):
//   ECB(key, 000102..0F)          = D7F8930CFE8758AD7BF2FEF759EBB845
//   ECB(key, "PaperMan-Packet!")  = 8B8ABD9B2B743448188ED7E554BD4AA2
//
// 表與流程即標準 Rijndael → 用 .NET 的一次性 EncryptEcb/DecryptEcb
// (硬體 AES-NI), NoPadding — 長度已由 codec 依 sub_592FB0 上取 16 對齊。
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
            throw new ArgumentException("AES-128 key must be 16 bytes", nameof(key16));
        _aes = Aes.Create();
        _aes.Key = key16.ToArray();
    }

    /// <summary>sub_4042A0 (n2_4=0)。data 長度必須為 16 的倍數, 原地加密。</summary>
    public void EncryptEcb(Span<byte> data) =>
        _aes.EncryptEcb(data, data, PaddingMode.None);

    /// <summary>sub_404470 (n2_4=0)。data 長度必須為 16 的倍數, 原地解密。</summary>
    public void DecryptEcb(Span<byte> data) =>
        _aes.DecryptEcb(data, data, PaddingMode.None);

    public void Dispose() => _aes.Dispose();
}
