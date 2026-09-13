// =============================================================================
// 線上 session 對照表 — 依暱稱找連線 (191 GR_CALLUSER 呼出、跨房操作)。
//
// client 的 191 REQ 只帶目標暱稱 (sub_56FD60: str nick), 因此 server 需要
// 一張 nick → Session 的反查表才能把 192 ACK 送到目標玩家。登入綁定暱稱
// 後註冊, 斷線時以「值相符才移除」(TryRemove(key, value)) 解註冊, 避免
// 舊連線誤刪新連線的同名條目。
// =============================================================================
using System.Collections.Concurrent;

namespace PaperMan.Server;

public sealed class SessionRegistry
{
    // 原版暱稱不分大小寫比對 (client 以 ASCII 名為主, 保險起見用 OrdinalIgnoreCase)。
    private readonly ConcurrentDictionary<string, Session> _byNick =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>登入綁定暱稱後註冊 (冪等: 同暱稱重複註冊以最新連線為準)。</summary>
    public void Register(Session session)
    {
        if (session.Nickname.Length > 0)
        {
            _byNick[session.Nickname] = session;
        }
    }

    /// <summary>斷線解註冊 — 僅當表內仍是同一 session 才移除。</summary>
    public void Unregister(Session session)
    {
        if (session.Nickname.Length > 0)
        {
            _byNick.TryRemove(KeyValuePair.Create(session.Nickname, session));
        }
    }

    /// <summary>依暱稱查線上 session (null = 離線)。</summary>
    public Session? Find(string nickname) =>
        _byNick.TryGetValue(nickname, out var session) ? session : null;
}
