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
    public async Task ScanAsync_RespectsGitIgnoreAndSourceControlDefaults()
    {
        var scanner = new FileSystemProjectScanner();

        var snapshot = await scanner.ScanAsync(_fixtureRoot, ScanOptions.Default, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("keep-root.txt", relativePaths);
        Assert.Contains("shadow.txt", relativePaths);
        Assert.Contains("nested", relativePaths);
        Assert.Contains("nested/keep.txt", relativePaths);
        Assert.Contains("nested/shadow.txt", relativePaths);
        Assert.DoesNotContain("root-hidden.txt", relativePaths);
        Assert.DoesNotContain(".git", relativePaths);
        Assert.Contains("node_modules", relativePaths);
        Assert.Contains("bin", relativePaths);
        Assert.Contains("obj", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_GlobalExcludes_FollowGitIgnoreNameOnlyRules()
    {
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = true,
            GlobalExcludes = ["scripts", "keep-root.txt"],
        };

        var snapshot = await scanner.ScanAsync(_fixtureRoot, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.DoesNotContain("scripts", relativePaths);
        Assert.DoesNotContain("keep-root.txt", relativePaths);
        Assert.DoesNotContain("nested/scripts", relativePaths);
        Assert.DoesNotContain("nested/scripts/nested-script.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_TogglesIgnorePolicies()
    {
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = false,
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

    [Fact]
    public async Task ScanAsync_GlobalExcludes_SupportRootRelativeRules()
    {
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = true,
            GlobalExcludes = ["/scripts/"],
        };

        var snapshot = await scanner.ScanAsync(_fixtureRoot, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.DoesNotContain("scripts", relativePaths);
        Assert.Contains("nested/scripts", relativePaths);
        Assert.Contains("nested/scripts/nested-script.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_GlobalExcludes_SupportNegationRules()
    {
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = true,
            GlobalExcludes =
            [
                "scripts/",
                "!nested/scripts/",
            ],
        };

        var snapshot = await scanner.ScanAsync(_fixtureRoot, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.DoesNotContain("scripts", relativePaths);
        Assert.Contains("nested/scripts", relativePaths);
        Assert.Contains("nested/scripts/nested-script.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_FolderExcludes_OverrideGitIgnoreLayers()
    {
        using var tempProject = TemporaryProject.Create();
        tempProject.WriteFile(".gitignore", "!global-hidden.txt\nnested/*\n");
        tempProject.WriteFile("global-hidden.txt", "keep");
        tempProject.WriteFile("nested/.gitignore", "!keep.txt\n");
        tempProject.WriteFile("nested/keep.txt", "keep");

        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = true,
            UseGlobalExcludes = true,
            GlobalExcludes = ["global-hidden.txt"],
            UseFolderExcludes = true,
            FolderExcludes = ["nested/keep.txt"],
        };

        var snapshot = await scanner.ScanAsync(tempProject.RootPath, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("global-hidden.txt", relativePaths);
        Assert.Contains("nested", relativePaths);
        Assert.DoesNotContain("nested/keep.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_DisabledFolderExcludes_DoNotApply()
    {
        using var tempProject = TemporaryProject.Create();
        tempProject.WriteFile("dist/output.txt", "keep");

        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = false,
            UseFolderExcludes = false,
            FolderExcludes = ["dist/"],
        };

        var snapshot = await scanner.ScanAsync(tempProject.RootPath, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("dist", relativePaths);
        Assert.Contains("dist/output.txt", relativePaths);
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

    private sealed class TemporaryProject : IDisposable
    {
        public string RootPath { get; }

        private TemporaryProject(string rootPath)
        {
            RootPath = rootPath;
        }

        public static TemporaryProject Create()
        {
            var rootPath = Path.Combine(
                Path.GetTempPath(),
                "TokenMap.IgnorePolicyTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            return new TemporaryProject(rootPath);
        }

        public void WriteFile(string relativePath, string contents)
        {
            var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
        }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
