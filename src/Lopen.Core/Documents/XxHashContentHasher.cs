using System.IO.Hashing;
using System.Text;

namespace Lopen.Core.Documents;

/// <summary>
/// Content hasher using XxHash128 for fast, non-cryptographic hashing.
/// Used for specification drift detection.
/// </summary>
internal sealed class XxHashContentHasher : IContentHasher
{
    /// <inheritdoc />
    public string ComputeHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = Encoding.UTF8.GetBytes(content);
        var hash = XxHash128.Hash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc />
    public bool HasDrifted(string content, string expectedHash)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(expectedHash);

        var currentHash = ComputeHash(content);
        return !currentHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
