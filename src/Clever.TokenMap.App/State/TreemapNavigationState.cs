using System;
using System.Collections.Generic;
using System.Linq;
using Clever.TokenMap.Core.Enums;
using Clever.TokenMap.Core.Models;
using Clever.TokenMap.Core.Metrics;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clever.TokenMap.App.State;

public sealed partial class TreemapNavigationState : ObservableObject
{
    private const int MaxThresholdSteps = 256;

    private ProjectSnapshot? _currentSnapshot;
    private MetricId _selectedMetric = MetricIds.Tokens;
    private List<double> _thresholdSteps = [];
    private double _thresholdSliderMaximum;
    private double _thresholdSliderMinimum;
    private double _thresholdSliderValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanResetTreemapRoot))]
    private ProjectNode? treemapRootNode;

    [ObservableProperty]
    private ProjectNode? selectedNode;

    [ObservableProperty]
    private IReadOnlyList<TreemapBreadcrumbItem> treemapBreadcrumbs = [];

    public bool CanResetTreemapRoot =>
        _currentSnapshot is not null &&
        TreemapRootNode is not null &&
        !string.Equals(TreemapRootNode.Id, _currentSnapshot.Root.Id, StringComparison.Ordinal);

    public double ThresholdSliderMinimum
    {
        get => _thresholdSliderMinimum;
        private set
        {
            if (SetProperty(ref _thresholdSliderMinimum, value))
            {
                OnPropertyChanged(nameof(CanAdjustThreshold));
            }
        }
    }

    public double ThresholdSliderMaximum
    {
        get => _thresholdSliderMaximum;
        private set
        {
            if (SetProperty(ref _thresholdSliderMaximum, value))
            {
                OnPropertyChanged(nameof(CanAdjustThreshold));
            }
        }
    }

    public double ThresholdSliderValue
    {
        get => _thresholdSliderValue;
        set
        {
            var normalizedValue = NormalizeThresholdSliderValue(value);
            if (!SetProperty(ref _thresholdSliderValue, normalizedValue))
            {
                return;
            }

            OnPropertyChanged(nameof(ThresholdValue));
            OnPropertyChanged(nameof(ThresholdValueText));
        }
    }

    public bool CanAdjustThreshold => ThresholdSliderMaximum > ThresholdSliderMinimum;

    public double ThresholdValue => _thresholdSteps.Count == 0
        ? 0
        : _thresholdSteps[GetThresholdStepIndex(ThresholdSliderValue)];

    public string ThresholdValueText =>
        MetricValueFormatter.FormatCompact(_selectedMetric, MetricValue.From(ThresholdValue));

    public void LoadSnapshot(ProjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _currentSnapshot = snapshot;
        TreemapRootNode = snapshot.Root;
        SelectedNode = snapshot.Root;
        TreemapBreadcrumbs = BuildTreemapBreadcrumbs(snapshot.Root);
    }

    public void Clear()
    {
        _currentSnapshot = null;
        TreemapRootNode = null;
        SelectedNode = null;
        TreemapBreadcrumbs = [];
        _thresholdSteps = [];
        ThresholdSliderMinimum = 0;
        ThresholdSliderMaximum = 0;
        ThresholdSliderValue = 0;
        OnPropertyChanged(nameof(ThresholdValue));
        OnPropertyChanged(nameof(ThresholdValueText));
    }

    public void SetSelectedMetric(MetricId metric)
    {
        var normalizedMetric = DefaultMetricCatalog.NormalizeMetricId(metric);
        if (_selectedMetric == normalizedMetric)
        {
            return;
        }

        _selectedMetric = normalizedMetric;
        OnPropertyChanged(nameof(ThresholdValueText));
        ResetThresholdRange();
    }

    public void SelectNode(ProjectNode? node)
    {
        SelectedNode = node;
    }

    public bool CanSetTreemapRoot(ProjectNode? node)
    {
        if (_currentSnapshot is null || node is null || !CanDrillInto(node))
        {
            return false;
        }

        return !string.Equals(TreemapRootNode?.Id, node.Id, StringComparison.Ordinal);
    }

    public void SetTreemapRoot(ProjectNode? node)
    {
        if (!CanSetTreemapRoot(node))
        {
            return;
        }

        TreemapRootNode = node;
        SelectedNode = node;
    }

    public bool DrillInto(ProjectNode? node)
    {
        if (!CanSetTreemapRoot(node))
        {
            return false;
        }

        SetTreemapRoot(node);
        return true;
    }

    public void ResetTreemapRoot()
    {
        if (_currentSnapshot is null)
        {
            return;
        }

        TreemapRootNode = _currentSnapshot.Root;
    }

    public void NavigateToBreadcrumb(ProjectNode? node)
    {
        if (node is null)
        {
            return;
        }

        TreemapRootNode = node;
    }

    partial void OnTreemapRootNodeChanged(ProjectNode? value)
    {
        TreemapBreadcrumbs = BuildTreemapBreadcrumbs(value);
        ResetThresholdRange();
    }

    private List<TreemapBreadcrumbItem> BuildTreemapBreadcrumbs(ProjectNode? node)
    {
        if (_currentSnapshot is null || node is null)
        {
            return [];
        }

        var path = new List<ProjectNode>();
        if (!TryBuildNodePath(_currentSnapshot.Root, node.Id, path))
        {
            return [];
        }

        var items = new List<TreemapBreadcrumbItem>(path.Count);
        for (var index = 0; index < path.Count; index++)
        {
            var pathNode = path[index];
            var label = index == 0
                ? pathNode.Name
                : $"/ {pathNode.Name}";
            items.Add(new TreemapBreadcrumbItem(
                label,
                pathNode,
                canNavigate: index < path.Count - 1));
        }

        return items;
    }

    private static bool TryBuildNodePath(ProjectNode current, string targetId, List<ProjectNode> path)
    {
        path.Add(current);
        if (string.Equals(current.Id, targetId, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var child in current.Children)
        {
            if (TryBuildNodePath(child, targetId, path))
            {
                return true;
            }
        }

        path.RemoveAt(path.Count - 1);
        return false;
    }

    private static bool CanDrillInto(ProjectNode? node) =>
        node is not null &&
        node.Kind != ProjectNodeKind.File &&
        node.Children.Count > 0;

    private void ResetThresholdRange()
    {
        if (TreemapRootNode is null)
        {
            _thresholdSteps = [];
            ThresholdSliderMinimum = 0;
            ThresholdSliderMaximum = 0;
            ThresholdSliderValue = 0;
            OnPropertyChanged(nameof(ThresholdValue));
            OnPropertyChanged(nameof(ThresholdValueText));
            return;
        }

        var metric = DefaultMetricCatalog.NormalizeMetricId(_selectedMetric);
        _thresholdSteps = BuildThresholdSteps(TreemapRootNode, metric);
        ThresholdSliderMinimum = 0;
        ThresholdSliderMaximum = Math.Max(0, _thresholdSteps.Count - 1);
        ThresholdSliderValue = 0;
        OnPropertyChanged(nameof(ThresholdValue));
        OnPropertyChanged(nameof(ThresholdValueText));
    }

    private static IEnumerable<double> EnumerateThresholdCandidates(ProjectNode node, MetricId metric)
    {
        if (IsLeafNode(node))
        {
            var weight = node.ComputedMetrics.TryGetNumber(metric);
            if (weight is > 0)
            {
                yield return weight.Value;
            }

            yield break;
        }

        foreach (var child in node.Children)
        {
            foreach (var weight in EnumerateThresholdCandidates(child, metric))
            {
                yield return weight;
            }
        }
    }

    private static List<double> BuildThresholdSteps(ProjectNode node, MetricId metric)
    {
        var values = EnumerateThresholdCandidates(node, metric)
            .OrderBy(static value => value)
            .ToList();

        if (values.Count == 0)
        {
            return [];
        }

        if (values.Count <= MaxThresholdSteps)
        {
            return DeduplicateSortedValues(values);
        }

        var steps = new List<double>(MaxThresholdSteps);
        double? previous = null;

        for (var index = 0; index < MaxThresholdSteps; index++)
        {
            var sampleIndex = (int)Math.Round(
                index * (values.Count - 1d) / (MaxThresholdSteps - 1d),
                MidpointRounding.AwayFromZero);
            var sample = values[sampleIndex];
            if (previous.HasValue && sample == previous.Value)
            {
                continue;
            }

            steps.Add(sample);
            previous = sample;
        }

        return steps;
    }

    private static List<double> DeduplicateSortedValues(List<double> values)
    {
        var uniqueValues = new List<double>(values.Count);
        double? previous = null;

        foreach (var value in values)
        {
            if (previous.HasValue && value == previous.Value)
            {
                continue;
            }

            uniqueValues.Add(value);
            previous = value;
        }

        return uniqueValues;
    }

    private int GetThresholdStepIndex(double sliderValue)
    {
        if (_thresholdSteps.Count == 0)
        {
            return 0;
        }

        var roundedValue = (int)Math.Round(sliderValue, MidpointRounding.AwayFromZero);
        return Math.Clamp(roundedValue, 0, _thresholdSteps.Count - 1);
    }

    private double NormalizeThresholdSliderValue(double sliderValue)
    {
        if (_thresholdSteps.Count == 0)
        {
            return 0;
        }

        return GetThresholdStepIndex(sliderValue);
    }

    private static bool IsLeafNode(ProjectNode node) =>
        node.Kind == ProjectNodeKind.File || node.Children.Count == 0;
}
