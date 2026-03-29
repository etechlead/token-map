using System;

namespace Clever.TokenMap.App.Services;

public interface IUiDispatcher
{
    bool CheckAccess();

    void Post(Action action);
}
