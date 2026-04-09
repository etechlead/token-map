using System;
using System.Collections.Generic;
using System.Linq;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class RefactorPromptTemplateSettingsViewModel : ViewModelBase
{
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly LocalizationState _localization;
    private readonly Dictionary<string, string> _editorDrafts = new(StringComparer.OrdinalIgnoreCase);
    private readonly RelayCommand _closeEditorCommand;
    private readonly RelayCommand _openEditorCommand;
    private readonly RelayCommand _resetEditorCommand;
    private readonly RelayCommand _saveEditorCommand;

    public RefactorPromptTemplateSettingsViewModel(
        ISettingsCoordinator settingsCoordinator,
        LocalizationState localization)
    {
        _settingsCoordinator = settingsCoordinator;
        _localization = localization;
        _settingsCoordinator.State.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IReadOnlySettingsState.SelectedPromptLanguageTag))
            {
                RefreshEditorForSelectedLanguage();
                OnPropertyChanged(nameof(SelectedPromptLanguageTag));
                OnPropertyChanged(nameof(SelectedPromptLanguageOption));
            }
        };
        _closeEditorCommand = new RelayCommand(CloseEditor, () => IsEditorOpen);
        _openEditorCommand = new RelayCommand(OpenEditor);
        _resetEditorCommand = new RelayCommand(ResetEditorToDefault, () => IsEditorOpen);
        _saveEditorCommand = new RelayCommand(SaveEditor, () => IsEditorOpen);
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditorOpen))]
    private RefactorPromptTemplateEditorViewModel? editor;

    public bool IsEditorOpen => Editor is not null;

    public IRelayCommand OpenEditorCommand => _openEditorCommand;

    public IRelayCommand CloseEditorCommand => _closeEditorCommand;

    public IRelayCommand SaveEditorCommand => _saveEditorCommand;

    public IRelayCommand ResetEditorCommand => _resetEditorCommand;

    public IReadOnlyList<ApplicationLanguageOption> PromptLanguageOptions => _localization.PromptLanguageOptions;

    public string SelectedPromptLanguageTag
    {
        get => _settingsCoordinator.State.SelectedPromptLanguageTag;
        set => _settingsCoordinator.SetSelectedPromptLanguageTag(value);
    }

    public ApplicationLanguageOption? SelectedPromptLanguageOption
    {
        get => PromptLanguageOptions.FirstOrDefault(option =>
            string.Equals(option.Value, SelectedPromptLanguageTag, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value is null || string.Equals(value.Value, SelectedPromptLanguageTag, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            SelectedPromptLanguageTag = value.Value;
        }
    }

    public void OpenEditor()
    {
        _editorDrafts.Clear();
        Editor = CreateEditor(_settingsCoordinator.State.SelectedPromptLanguageTag);
    }

    public void CloseEditor()
    {
        _editorDrafts.Clear();
        Editor = null;
    }

    public void SaveEditor()
    {
        if (Editor is null)
        {
            return;
        }

        StoreCurrentEditorDraft();

        foreach (var pair in _editorDrafts)
        {
            _settingsCoordinator.SetRefactorPromptTemplate(pair.Key, pair.Value);
        }

        _editorDrafts.Clear();
        Editor = null;
    }

    public void ResetEditorToDefault()
    {
        if (Editor is null)
        {
            return;
        }

        Editor.TemplateText = RefactorPromptTemplateCatalog.GetDefaultTemplate(Editor.PromptLanguageTag);
    }

    partial void OnEditorChanged(RefactorPromptTemplateEditorViewModel? value)
    {
        OnPropertyChanged(nameof(IsEditorOpen));
        _closeEditorCommand.NotifyCanExecuteChanged();
        _saveEditorCommand.NotifyCanExecuteChanged();
        _resetEditorCommand.NotifyCanExecuteChanged();
    }

    private void RefreshEditorForSelectedLanguage()
    {
        if (Editor is null)
        {
            return;
        }

        StoreCurrentEditorDraft();
        Editor = CreateEditor(_settingsCoordinator.State.SelectedPromptLanguageTag);
    }

    private void StoreCurrentEditorDraft()
    {
        if (Editor is null)
        {
            return;
        }

        _editorDrafts[Editor.PromptLanguageTag] = Editor.TemplateText;
    }

    private RefactorPromptTemplateEditorViewModel CreateEditor(string languageTag)
    {
        var templateText = _editorDrafts.TryGetValue(languageTag, out var draft)
            ? draft
            : _settingsCoordinator.State.GetRefactorPromptTemplate(languageTag);
        return new RefactorPromptTemplateEditorViewModel(languageTag, templateText, _localization);
    }
}
