// =============================================================================
// Login-to-channel handoff.
//
// 681 is received on the account TCP socket, while 143 is sent on a fresh
// channel TCP socket after 693. Native 143 carries an ANSI String[24] plus the
// n100/ext_count values echoed from 681; it is not a cryptographic bearer
// token. The available export proves the string's size and reuse but does not
// prove the writer that gives it account-name or nickname semantics. Therefore
// it is deliberately *not* an authentication lookup key here.
//
// A claim is bound to a recent successful login, its transport source IP, and a
// short lifetime. When more than one live claim has the same source-IP and
// native echo values, there is no proven way to disambiguate them, so claiming
// fails safely instead of guessing based on an unproven String[24] meaning.
// =============================================================================
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace PaperMan.Server;

/// <summary>Authenticated data transferred from a successful 681 to one 143 claim.</summary>
public sealed record ChannelAdmission(
    long AccountId,
    long UserId,
    string LoginName,
    string Nickname,
    int BillingUiMode,
    int FeatureExtensionCount,
    string RemoteIp,
    DateTimeOffset ExpiresAt);

/// <summary>Concurrency-safe, single-use claims for the account-TCP → channel-TCP transition.</summary>
public sealed class ChannelAdmissionRegistry
{
    private readonly ConcurrentDictionary<Guid, ChannelAdmission> _admissions = new();
    private readonly ConcurrentDictionary<long, Guid> _latestByAccount = new();
    private readonly Lock _issueGate = new();

    /// <summary>
    /// Issues a replacement claim for this account. The native 143 identity is
    /// intentionally absent: using it would infer account/nickname semantics
    /// not established by the native writer in this checkout.
    /// </summary>
    public void Issue(
        long accountId,
        long userId,
        string loginName,
        string nickname,
        int billingUiMode,
        int featureExtensionCount,
        string remoteIp,
        TimeSpan lifetime,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(accountId);
        ArgumentException.ThrowIfNullOrWhiteSpace(loginName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIp);
        ArgumentOutOfRangeException.ThrowIfNegative(featureExtensionCount);
        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Admission lifetime must be positive.");
        }

        var admissionId = Guid.NewGuid();
        var admission = new ChannelAdmission(
            accountId,
            userId,
            loginName,
            nickname,
            billingUiMode,
            featureExtensionCount,
            remoteIp,
            now + lifetime);

        // Prevent concurrent login sockets for one account from creating two
        // independently claimable admissions.
        lock (_issueGate)
        {
            PruneExpired(now);
            if (_latestByAccount.TryGetValue(accountId, out var priorId))
            {
                Remove(priorId);
            }

            _admissions[admissionId] = admission;
            _latestByAccount[accountId] = admissionId;
        }
    }

    /// <summary>
    /// Atomically consumes the sole matching admission. A bad source or echoed
    /// 681 value does not consume a valid admission, so a malformed packet
    /// cannot be used as a denial-of-service primitive. An ambiguous NAT case
    /// is rejected: native 143 contains no identity whose semantics we can
    /// prove sufficiently to choose between those accounts.
    /// </summary>
    public bool TryClaim(
        int billingUiMode,
        int featureExtensionCount,
        string remoteIp,
        DateTimeOffset now,
        [NotNullWhen(true)] out ChannelAdmission? admission)
    {
        // Claim selection and consumption must share the Issue lock. Otherwise
        // a concurrent login could add a second indistinguishable same-NAT
        // claim after the ambiguity scan but before the first claim is removed.
        admission = null;
        lock (_issueGate)
        {
            ChannelAdmission? candidate = null;
            Guid candidateId = Guid.Empty;

            foreach (var (admissionId, possible) in _admissions)
            {
                if (possible.ExpiresAt <= now)
                {
                    Remove(admissionId);
                    continue;
                }

                if (!StringComparer.OrdinalIgnoreCase.Equals(possible.RemoteIp, remoteIp)
                    || possible.BillingUiMode != billingUiMode
                    || possible.FeatureExtensionCount != featureExtensionCount)
                {
                    continue;
                }

                // Two matches cannot be resolved honestly from the native 143
                // contract. Refuse rather than treating String[24] as a guessed
                // account-name/nickname alias.
                if (candidate is not null)
                {
                    return false;
                }

                candidate = possible;
                candidateId = admissionId;
            }

            if (candidate is null
                || !_admissions.TryRemove(KeyValuePair.Create(candidateId, candidate)))
            {
                return false;
            }

            _latestByAccount.TryRemove(KeyValuePair.Create(candidate.AccountId, candidateId));
            admission = candidate;
            return true;
        }
    }

    private void PruneExpired(DateTimeOffset now)
    {
        foreach (var (admissionId, admission) in _admissions)
        {
            if (admission.ExpiresAt <= now)
            {
                Remove(admissionId);
            }
        }
    }

    private void Remove(Guid admissionId)
    {
        if (_admissions.TryRemove(admissionId, out var admission))
        {
            _latestByAccount.TryRemove(KeyValuePair.Create(admission.AccountId, admissionId));
        }
    }
}
