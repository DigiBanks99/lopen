using Lopen.Core.Git;

namespace Lopen.Cli.Tests.Fakes;

internal sealed class FakeRevertService : IRevertService
{
    public bool RevertCalled { get; private set; }
    public string? LastCommitSha { get; private set; }

    public RevertResult Result { get; set; } = new(true, "abc123", "Reverted successfully.");
    public Exception? RevertException { get; set; }

    public Task<RevertResult> RevertToCommitAsync(string commitSha, CancellationToken cancellationToken = default)
    {
        RevertCalled = true;
        LastCommitSha = commitSha;
        if (RevertException is not null)
            throw RevertException;
        return Task.FromResult(Result);
    }
}
