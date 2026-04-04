using System;
using System.Threading;
using System.Threading.Tasks;
using Clever.TokenMap.App.State;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Interfaces;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.App.Services;

public sealed class FilePreviewController(
    IFilePreviewContentReader contentReader,
    IUiDispatcher uiDispatcher,
    FilePreviewState state) : IFilePreviewController
{
    private readonly IFilePreviewContentReader _contentReader = contentReader;
    private readonly IUiDispatcher _uiDispatcher = uiDispatcher;
    private CancellationTokenSource? _activeLoadCancellationTokenSource;
    private int _loadVersion;

    public FilePreviewState State { get; } = state;

    public async Task OpenAsync(ProjectNode? node, CancellationToken cancellationToken = default)
    {
        if (node is null || node.Kind is not ProjectNodeKind.File)
        {
            return;
        }

        var loadVersion = BeginLoad(node, cancellationToken, out var linkedCancellationTokenSource);
        try
        {
            var result = await _contentReader.ReadAsync(node.FullPath, linkedCancellationTokenSource.Token).ConfigureAwait(false);
            if (loadVersion != _loadVersion)
            {
                return;
            }

            UpdateState(() => State.ShowResult(node, result));
        }
        catch (OperationCanceledException) when (linkedCancellationTokenSource.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_activeLoadCancellationTokenSource, linkedCancellationTokenSource))
            {
                _activeLoadCancellationTokenSource = null;
            }

            linkedCancellationTokenSource.Dispose();
        }
    }

    public void Close()
    {
        CancelActiveLoad();
        UpdateState(State.Close);
    }

    private int BeginLoad(
        ProjectNode node,
        CancellationToken cancellationToken,
        out CancellationTokenSource linkedCancellationTokenSource)
    {
        CancelActiveLoad();

        linkedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeLoadCancellationTokenSource = linkedCancellationTokenSource;
        var loadVersion = unchecked(++_loadVersion);
        UpdateState(() => State.ShowLoading(node));
        return loadVersion;
    }

    private void CancelActiveLoad()
    {
        if (_activeLoadCancellationTokenSource is null)
        {
            return;
        }

        _activeLoadCancellationTokenSource.Cancel();
        _activeLoadCancellationTokenSource.Dispose();
        _activeLoadCancellationTokenSource = null;
    }

    private void UpdateState(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_uiDispatcher.CheckAccess())
        {
            action();
            return;
        }

        _uiDispatcher.Post(action);
    }
}
