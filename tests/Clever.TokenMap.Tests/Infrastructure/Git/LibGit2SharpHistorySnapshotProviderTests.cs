using System.Diagnostics;
using Clever.TokenMap.Core.Paths;
using Clever.TokenMap.Infrastructure.Analysis.Git;
using LibGit2Sharp;

namespace Clever.TokenMap.Tests.Infrastructure.Git;

public sealed class LibGit2SharpHistorySnapshotProviderTests : IDisposable
{
    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), $"tokenmap-git-{Guid.NewGuid():N}");
    private readonly List<string> _cleanupPaths = [];

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
        Assert.Equal(0, history.Value.UniqueCochangedFileCount90d);
        Assert.Equal(0, history.Value.StrongCochangedFileCount90d);
        Assert.Equal(0d, history.Value.AverageCochangeSetSize90d, precision: 12);
    }

    [Fact]
    public async Task TryCreateAsync_CollectsHistoryWhenAnalysisRootUsesDirectoryAlias()
    {
        InitializeRepository();
        CommitTextFile("src/Program.cs", "line-1\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-20));
        CommitTextFile("src/Program.cs", "line-1\nline-2\n", "author@example.com", DateTimeOffset.UtcNow.AddDays(-10));
        var aliasRootPath = CreateDirectoryAlias(_rootPath);

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(aliasRootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = Assert.Single(snapshot.FileHistoryByAnalysisRelativePath);
        Assert.Equal("src/Program.cs", history.Key);
        Assert.Equal(2, history.Value.TouchCount90d);
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
        Assert.Equal(0, history.Value.UniqueCochangedFileCount90d);
        Assert.Equal(0, history.Value.StrongCochangedFileCount90d);
        Assert.Equal(0d, history.Value.AverageCochangeSetSize90d, precision: 12);
    }

    [Fact]
    public async Task TryCreateAsync_FiltersCochangePartnersToNestedAnalysisRoot()
    {
        InitializeRepository();
        CommitTextFiles(
            [("src/Program.cs", "class Program {}\n"), ("tests/ProgramTests.cs", "class ProgramTests {}\n")],
            "dev@example.com",
            DateTimeOffset.UtcNow.AddDays(-8));
        CommitTextFiles(
            [("src/Program.cs", "class Program { static void Main() { } }\n"), ("src/Utils.cs", "class Utils {}\n")],
            "dev@example.com",
            DateTimeOffset.UtcNow.AddDays(-7));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(Path.Combine(_rootPath, "src"), CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(2, snapshot.FileHistoryByAnalysisRelativePath.Count);

        var programHistory = snapshot.FileHistoryByAnalysisRelativePath["Program.cs"];
        Assert.Equal(1, programHistory.UniqueCochangedFileCount90d);
        Assert.Equal(0, programHistory.StrongCochangedFileCount90d);
        Assert.Equal(0.5d, programHistory.AverageCochangeSetSize90d, precision: 12);

        var utilsHistory = snapshot.FileHistoryByAnalysisRelativePath["Utils.cs"];
        Assert.Equal(1, utilsHistory.UniqueCochangedFileCount90d);
        Assert.Equal(0, utilsHistory.StrongCochangedFileCount90d);
        Assert.Equal(1d, utilsHistory.AverageCochangeSetSize90d, precision: 12);
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

    [Fact]
    public async Task TryCreateAsync_ComputesCochangeBlastRadiusSignals()
    {
        InitializeRepository();
        CommitTextFiles(
            [("A.cs", "class A { }\n"), ("B.cs", "class B { }\n"), ("C.cs", "class C { }\n")],
            "dev@example.com",
            DateTimeOffset.UtcNow.AddDays(-12));
        CommitTextFiles(
            [("A.cs", "class A { int X => 1; }\n"), ("B.cs", "class B { int X => 1; }\n")],
            "dev@example.com",
            DateTimeOffset.UtcNow.AddDays(-11));
        CommitTextFiles(
            [("A.cs", "class A { int X => 2; }\n"), ("D.cs", "class D { }\n")],
            "dev@example.com",
            DateTimeOffset.UtcNow.AddDays(-10));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(_rootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        var history = snapshot.FileHistoryByAnalysisRelativePath["A.cs"];
        Assert.Equal(3, history.TouchCount90d);
        Assert.Equal(3, history.UniqueCochangedFileCount90d);
        Assert.Equal(1, history.StrongCochangedFileCount90d);
        Assert.Equal(4d / 3d, history.AverageCochangeSetSize90d, precision: 12);
    }

    [Fact]
    public async Task TryCreateAsync_ExcludesFilesWithoutRecentCommits()
    {
        InitializeRepository();
        CommitTextFile("A.cs", "class A { }\n", "dev@example.com", DateTimeOffset.UtcNow.AddDays(-120));

        var provider = new LibGit2SharpHistorySnapshotProvider(new PathNormalizer());

        var snapshot = await provider.TryCreateAsync(_rootPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.FileHistoryByAnalysisRelativePath);
    }

    public void Dispose()
    {
        foreach (var cleanupPath in _cleanupPaths)
        {
            if (Directory.Exists(cleanupPath))
            {
                Directory.Delete(cleanupPath);
            }
        }

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
        CommitTextFiles([(relativePath, content)], authorEmail, authoredAt);
    }

    private void CommitTextFiles(
        IReadOnlyList<(string RelativePath, string Content)> files,
        string authorEmail,
        DateTimeOffset authoredAt)
    {
        foreach (var (relativePath, content) in files)
        {
            var fullPath = GetFullPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        CommitPaths(files.Select(file => file.RelativePath), authorEmail, authoredAt);
    }

    private void CommitBinaryFile(string relativePath, byte[] content, string authorEmail, DateTimeOffset authoredAt)
    {
        var fullPath = GetFullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, content);
        CommitPaths([relativePath], authorEmail, authoredAt);
    }

    private void CommitPaths(IEnumerable<string> relativePaths, string authorEmail, DateTimeOffset authoredAt)
    {
        using var repository = new Repository(_rootPath);
        foreach (var relativePath in relativePaths)
        {
            Commands.Stage(repository, relativePath.Replace('\\', '/'));
        }

        var signature = new Signature(
            name: authorEmail.Split('@')[0],
            email: authorEmail,
            when: authoredAt);
        repository.Commit("Update files", signature, signature);
    }

    private string GetFullPath(string relativePath) =>
        Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private string CreateDirectoryAlias(string targetPath)
    {
        var aliasPath = Path.Combine(Path.GetTempPath(), $"tokenmap-git-alias-{Guid.NewGuid():N}");

        if (OperatingSystem.IsWindows())
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c mklink /J \"{aliasPath}\" \"{targetPath}\"",
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }) ?? throw new InvalidOperationException("Failed to start junction creation process.");

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                throw new InvalidOperationException(
                    $"Failed to create directory junction at '{aliasPath}'. Output: {output} Error: {error}");
            }
        }
        else
        {
            Directory.CreateSymbolicLink(aliasPath, targetPath);
        }

        _cleanupPaths.Add(aliasPath);
        return aliasPath;
    }
}
