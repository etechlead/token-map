using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Scanning;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class IgnorePolicyScannerTests
{
    private readonly string _fixtureRoot = Path.Combine(
        FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "IgnorePolicyFixture");

    [Fact]
    public async Task ScanAsync_RespectsIgnoreFilesAndDefaultExcludes()
    {
        var scanner = new FileSystemProjectScanner();

        var snapshot = await scanner.ScanAsync(_fixtureRoot, ScanOptions.Default, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("keep-root.txt", relativePaths);
        Assert.Contains("shadow.txt", relativePaths);
        Assert.Contains("nested", relativePaths);
        Assert.Contains("nested/keep.txt", relativePaths);
        Assert.DoesNotContain("root-hidden.txt", relativePaths);
        Assert.DoesNotContain("nested/shadow.txt", relativePaths);
        Assert.DoesNotContain(".git", relativePaths);
        Assert.DoesNotContain("node_modules", relativePaths);
        Assert.DoesNotContain("bin", relativePaths);
        Assert.DoesNotContain("obj", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_UserExcludesAreAppliedRelativeToRoot()
    {
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            RespectDotIgnore = false,
            UseDefaultExcludes = false,
            UserExcludes = ["scripts", "keep-root.txt"],
        };

        var snapshot = await scanner.ScanAsync(_fixtureRoot, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.DoesNotContain("scripts", relativePaths);
        Assert.DoesNotContain("keep-root.txt", relativePaths);
        Assert.Contains("nested/scripts", relativePaths);
        Assert.Contains("nested/scripts/nested-script.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_TogglesIgnorePolicies()
    {
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            RespectDotIgnore = false,
            UseDefaultExcludes = false,
        };

        var snapshot = await scanner.ScanAsync(_fixtureRoot, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("root-hidden.txt", relativePaths);
        Assert.Contains("nested/shadow.txt", relativePaths);
        Assert.Contains(".git", relativePaths);
        Assert.Contains("node_modules", relativePaths);
        Assert.Contains("bin", relativePaths);
        Assert.Contains("obj", relativePaths);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Clever.TokenMap.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static HashSet<string> GetRelativePaths(Clever.TokenMap.Core.Models.ProjectNode node)
    {
        var relativePaths = new HashSet<string>(StringComparer.Ordinal);
        Collect(node, relativePaths);
        return relativePaths;
    }

    private static void Collect(Clever.TokenMap.Core.Models.ProjectNode node, HashSet<string> relativePaths)
    {
        if (!string.IsNullOrEmpty(node.RelativePath))
        {
            relativePaths.Add(node.RelativePath);
        }

        foreach (var child in node.Children)
        {
            Collect(child, relativePaths);
        }
    }
}
