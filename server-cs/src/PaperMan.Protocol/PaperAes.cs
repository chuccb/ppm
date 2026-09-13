// =============================================================================
// AES-128 層 — 對應反編譯:
//   sub_403430 : key schedule (n16_0=16, n10=10 rounds → AES-128,
//                Rijndael 標準 T-table dword_B69208/B69608/B69A08/B68E08,
//                SBox byte_B66C08, 金鑰硬編碼於 .data 0xB69E88)
//   sub_403DE0 / sub_403650 : 單 block 加密   sub_404040 : 單 block 解密
//   sub_4042A0 / sub_404470 : 多 block driver (n2=1/2 CBC 變體, 其他 ECB;
//                封包路徑 n2_4 未初始化 = 0 → ECB)
//
// 表與流程即標準 Rijndael → 用 .NET 的一次性 EncryptEcb/DecryptEcb
// (硬體 AES-NI), NoPadding — 長度已由 codec 依 sub_592FB0 上取 16 對齊。
// =============================================================================
using System.Security.Cryptography;

namespace PaperMan.Protocol;

public sealed class PaperAes : IDisposable
{
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
