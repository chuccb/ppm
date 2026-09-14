// =============================================================================
// Account-listener handlers.
//
// 694 is emitted once by Program when a Login-role TCP session opens; native
// CLobbyLogin::sub_43E500 receives it and immediately calls sub_43DF00 to emit
// 682.  Therefore this file must never send 694 after 682/681.
// =============================================================================
using PaperMan.Protocol;

namespace PaperMan.Server;

public static class AuthHandlers
{
    public static void Register(Registrar add)
    {
        add(Opcode.GT_PING_REQ, PingReply);
        add(Opcode.GL_LOGIN_REQ, Login);
    }

    // 101 = client response to server-originated 102 (sub_58D6F0). It is not a
    // request/reply pair: replying with 102 here would create a ping loop.
    private static ValueTask PingReply(Session session, Packet packet, ServerContext context)
    {
        session.LastPongAt = DateTimeOffset.UtcNow;
        return ValueTask.CompletedTask;
    }

    /// <summary>Validates and handles the fixed-size tail of GL_LOGIN_REQ(682).</summary>
    private static async ValueTask Login(Session session, Packet packet, ServerContext context)
    {
        if (!TryReadValidLoginRequest(packet, out var request, out var dataRevision))
        {
            // A native client consumes result 2 as the account/password failure
            // message and reads no success-only tail.
            await session.SendAsync(LoginWire.CreateAcknowledgement(
                new LoginAcknowledgement((int)LoginCode.BadCredentials)));
            return;
        }

        string safeAccount = ToLogSafe(request.AccountName);
        Console.WriteLine(
            $"[s{session.Id}] GL_LOGIN_REQ: account='{safeAccount}' ({request.AccountName.Length} chars), " +
            $"dataRevision=0x{dataRevision:X8}, fingerprintSource={request.FingerprintSource}, fingerprint=24B");

        var login = context.Db.Login(
            request.AccountName,
            request.PasswordOrToken,
            dataRevision,
            request.FingerprintSource,
            request.Fingerprint,
            session.RemoteIp);
        Console.WriteLine(
            $"[s{session.Id}] Db.Login: result={login.Result}, userId={login.UserId}, " +
            $"hasNickname={!string.IsNullOrEmpty(login.Nickname)}");

        LoginAcknowledgement acknowledgement;
        if (login.Result is LoginCode.Ok
            && login.AccountId > 0
            && login.UserId is >= int.MinValue and <= int.MaxValue)
        {
            acknowledgement = context.Config.CreateLoginAcknowledgement((int)login.UserId);
        }
        else
        {
            // GL_LOGIN_ACK carries user_no as s32. Do not truncate a database
            // id that cannot be represented by the original client or bind an
            // invalid account id after acknowledging success.
            var result = login.Result is LoginCode.Ok ? LoginCode.Unavailable : login.Result;
            acknowledgement = new LoginAcknowledgement((int)result);
        }

        var ack = LoginWire.CreateAcknowledgement(acknowledgement);
        Console.WriteLine($"[s{session.Id}] Sending GL_LOGIN_ACK: payload={ack.Length}B, result={acknowledgement.ResultCode}");
        await session.SendAsync(ack);

        if (acknowledgement.ResultCode == (int)LoginCode.Ok)
        {
            session.BindAuthentication(login.AccountId, login.UserId, request.AccountName, login.Nickname);
            context.ChannelAdmissions.Issue(
                login.AccountId,
                login.UserId,
                request.AccountName,
                login.Nickname,
                context.Config.BillingUiMode,
                context.Config.LoginFeatureExtension is null ? 0 : 1,
                session.RemoteIp,
                context.Config.ChannelAdmissionLifetime,
                DateTimeOffset.UtcNow);
        }
    }

    private static bool TryReadValidLoginRequest(Packet packet, out LoginRequest request, out uint dataRevision)
    {
        request = default!;
        dataRevision = 0;

        try
        {
            request = LoginWire.ReadRequest(packet);
            if (string.IsNullOrEmpty(request.AccountName)
                || string.IsNullOrEmpty(request.PasswordOrToken)
                || !IsNativeLoginName(request.AccountName)
                || (byte)request.FingerprintSource > (byte)LoginFingerprintSource.StorageSerial
                || !HasNativeFingerprintShape(request)
                || !LoginWire.TryDecodeDataRevision(request.ObfuscatedDataRevision, out dataRevision))
            {
                return false;
            }

            // The native export does not prove which writer gives 143's
            // String[24] its account/nickname meaning. Do not create a false
            // server-side identity invariant from that opaque client state.
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or EndOfStreamException or InvalidDataException)
        {
            Console.WriteLine($"[login] rejected malformed GL_LOGIN_REQ: {exception.Message}");
            return false;
        }
    }

    /// <summary>Mirrors sub_43DD60's account-edit validation before the native client builds 682.</summary>
    private static bool IsNativeLoginName(string accountName) =>
        accountName.All(static character =>
            character is >= '0' and <= '9'
            or >= 'A' and <= 'Z'
            or >= 'a' and <= 'z'
            or '@'
            or '＠');

    /// <summary>Checks the exact zero-filled raw24 shapes emitted by sub_43DF00.</summary>
    private static bool HasNativeFingerprintShape(LoginRequest request)
    {
        ReadOnlySpan<byte> fingerprint = request.Fingerprint;
        if (fingerprint.Length != 24)
        {
            return false;
        }

        return request.FingerprintSource switch
        {
            LoginFingerprintSource.Unavailable => IsAllZero(fingerprint),
            LoginFingerprintSource.AdapterMacAddress => IsAllZero(fingerprint[6..]),
            // sub_9A8790 writes at most indices 0..22 into a block cleared
            // immediately before the source probe; byte 23 remains NUL.
            LoginFingerprintSource.StorageSerial => fingerprint[23] == 0,
            _ => false,
        };
    }

    private static bool IsAllZero(ReadOnlySpan<byte> values)
    {
        foreach (byte value in values)
        {
            if (value != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static string ToLogSafe(string value) => string.Create(value.Length, value, static (destination, source) =>
    {
        for (int index = 0; index < source.Length; index++)
        {
            destination[index] = char.IsControl(source[index]) ? '.' : source[index];
        }
    });
}
