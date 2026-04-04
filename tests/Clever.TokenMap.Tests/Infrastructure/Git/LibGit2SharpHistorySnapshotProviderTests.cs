using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Infrastructure.Analysis.Git;
using LibGit2Sharp;

namespace Clever.TokenMap.Tests.Infrastructure.Git;

public sealed class LibGit2SharpHistorySnapshotProviderTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-git-{Guid.NewGuid():N}");

    public LibGit2SharpHistorySnapshotProviderTests()
    {
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public async Task TryCreateAsync_CollectsTextHistoryAcrossCommits()
    {
        InitializeRepository();
        CommitTextFile("src/Program.cs", "line-1\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-20));
        CommitTextFile("src/Program.cs", "line-1\nline-2\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-10));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(_rootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = Assert.Single(snapshot.FileHistoryByAnalysisRelativePath);
        Assert.Equal("src/Program.cs", history.Key);
        Assert.Equal(2, history.Value.ChurnLines90d);
        Assert.Equal(2, history.Value.TouchCount90d);
        Assert.Equal(1, history.Value.AuthorCount90d);
    }

    [Fact]
    public async Task TryCreateAsync_RecordsBinaryTouchesWithZeroChurn()
    {
        InitializeRepository();
        CommitBinaryFile("image.bin", [0x42, 0x00, 0x43], "artist@example.com", DateTimeOffset.UtcNow.AddDays(-5));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(_rootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = Assert.Single(snapshot.FileHistoryByAnalysisRelativePath);
        Assert.Equal("image.bin", history.Key);
        Assert.Equal(0, history.Value.ChurnLines90d);
        Assert.Equal(1, history.Value.TouchCount90d);
        Assert.Equal(1, history.Value.AuthorCount90d);
    }

    [Fact]
    public async Task TryCreateAsync_FiltersToNestedAnalysisRoot()
    {
        InitializeRepository();
        CommitTextFile("src/Program.cs", "class Program {}\n", "dev@example.com", DateTimeOffset.UtcNow.AddDays(-8));
        CommitTextFile("tests/ProgramTests.cs", "class ProgramTests {}\n", "dev@example.com", DateTimeOffset.UtcNow.AddDays(-7));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(Path.Combine(_rootPath, "src"), CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = Assert.Single(snapshot.FileHistoryByAnalysisRelativePath);
        Assert.Equal("Program.cs", history.Key);
    }

    [Fact]
    public async Task TryCreateAsync_CountsDuplicateTouchesAcrossCommits()
    {
        InitializeRepository();
        CommitTextFile("src/Program.cs", "line-1\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-15));
        CommitTextFile("src/Program.cs", "line-1\nline-2\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-14));
        CommitTextFile("src/Program.cs", "line-1\nline-2\nline-3\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-13));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(_rootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = Assert.Single(snapshot.FileHistoryByAnalysisRelativePath);
        Assert.Equal(3, history.Value.TouchCount90d);
    }

    [Fact]
    public async Task TryCreateAsync_CountsUniqueAuthorsByEmail()
    {
        InitializeRepository();
        CommitTextFile("src/Program.cs", "line-1\n", "first@example.com", DateTimeOffset.UtcNow.AddDays(-12));
        CommitTextFile("src/Program.cs", "line-1\nline-2\n", "second@example.com", DateTimeOffset.UtcNow.AddDays(-11));
        CommitTextFile("src/Program.cs", "line-1\nline-2\nline-3\n", "SECOND@example.com", DateTimeOffset.UtcNow.AddDays(-10));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(_rootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = Assert.Single(snapshot.FileHistoryByAnalysisRelativePath);
        Assert.Equal(2, history.Value.AuthorCount90d);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            foreach (var filePath in Directory.EnumerateFiles(_rootPath, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(filePath, FileAttributes.Normal);
            }

            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private void InitializeRepository()
    {
        if (Repository.IsValid(_rootPath))
        {
            return;
        }

        Repository.Init(_rootPath);
    }

    private void CommitTextFile(string relativePath, string content, string authorEmail, DateTimeOffset authoredAt)
    {
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        CommitPath(relativePath, authorEmail, authoredAt);
    }

    private void CommitBinaryFile(string relativePath, byte[] content, string authorEmail, DateTimeOffset authoredAt)
    {
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        CommitPath(relativePath, authorEmail, authoredAt);
    }

    private void CommitPath(string relativePath, string authorEmail, DateTimeOffset authoredAt)
    {
        using var repository = new Repository(_rootPath);
        Commands.Stage(repository, relativePath.Replace('\\', '/'));

        var signature = new Signature(
            name: authorEmail.Split('@')[0],
            email: authorEmail,
            when: authoredAt);
        repository.Commit($"Update {relativePath}", signature, signature);
    }

    private string GetFullPath(string relativePath) =>
        Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
