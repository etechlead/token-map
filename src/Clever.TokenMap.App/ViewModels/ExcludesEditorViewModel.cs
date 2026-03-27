using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public sealed partial class ExcludesEditorViewModel : ViewModelBase
{
    private const string GlobalExcludesHelperText = "Use gitignore-style rules, one per line. Use / for project-root rules, ! for re-include rules, and # for comments.";
    private const string FolderExcludesHelperText = "Use gitignore-style rules, one per line. These rules apply only to the current folder and override .gitignore. Use / for folder-root rules, ! for re-include rules, and # for comments.";

    private readonly IAnalysisSessionController _analysisSessionController;
    private readonly Func<ScanOptions> _buildScanOptions;
    private readonly RelayCommand _cancelCommand;
    private readonly RelayCommand<ProjectNode?> _excludeNodeFromFolderCommand;
    private readonly RelayCommand _openFolderCommand;
    private readonly RelayCommand _openGlobalCommand;
    private readonly AsyncRelayCommand _saveAndRescanCommand;
    private readonly RelayCommand _saveCommand;
    private readonly ISettingsCoordinator _settingsCoordinator;
    private ExcludesEditorScope _activeScope;
    private bool _isLoadingText;

    private enum ExcludesEditorScope
    {
        Global,
        Folder,
    }

    public ExcludesEditorViewModel(
        ISettingsCoordinator settingsCoordinator,
        IAnalysisSessionController analysisSessionController,
        Func<ScanOptions> buildScanOptions)
    {
        _settingsCoordinator = settingsCoordinator;
        _analysisSessionController = analysisSessionController;
        _buildScanOptions = buildScanOptions;

        _cancelCommand = new RelayCommand(Cancel);
        _excludeNodeFromFolderCommand = new RelayCommand<ProjectNode?>(ExcludeNodeFromFolder, CanExcludeNodeFromFolder);
        _openFolderCommand = new RelayCommand(OpenFolder, () => _settingsCoordinator.CurrentFolderState.HasActiveFolder);
        _openGlobalCommand = new RelayCommand(OpenGlobal);
        _saveAndRescanCommand = new AsyncRelayCommand(SaveAndRescanAsync, CanSaveAndRescan);
        _saveCommand = new RelayCommand(Save);

        _analysisSessionController.PropertyChanged += AnalysisSessionControllerOnPropertyChanged;
        _settingsCoordinator.CurrentFolderState.PropertyChanged += CurrentFolderStateOnPropertyChanged;
    }

    [ObservableProperty]
    private bool isOpen;

    [ObservableProperty]
    private string text = string.Join(Environment.NewLine, GlobalExcludeDefaults.DefaultEntries);

    [ObservableProperty]
    private string title = "Global excludes";

    [ObservableProperty]
    private string helperText = GlobalExcludesHelperText;

    [ObservableProperty]
    private bool showRescanNotice;

    public IRelayCommand OpenGlobalCommand => _openGlobalCommand;

    public IRelayCommand OpenFolderCommand => _openFolderCommand;

    public IRelayCommand CancelCommand => _cancelCommand;

    public IRelayCommand SaveCommand => _saveCommand;

    public IAsyncRelayCommand SaveAndRescanCommand => _saveAndRescanCommand;

    public IRelayCommand<ProjectNode?> ExcludeNodeFromFolderCommand => _excludeNodeFromFolderCommand;

    public bool CanExcludeNodeFromFolder(ProjectNode? node) =>
        !_analysisSessionController.IsBusy &&
        _settingsCoordinator.CurrentFolderState.HasActiveFolder &&
        node is not null &&
        !string.IsNullOrWhiteSpace(node.RelativePath) &&
        node.Kind is not ProjectNodeKind.Root;

    private bool CanSaveAndRescan() =>
        !_analysisSessionController.IsBusy &&
        _analysisSessionController.HasSelectedFolder;

    private void OpenGlobal()
    {
        _activeScope = ExcludesEditorScope.Global;
        Title = "Global excludes";
        HelperText = GlobalExcludesHelperText;
        LoadText(_settingsCoordinator.State.GlobalExcludes);
        DismissRescanNotice();
        IsOpen = true;
        RefreshCommandAvailability();
    }

    private void OpenFolder()
    {
        OpenFolderCore(entryToAppend: null);
    }

    private void OpenFolderCore(string? entryToAppend)
    {
        if (!_settingsCoordinator.CurrentFolderState.HasActiveFolder)
        {
            return;
        }

        _activeScope = ExcludesEditorScope.Folder;
        Title = $"Excludes for {FolderDisplayText.GetFolderDisplayName(_settingsCoordinator.CurrentFolderState.ActiveRootPath)}";
        HelperText = FolderExcludesHelperText;

        var entries = _settingsCoordinator.CurrentFolderState.FolderExcludes.ToList();
        if (!string.IsNullOrWhiteSpace(entryToAppend) &&
            !entries.Contains(entryToAppend, StringComparer.Ordinal))
        {
            entries.Add(entryToAppend);
        }

        LoadText(entries);
        DismissRescanNotice();
        IsOpen = true;
        RefreshCommandAvailability();
    }

    private void Cancel()
    {
        IsOpen = false;
        RefreshCommandAvailability();
    }

    private void Save()
    {
        var changed = SaveInternal();
        ShowRescanNotice = changed && _analysisSessionController.HasSelectedFolder;
    }

    private async Task SaveAndRescanAsync()
    {
        SaveInternal();

        if (_analysisSessionController.HasSelectedFolder && !_analysisSessionController.IsBusy)
        {
            await _analysisSessionController.RescanAsync(_buildScanOptions());
        }
    }

    private bool SaveInternal()
    {
        var updatedEntries = ParseText(Text);
        var changed = SaveByScope(updatedEntries);

        IsOpen = false;
        RefreshCommandAvailability();
        return changed;
    }

    private bool SaveByScope(IReadOnlyList<string> updatedEntries) =>
        _activeScope switch
        {
            ExcludesEditorScope.Folder => SaveFolder(updatedEntries),
            _ => SaveGlobal(updatedEntries),
        };

    private bool SaveGlobal(IReadOnlyList<string> updatedEntries)
    {
        var changed = !_settingsCoordinator.State.GlobalExcludes.SequenceEqual(updatedEntries, StringComparer.Ordinal);
        _settingsCoordinator.ReplaceGlobalExcludes(updatedEntries);
        return changed;
    }

    private bool SaveFolder(IReadOnlyList<string> updatedEntries)
    {
        if (!_settingsCoordinator.CurrentFolderState.HasActiveFolder)
        {
            return false;
        }

        var changed = !_settingsCoordinator.CurrentFolderState.FolderExcludes.SequenceEqual(updatedEntries, StringComparer.Ordinal);
        if (!_settingsCoordinator.CurrentFolderState.UseFolderExcludes)
        {
            _settingsCoordinator.SetUseFolderExcludes(true);
            changed = true;
        }

        _settingsCoordinator.ReplaceFolderExcludes(updatedEntries);
        return changed;
    }

    private void ExcludeNodeFromFolder(ProjectNode? node)
    {
        if (!CanExcludeNodeFromFolder(node))
        {
            return;
        }

        OpenFolderCore(BuildFolderExcludeEntry(node!));
    }

    private void AnalysisSessionControllerOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IAnalysisSessionController.SelectedFolderPath):
                _openFolderCommand.NotifyCanExecuteChanged();
                _excludeNodeFromFolderCommand.NotifyCanExecuteChanged();
                CloseFolderScopedEditorIfNeeded();
                DismissRescanNotice();
                RefreshCommandAvailability();
                break;
            case nameof(IAnalysisSessionController.State):
                if (_analysisSessionController.State == AnalysisState.Scanning)
                {
                    DismissRescanNotice();
                }

                _excludeNodeFromFolderCommand.NotifyCanExecuteChanged();
                RefreshCommandAvailability();
                break;
        }
    }

    private void CurrentFolderStateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IReadOnlyCurrentFolderSettingsState.ActiveRootPath))
        {
            _openFolderCommand.NotifyCanExecuteChanged();
            _excludeNodeFromFolderCommand.NotifyCanExecuteChanged();
            CloseFolderScopedEditorIfNeeded();
        }
    }

    partial void OnTextChanged(string value)
    {
        _ = value;

        if (!_isLoadingText)
        {
            DismissRescanNotice();
        }
    }

    private void RefreshCommandAvailability()
    {
        _saveAndRescanCommand.NotifyCanExecuteChanged();
    }

    private void CloseFolderScopedEditorIfNeeded()
    {
        if (_activeScope == ExcludesEditorScope.Folder && IsOpen)
        {
            IsOpen = false;
            RefreshCommandAvailability();
        }
    }

    private void LoadText(IEnumerable<string> entries)
    {
        _isLoadingText = true;
        try
        {
            Text = string.Join(Environment.NewLine, entries);
        }
        finally
        {
            _isLoadingText = false;
        }
    }

    private void DismissRescanNotice()
    {
        ShowRescanNotice = false;
    }

    private static IReadOnlyList<string> ParseText(string? text) =>
        GlobalExcludeList.Normalize((text ?? string.Empty).ReplaceLineEndings("\n").Split('\n'));

    private static string BuildFolderExcludeEntry(ProjectNode node)
    {
        var relativePath = node.RelativePath.Replace('\\', '/').Trim('/');
        return node.Kind is ProjectNodeKind.Directory
            ? $"/{relativePath}/"
            : $"/{relativePath}";
    }
}
