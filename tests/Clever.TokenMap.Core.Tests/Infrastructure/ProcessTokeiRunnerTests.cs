using Clever.TokenMap.Infrastructure.Tokei;

namespace Clever.TokenMap.Core.Tests.Infrastructure;

public sealed class ProcessTokeiRunnerTests
{
    [Fact]
    public async Task CollectAsync_UsesSidecarExecutable_WhenPresent()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repoRoot = GetRepositoryRoot();
        var fixtureRoot = Path.Combine(repoRoot, "tests", "Fixtures", "TokeiSidecarFixture");
        var originalCurrentDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(repoRoot);

            var runner = new ProcessTokeiRunner();
            var stats = await runner.CollectAsync(
                fixtureRoot,
                ["Program.cs"],
                CancellationToken.None);

            var programStats = Assert.Single(stats);
            Assert.Equal("Program.cs", programStats.Key);
            Assert.True(programStats.Value.TotalLines > 0);
            Assert.Equal("C#", programStats.Value.Language);
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
        }
    }

    private static string GetRepositoryRoot() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
}
