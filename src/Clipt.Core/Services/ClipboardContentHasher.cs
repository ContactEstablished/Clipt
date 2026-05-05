using System.Security.Cryptography;
using System.Text;

namespace Clipt.Core.Services;

public static class ClipboardContentHasher
{
    public static string ComputeHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }
}
