using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Infrastructure.Paths;

namespace Clever.TokenMap.Infrastructure.Tokei;

public sealed class ProcessTokeiRunner : ITokeiRunner
{
    private readonly string? _executablePath;
    private readonly PathNormalizer _pathNormalizer;
    private readonly TokeiJsonParser _parser;

    public ProcessTokeiRunner(string? executablePath = null, PathNormalizer? pathNormalizer = null)
    {
        _executablePath = executablePath;
        _pathNormalizer = pathNormalizer ?? new PathNormalizer();
        _parser = new TokeiJsonParser(_pathNormalizer);
    }

    public async Task<IReadOnlyDictionary<string, TokeiFileStats>> CollectAsync(
        string rootPath,
        IReadOnlyCollection<string> includedRelativePaths,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(includedRelativePaths);

        cancellationToken.ThrowIfCancellationRequested();

        if (includedRelativePaths.Count == 0)
        {
            return new Dictionary<string, TokeiFileStats>(_pathNormalizer.PathComparer);
        }

        var normalizedRootPath = _pathNormalizer.NormalizeRootPath(rootPath);
        var executablePath = ResolveExecutablePath();
        using var process = new Process
        {
            StartInfo = CreateStartInfo(executablePath, normalizedRootPath),
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("Unable to start tokei process.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or FileNotFoundException)
        {
            throw new FileNotFoundException("Unable to locate the 'tokei' executable.", executablePath, exception);
        }

        using var registration = cancellationToken.Register(() => TryTerminate(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryTerminate(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"tokei exited with code {process.ExitCode}: {stderr.Trim()}");
        }

        return _parser.Parse(stdout, includedRelativePaths);
    }

    private ProcessStartInfo CreateStartInfo(string executablePath, string rootPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = rootPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(".");
        startInfo.ArgumentList.Add("--files");
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add("json");
        startInfo.ArgumentList.Add("--no-ignore");
        startInfo.ArgumentList.Add("--hidden");

        return startInfo;
    }

    private string ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_executablePath))
        {
            return _pathNormalizer.NormalizeFullPath(_executablePath);
        }

        var executableName = GetExecutableName();
        foreach (var baseDirectory in EnumerateCandidateBaseDirectories())
        {
            var candidatePath = Path.Combine(baseDirectory, "third_party", "tokei", GetRuntimeIdentifier(), executableName);
            if (File.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        return executableName;
    }

    private IEnumerable<string> EnumerateCandidateBaseDirectories()
    {
        var comparer = _pathNormalizer.PathComparer;
        var visited = new HashSet<string>(comparer);

        foreach (var seedDirectory in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(seedDirectory))
            {
                continue;
            }

            var current = new DirectoryInfo(seedDirectory);
            while (current is not null && visited.Add(current.FullName))
            {
                yield return current.FullName;
                current = current.Parent;
            }
        }
    }

    private static void TryTerminate(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
            // Best-effort termination on cancellation.
        }
    }

    private static string GetExecutableName() =>
        OperatingSystem.IsWindows()
            ? "tokei.exe"
            : "tokei";

    private static string GetRuntimeIdentifier()
    {
        if (OperatingSystem.IsWindows())
        {
            return "win-x64";
        }

        if (OperatingSystem.IsMacOS())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "osx-arm64"
                : "osx-x64";
        }

        if (OperatingSystem.IsLinux())
        {
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "linux-arm64"
                : "linux-x64";
        }

        throw new PlatformNotSupportedException("Unsupported platform for tokei sidecar discovery.");
    }
}
