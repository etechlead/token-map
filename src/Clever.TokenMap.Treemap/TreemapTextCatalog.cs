using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Clever.TokenMap.Core.Metrics;

namespace Clever.TokenMap.Treemap;

public sealed class TreemapTextCatalog
{
    private static readonly IReadOnlyDictionary<MetricId, string> DefaultMetricLabels =
        new ReadOnlyDictionary<MetricId, string>(
            DefaultMetricCatalog.GetUserVisibleDefinitions()
                .ToDictionary(definition => definition.Id, definition => definition.DisplayName));

    private static readonly IReadOnlyDictionary<MetricId, string> EmptyMetricLabels =
        new ReadOnlyDictionary<MetricId, string>(new Dictionary<MetricId, string>());

    public static TreemapTextCatalog Default { get; } = new(
        placeholderNoSnapshot: "Run analysis to populate treemap.",
        placeholderNoWeightedNodes: "No weighted nodes for the selected metric.",
        rootPath: "(root)",
        notAvailable: "n/a",
        share: "Share",
        type: "Type",
        extension: "Ext",
        noExtension: "(none)",
        filesInSubtree: "Files in subtree",
        kindRoot: "Root",
        kindDirectory: "Directory",
        kindFile: "File",
        metricLabels: DefaultMetricLabels);

    public TreemapTextCatalog(
        string placeholderNoSnapshot,
        string placeholderNoWeightedNodes,
        string rootPath,
        string notAvailable,
        string share,
        string type,
        string extension,
        string noExtension,
        string filesInSubtree,
        string kindRoot,
        string kindDirectory,
        string kindFile,
        IReadOnlyDictionary<MetricId, string>? metricLabels = null)
    {
        PlaceholderNoSnapshot = placeholderNoSnapshot;
        PlaceholderNoWeightedNodes = placeholderNoWeightedNodes;
        RootPath = rootPath;
        NotAvailable = notAvailable;
        Share = share;
        Type = type;
        Extension = extension;
        NoExtension = noExtension;
        FilesInSubtree = filesInSubtree;
        KindRoot = kindRoot;
        KindDirectory = kindDirectory;
        KindFile = kindFile;
        MetricLabels = metricLabels ?? EmptyMetricLabels;
    }

    public string PlaceholderNoSnapshot { get; }

    public string PlaceholderNoWeightedNodes { get; }

    public string RootPath { get; }

    public string NotAvailable { get; }

    public string Share { get; }

    public string Type { get; }

    public string Extension { get; }

    public string NoExtension { get; }

    public string FilesInSubtree { get; }

    public string KindRoot { get; }

    public string KindDirectory { get; }

    public string KindFile { get; }

    public IReadOnlyDictionary<MetricId, string> MetricLabels { get; }

    public string GetMetricLabel(MetricId metricId, string fallback)
    {
        var normalizedMetricId = DefaultMetricCatalog.NormalizeMetricId(metricId);
        return MetricLabels.TryGetValue(normalizedMetricId, out var label) &&
               !string.IsNullOrWhiteSpace(label)
            ? label
            : fallback;
    }
}
