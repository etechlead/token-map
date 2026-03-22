using Avalonia;
using Clever.TokenMap.Core.Models;

namespace Clever.TokenMap.Treemap;

public sealed record TreemapNodeVisual(
    ProjectNode Node,
    Rect Bounds,
    int Depth);

