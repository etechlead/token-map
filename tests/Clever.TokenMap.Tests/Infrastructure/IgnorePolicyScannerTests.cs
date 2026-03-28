using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Scanning;

namespace Clever.TokenMap.Tests.Infrastructure;

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
        using var fixture = CreateIgnorePolicyFixtureProject();
        var scanner = new FileSystemProjectScanner();

        var snapshot = await scanner.ScanAsync(fixture.RootPath, ScanOptions.Default, progress: null, CancellationToken.None);
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
        using var fixture = CreateIgnorePolicyFixtureProject();
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = true,
            GlobalExcludes = ["scripts", "keep-root.txt"],
        };

        var snapshot = await scanner.ScanAsync(fixture.RootPath, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.DoesNotContain("scripts", relativePaths);
        Assert.DoesNotContain("keep-root.txt", relativePaths);
        Assert.DoesNotContain("nested/scripts", relativePaths);
        Assert.DoesNotContain("nested/scripts/nested-script.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_TogglesIgnorePolicies()
    {
        using var fixture = CreateIgnorePolicyFixtureProject();
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = false,
        };

        var snapshot = await scanner.ScanAsync(fixture.RootPath, options, progress: null, CancellationToken.None);
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
        using var fixture = CreateIgnorePolicyFixtureProject();
        var scanner = new FileSystemProjectScanner();
        var options = new ScanOptions
        {
            RespectGitIgnore = false,
            UseGlobalExcludes = true,
            GlobalExcludes = ["/scripts/"],
        };

        var snapshot = await scanner.ScanAsync(fixture.RootPath, options, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.DoesNotContain("scripts", relativePaths);
        Assert.Contains("nested/scripts", relativePaths);
        Assert.Contains("nested/scripts/nested-script.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_GlobalExcludes_SupportNegationRules()
    {
        using var fixture = CreateIgnorePolicyFixtureProject();
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

        var snapshot = await scanner.ScanAsync(fixture.RootPath, options, progress: null, CancellationToken.None);
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
    public async Task ScanAsync_OuterRoot_AppliesNestedDirectoryGitIgnore()
    {
        using var tempProject = TemporaryProject.Create();
        tempProject.WriteFile(".git/config", "[core]\n");
        tempProject.WriteFile("repo-a/.git/config", "[core]\n");
        tempProject.WriteFile("repo-a/.gitignore", "ignored-from-nested.txt\n");
        tempProject.WriteFile("repo-a/ignored-from-nested.txt", "skip");
        tempProject.WriteFile("repo-a/keep.txt", "keep");

        var scanner = new FileSystemProjectScanner();

        var snapshot = await scanner.ScanAsync(tempProject.RootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("repo-a", relativePaths);
        Assert.Contains("repo-a/keep.txt", relativePaths);
        Assert.DoesNotContain("repo-a/ignored-from-nested.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_SelectedNestedRoot_UsesOnlyNestedRootGitIgnoreChain()
    {
        using var tempProject = TemporaryProject.Create();
        tempProject.WriteFile(".git/config", "[core]\n");
        tempProject.WriteFile(".gitignore", "repo-a/ignored-by-parent.txt\n");
        tempProject.WriteFile("repo-a/.git/config", "[core]\n");
        tempProject.WriteFile("repo-a/.gitignore", "ignored-by-nested.txt\n");
        tempProject.WriteFile("repo-a/ignored-by-parent.txt", "keep when repo-a is selected");
        tempProject.WriteFile("repo-a/ignored-by-nested.txt", "skip");
        tempProject.WriteFile("repo-a/keep.txt", "keep");

        var scanner = new FileSystemProjectScanner();

        var snapshot = await scanner.ScanAsync(
            Path.Combine(tempProject.RootPath, "repo-a"),
            ScanOptions.Default,
            progress: null,
            CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("ignored-by-parent.txt", relativePaths);
        Assert.Contains("keep.txt", relativePaths);
        Assert.DoesNotContain("ignored-by-nested.txt", relativePaths);
    }

    [Fact]
    public async Task ScanAsync_GitIgnoreBracketClasses_MatchDirectoryNames()
    {
        using var tempProject = TemporaryProject.Create();
        tempProject.WriteFile(".gitignore", "[Bb]in/\n[Oo]bj/\n");
        tempProject.WriteFile("src/Project/bin/output.dll", "skip");
        tempProject.WriteFile("src/Project/obj/output.g.cs", "skip");
        tempProject.WriteFile("src/Project/keep.txt", "keep");

        var scanner = new FileSystemProjectScanner();

        var snapshot = await scanner.ScanAsync(tempProject.RootPath, ScanOptions.Default, progress: null, CancellationToken.None);
        var relativePaths = GetRelativePaths(snapshot.Root);

        Assert.Contains("src", relativePaths);
        Assert.Contains("src/Project", relativePaths);
        Assert.Contains("src/Project/keep.txt", relativePaths);
        Assert.DoesNotContain("src/Project/bin", relativePaths);
        Assert.DoesNotContain("src/Project/bin/output.dll", relativePaths);
        Assert.DoesNotContain("src/Project/obj", relativePaths);
        Assert.DoesNotContain("src/Project/obj/output.g.cs", relativePaths);
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

    private TemporaryProject CreateIgnorePolicyFixtureProject()
    {
        var fixture = TemporaryProject.Create();
        fixture.CopyDirectoryContentsFrom(_fixtureRoot);
        fixture.WriteFile(".git/config", "[core]\n");
        return fixture;
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

        public void CopyDirectoryContentsFrom(string sourceRootPath)
        {
            foreach (var directoryPath in Directory.GetDirectories(sourceRootPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRootPath, directoryPath);
                if (IsFixtureGitPath(relativePath))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.Combine(RootPath, relativePath));
            }

            foreach (var filePath in Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceRootPath, filePath);
                if (IsFixtureGitPath(relativePath))
                {
                    continue;
                }

                var destinationPath = Path.Combine(RootPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                File.Copy(filePath, destinationPath, overwrite: true);
            }
        }

        private static bool IsFixtureGitPath(string relativePath)
        {
            var normalizedRelativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
            return normalizedRelativePath.Equals(".git", StringComparison.Ordinal) ||
                   normalizedRelativePath.StartsWith(".git/", StringComparison.Ordinal);
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
