using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clever.TokenMap.App.ViewModels;

public partial class RefactorPromptTemplateSettingsViewModel : ViewModelBase
{
    private readonly ISettingsCoordinator _settingsCoordinator;
    private readonly RelayCommand _closeEditorCommand;
    private readonly RelayCommand _openEditorCommand;
    private readonly RelayCommand _resetEditorCommand;
    private readonly RelayCommand _saveEditorCommand;

    public RefactorPromptTemplateSettingsViewModel(ISettingsCoordinator settingsCoordinator)
    {
        _settingsCoordinator = settingsCoordinator;
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

    public void OpenEditor()
    {
        Editor = new RefactorPromptTemplateEditorViewModel(_settingsCoordinator.State.RefactorPromptTemplate);
    }

    public void CloseEditor()
    {
        Editor = null;
    }

    public void SaveEditor()
    {
        if (Editor is null)
        {
            return;
        }

        _settingsCoordinator.SetRefactorPromptTemplate(Editor.TemplateText);
        Editor = null;
    }

    public void ResetEditorToDefault()
    {
        if (Editor is null)
        {
            return;
        }

        Editor.TemplateText = RefactorPromptTemplateDefaults.DefaultRefactorPromptTemplate;
    }

    partial void OnEditorChanged(RefactorPromptTemplateEditorViewModel? value)
    {
        OnPropertyChanged(nameof(IsEditorOpen));
        _closeEditorCommand.NotifyCanExecuteChanged();
        _saveEditorCommand.NotifyCanExecuteChanged();
        _resetEditorCommand.NotifyCanExecuteChanged();
    }
}
