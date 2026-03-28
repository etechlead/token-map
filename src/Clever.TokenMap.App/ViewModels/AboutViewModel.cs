using System;
using System.Reflection;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public sealed record AppAboutInfo(
    string ProductName,
    string Version,
    string InformationalVersion,
    string Description,
    string RepositoryDisplayName,
    string RepositoryUrl,
    string LicenseName)
{
    public static AppAboutInfo CreateDefault()
    {
        var assembly = typeof(AppAboutInfo).Assembly;
        var productName = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var userFacingVersion = GetUserFacingVersion(informationalVersion, assembly);

        return new AppAboutInfo(
            string.IsNullOrWhiteSpace(productName) ? "TokenMap" : productName,
            userFacingVersion,
            string.IsNullOrWhiteSpace(informationalVersion) ? userFacingVersion : informationalVersion,
            string.IsNullOrWhiteSpace(description) ? "Local source-tree analysis" : description,
            RepositoryDisplayName: "etechlead/token-map",
            RepositoryUrl: "https://github.com/etechlead/token-map",
            LicenseName: "MIT license");
    }

    private static string GetUserFacingVersion(string? informationalVersion, Assembly assembly)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        }

        var assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion is not null)
        {
            return $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        return "0.0.0";
    }
}

public sealed class AboutViewModel : ViewModelBase
{
    private readonly AppAboutInfo _aboutInfo;
    private readonly AsyncRelayCommand _openRepositoryCommand;
    private readonly IPathShellService _pathShellService;

    public AboutViewModel(AppAboutInfo aboutInfo, IPathShellService pathShellService)
    {
        _aboutInfo = aboutInfo ?? throw new ArgumentNullException(nameof(aboutInfo));
        _pathShellService = pathShellService ?? throw new ArgumentNullException(nameof(pathShellService));
        _openRepositoryCommand = new AsyncRelayCommand(OpenRepositoryAsync);
    }

    public string ProductName => _aboutInfo.ProductName;

    public string VersionText => $"v{_aboutInfo.Version}";

    public string InformationalVersion => _aboutInfo.InformationalVersion;

    public string Description => _aboutInfo.Description;

    public string RepositoryDisplayName => _aboutInfo.RepositoryDisplayName;

    public string RepositoryUrl => _aboutInfo.RepositoryUrl;

    public string LicenseName => _aboutInfo.LicenseName;

    public IAsyncRelayCommand OpenRepositoryCommand => _openRepositoryCommand;

    private async Task OpenRepositoryAsync()
    {
        await _pathShellService.TryOpenAsync(_aboutInfo.RepositoryUrl).ConfigureAwait(false);
    }
}
