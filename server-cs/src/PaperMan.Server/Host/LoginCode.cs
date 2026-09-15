// =============================================================================
// GL_LOGIN_ACK(681) result-code values consumed by the native login client.
// =============================================================================
namespace PaperMan.Server;

public enum LoginCode
{
    Unavailable = 0,        // generic native failure UI
    Ok = 1,
    BadCredentials = 2,
    Banned = 0xC8,          // 0xC8..0xD6 = various block/maintenance codes
}
