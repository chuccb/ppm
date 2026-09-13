// =============================================================================
// AES-128 層 — 對應反編譯:
//   sub_403430 : key schedule (n16_0=16, n10=10 rounds → AES-128,
//                Rijndael 標準 T-table dword_B69208/B69608/B69A08/B68E08,
//                SBox byte_B66C08, 金鑰硬編碼於 .data 0xB69E88)
//   sub_403DE0 / sub_403650 : 單 block 加密 (標準 AES)
//   sub_404040 : 單 block 解密
//   sub_4042A0 : 多 block 加密 (n2=1/2 CBC 變體, 其他 ECB; 封包路徑 n2_4 未初始化=0 → ECB)
//   sub_404470 : 多 block 解密 (同上)
//
// 逆向確認其表與流程即標準 Rijndael → 直接用 .NET AES-128-ECB, NoPadding
// (長度已由 codec 依 sub_592FB0 上取 16 對齊)。
// 原版金鑰須自 exe .data 段 0xB69E88 抽 16 bytes (IDA .c 導出不含資料段)。
// =============================================================================
using System.Security.Cryptography;

namespace PaperMan.Protocol;

public sealed class PaperAes : IDisposable
{
    private readonly Aes _aes;

    public PaperAes(byte[] key16)
    {
        ArgumentNullException.ThrowIfNull(key16);
        if (key16.Length != 16) throw new ArgumentException("AES-128 key must be 16 bytes");
        _aes = Aes.Create();
        _aes.Key = key16;
        _aes.Mode = CipherMode.ECB;        // n2_4=0 → sub_4042A0 走 ECB 分支
        _aes.Padding = PaddingMode.None;   // codec 已對齊 16
    }

    public void EncryptEcb(byte[] data)
    {
        using var enc = _aes.CreateEncryptor();
        var outBuf = enc.TransformFinalBlock(data, 0, data.Length);
        outBuf.CopyTo(data, 0);
    }

    public void DecryptEcb(byte[] data)
    {
        using var dec = _aes.CreateDecryptor();
        var outBuf = dec.TransformFinalBlock(data, 0, data.Length);
        outBuf.CopyTo(data, 0);
    }

    public void Dispose() => _aes.Dispose();
}
