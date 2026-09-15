// =============================================================================
// Per-room, per-match battle-object state.
//
// The OCC lifecycle is process-local: ground-object/capture progress has no
// recovered durable representation. Its methods are synchronized independently
// from Room membership so confirmed state transitions stay atomic.
// =============================================================================
using System.Diagnostics.CodeAnalysis;

namespace PaperMan.Server;

/// <summary>OCC 的單一據點生命週期。數字值不在封包上直接傳送。</summary>
public enum OccupyPointPhase : byte
{
    Idle,
    Capturing,
    Captured,
}

/// <summary>
/// 由 OCC handler 取出的不可變快照。
///
/// <para><see cref="CaptureParticipantCount"/> 對應 903/907 第四個 byte。client 會把它
/// 寫入據點控制器的 <c>+24</c>，只接受 1..2 的活動值（<c>sub_768210</c>），且
/// 在大於 1 時加速占領計時（<c>sub_76A4B0</c>）。故其可驗證語意是同點參與者數，
/// 而非隊伍、slot 或保留欄。這個私服尚未有位置聚合器，start actor 是唯一已知的
/// 參與者，因此以狀態導出的 1 初始化。</para>
/// </summary>
public sealed record OccupyPointSnapshot(
    byte PointId,
    byte ActorSlot,
    int ActorUserId,
    byte CaptureParticipantCount,
    OccupyPointPhase Phase);

/// <summary>
/// 房內短生命週期的戰場狀態。它不寫入 SQLite：地面武器與占領進度只在一局
/// 對戰有效，且必須在同一房間內原子更新。
/// </summary>
public sealed class RoomBattleState
{
    private readonly Lock _gate = new();
    private readonly Dictionary<byte, OccupyPointSnapshot> _occupyPoints = [];
    private bool _matchActive;

    /// <summary>在與據點轉換相同的 lock 內開始新局並清空上一局狀態。</summary>
    public void BeginMatch()
    {
        lock (_gate)
        {
            _occupyPoints.Clear();
            _matchActive = true;
        }
    }

    /// <summary>在與據點轉換相同的 lock 內結束對局，禁止任何後續戰場事件。</summary>
    public void EndMatch()
    {
        lock (_gate)
        {
            _matchActive = false;
            _occupyPoints.Clear();
        }
    }

    /// <summary>供沒有可變狀態的 battle handler 檢查局是否仍有效。</summary>
    public bool IsMatchActive
    {
        get
        {
            lock (_gate)
            {
                return _matchActive;
            }
        }
    }

    /// <summary>
    /// OCC UI 對 pointId-1 只處理三個據點（<c>sub_771490</c> / <c>sub_7713C0</c>
    /// 的 index &lt; 3 防護），所以拒絕 0 與大於 3 的 wire id。<paramref name="stateChanged"/>
    /// 僅在真正建立 claim 時為 true；相同 actor 的 TCP 重送可安全只回給原請求者。
    /// </summary>
    public bool TryStartOccupy(
        byte pointId,
        byte actorSlot,
        int actorUserId,
        [NotNullWhen(true)] out OccupyPointSnapshot? snapshot,
        out bool stateChanged)
    {
        lock (_gate)
        {
            if (!_matchActive || !IsValidPoint(pointId))
            {
                snapshot = null;
                stateChanged = false;
                return false;
            }

            if (_occupyPoints.TryGetValue(pointId, out var current))
            {
                // TCP 重送同一個 start 時保持冪等；另一人或已完成的據點不能搶寫。
                if (current.Phase != OccupyPointPhase.Capturing
                    || current.ActorSlot != actorSlot
                    || current.ActorUserId != actorUserId)
                {
                    snapshot = null;
                    stateChanged = false;
                    return false;
                }

                snapshot = current;
                stateChanged = false;
                return true;
            }

            snapshot = new(
                pointId,
                actorSlot,
                actorUserId,
                CaptureParticipantCount: 1,
                Phase: OccupyPointPhase.Capturing);
            _occupyPoints.Add(pointId, snapshot);
            stateChanged = true;
            return true;
        }
    }

    /// <summary>僅啟動該據點的同一玩家可以送 OCC_SUCC。</summary>
    public bool TryCompleteOccupy(
        byte pointId,
        byte actorSlot,
        int actorUserId,
        [NotNullWhen(true)] out OccupyPointSnapshot? snapshot,
        out bool stateChanged)
    {
        lock (_gate)
        {
            if (!_matchActive
                || !_occupyPoints.TryGetValue(pointId, out var current)
                || current.ActorSlot != actorSlot
                || current.ActorUserId != actorUserId)
            {
                snapshot = null;
                stateChanged = false;
                return false;
            }

            if (current.Phase == OccupyPointPhase.Captured)
            {
                snapshot = current;                          // 成功 ACK 僅須補給重送者
                stateChanged = false;
                return true;
            }

            if (current.Phase != OccupyPointPhase.Capturing)
            {
                snapshot = null;
                stateChanged = false;
                return false;
            }

            snapshot = current with { Phase = OccupyPointPhase.Captured };
            _occupyPoints[pointId] = snapshot;
            stateChanged = true;
            return true;
        }
    }

    /// <summary>僅啟動者可以送 OCC_FAIL；成功或他人的事件不可清除狀態。</summary>
    public bool TryFailOccupy(byte pointId, byte actorSlot, int actorUserId, [NotNullWhen(true)] out OccupyPointSnapshot? snapshot)
    {
        lock (_gate)
        {
            if (!_matchActive
                || !_occupyPoints.TryGetValue(pointId, out var current)
                || current.Phase != OccupyPointPhase.Capturing
                || current.ActorSlot != actorSlot
                || current.ActorUserId != actorUserId)
            {
                snapshot = null;
                return false;
            }

            _occupyPoints.Remove(pointId);
            snapshot = current with { Phase = OccupyPointPhase.Idle };
            return true;
        }
    }

    /// <summary>
    /// 離開房間的玩家不可以繼續鎖住仍在 capture 中的據點。已完成據點保留到
    /// <see cref="EndMatch"/>，因為它已是房內可見的結果。
    /// </summary>
    public void AbandonCapturesBy(byte actorSlot, long actorUserId)
    {
        if (actorUserId is < int.MinValue or > int.MaxValue)
        {
            return;
        }

        int wireUserId = (int)actorUserId;
        lock (_gate)
        {
            var abandoned = _occupyPoints
                .Where(pair => pair.Value.Phase == OccupyPointPhase.Capturing
                    && pair.Value.ActorSlot == actorSlot
                    && pair.Value.ActorUserId == wireUserId)
                .Select(pair => pair.Key)
                .ToArray();
            foreach (byte pointId in abandoned)
            {
                _occupyPoints.Remove(pointId);
            }
        }
    }

    private static bool IsValidPoint(byte pointId) => pointId is >= 1 and <= 3;
}

/// <summary>單一房間的即時狀態 (記憶體為主, DB rooms 表為快照)。</summary>
