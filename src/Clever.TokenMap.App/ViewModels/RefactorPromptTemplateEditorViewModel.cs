using System.Collections.Generic;
using System.Linq;
using Clever.TokenMap.App.Services;
using Clever.TokenMap.Core.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.ViewModels;

public partial class RefactorPromptTemplateEditorViewModel : ViewModelBase
{
    public RefactorPromptTemplateEditorViewModel(string templateText)
    {
        TemplateText = string.IsNullOrWhiteSpace(templateText)
            ? RefactorPromptTemplateDefaults.DefaultRefactorPromptTemplate
            : templateText;
        Placeholders = [.. RefactorPromptTemplateCatalog.Placeholders.Select(
            placeholder => new RefactorPromptTemplatePlaceholderViewModel(placeholder.Token, placeholder.Description))];
    }

    [ObservableProperty]
    private string templateText;

    public IReadOnlyList<RefactorPromptTemplatePlaceholderViewModel> Placeholders { get; }
}

public sealed class RefactorPromptTemplatePlaceholderViewModel(string token, string description) : ViewModelBase
{
    public string Token { get; } = token;

    public string Description { get; } = description;
}
