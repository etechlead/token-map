using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public interface IRefactorPromptComposer
{
    string Compose(ProjectNode node);
}
