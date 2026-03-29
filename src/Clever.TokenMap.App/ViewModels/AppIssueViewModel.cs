using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Diagnostics;
using Clever.TokenMap.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public sealed class AppIssueViewModel : ViewModelBase
{
    private readonly AppIssueState _state;
    private readonly IPathShellService _pathShellService;
    private readonly IAppStoragePaths _appStoragePaths;
    private readonly IApplicationControlService _applicationControlService;
    private readonly IAppIssueReporter _issueReporter;
    private readonly RelayCommand _dismissCommand;
    private readonly AsyncRelayCommand _openLogsCommand;
    private readonly RelayCommand _closeAppCommand;

    public AppIssueViewModel(
        AppIssueState state,
        IPathShellService pathShellService,
        IAppStoragePaths appStoragePaths,
        IApplicationControlService applicationControlService,
        IAppIssueReporter issueReporter)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _pathShellService = pathShellService ?? throw new ArgumentNullException(nameof(pathShellService));
        _appStoragePaths = appStoragePaths ?? throw new ArgumentNullException(nameof(appStoragePaths));
        _applicationControlService = applicationControlService ?? throw new ArgumentNullException(nameof(applicationControlService));
        _issueReporter = issueReporter ?? throw new ArgumentNullException(nameof(issueReporter));

        _dismissCommand = new RelayCommand(_state.Dismiss, () => ActiveIssue is not null && !IsFatal);
        _openLogsCommand = new AsyncRelayCommand(OpenLogsAsync, () => ActiveIssue is not null);
        _closeAppCommand = new RelayCommand(() => _applicationControlService.RequestShutdown(1), () => IsFatal);

        _state.PropertyChanged += StateOnPropertyChanged;
    }

    public bool HasActiveIssue => _state.HasActiveIssue;

    public bool IsBannerVisible => ActiveIssue is not null && !IsFatal;

    public bool IsModalVisible => IsFatal;

    public bool IsFatal => ActiveIssue?.Issue.IsFatal == true;

    public string Title => IsFatal
        ? "Unrecoverable application error"
        : "Application issue";

    public string Message => ActiveIssue?.Issue.UserMessage ?? string.Empty;

    public string ReferenceIdText => ActiveIssue is null
        ? string.Empty
        : $"Reference ID: {ActiveIssue.ReferenceId}";

    public IRelayCommand DismissCommand => _dismissCommand;

    public IAsyncRelayCommand OpenLogsCommand => _openLogsCommand;

    public IRelayCommand CloseAppCommand => _closeAppCommand;

    private DisplayedAppIssue? ActiveIssue => _state.ActiveIssue;

    private async Task OpenLogsAsync()
    {
        var logsDirectoryPath = _appStoragePaths.GetLogsDirectoryPath();
        var revealed = await _pathShellService.TryRevealAsync(logsDirectoryPath, isDirectory: true)
            .ConfigureAwait(false);
        if (revealed)
        {
            return;
        }

        _issueReporter.Report(new AppIssue
        {
            Code = "app.open_logs_failed",
            UserMessage = "TokenMap could not open the diagnostics log folder.",
            TechnicalMessage = "Opening the diagnostics log folder through the shell failed.",
            Context = AppIssueContext.Create(("LogsDirectoryPath", logsDirectoryPath)),
        });
    }

    private void StateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppIssueState.ActiveIssue))
        {
            return;
        }

        OnPropertyChanged(nameof(HasActiveIssue));
        OnPropertyChanged(nameof(IsBannerVisible));
        OnPropertyChanged(nameof(IsModalVisible));
        OnPropertyChanged(nameof(IsFatal));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(ReferenceIdText));
        _dismissCommand.NotifyCanExecuteChanged();
        _openLogsCommand.NotifyCanExecuteChanged();
        _closeAppCommand.NotifyCanExecuteChanged();
    }
}
