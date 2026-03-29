using System.ComponentModel;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public interface IAnalysisSessionController : INotifyPropertyChanged
{
    string? SelectedFolderPath { get; }

    ProjectSnapshot? CurrentSnapshot { get; }

    AnalysisState State { get; }

    AnalysisProgress? CurrentProgress { get; }

    bool HasSelectedFolder { get; }

    bool HasSnapshot { get; }

    bool IsBusy { get; }

    Task OpenFolderAsync(ScanOptions options);

    Task OpenFolderAsync(string folderPath, ScanOptions options);

    Task RescanAsync(ScanOptions options);

    void Cancel();
}
