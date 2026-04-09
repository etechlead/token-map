using System.Collections.Generic;
using System.Linq;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.App.State;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class RefactorPromptTemplateEditorViewModel : ViewModelBase
{
    public RefactorPromptTemplateEditorViewModel(
        string promptLanguageTag,
        string templateText,
        LocalizationState localization)
    {
        PromptLanguageTag = promptLanguageTag;
        TemplateText = RefactorPromptTemplateCatalog.ResolveTemplate(promptLanguageTag, templateText);
        Placeholders = [.. RefactorPromptTemplateCatalog.GetPlaceholders(localization).Select(
            placeholder => new RefactorPromptTemplatePlaceholderViewModel(placeholder.Token, placeholder.Description))];
    }

    public string PromptLanguageTag { get; }

    [ObservableProperty]
    private string templateText;

    public IReadOnlyList<RefactorPromptTemplatePlaceholderViewModel> Placeholders { get; }
}

public sealed class RefactorPromptTemplatePlaceholderViewModel(string token, string description) : ViewModelBase
{
    public string Token { get; } = token;

    public string Description { get; } = description;
}
