// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.App.Services;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Measuring;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The measuring tool: distances, path lengths and areas between points clicked on a page, at the scale the page
/// declares or one the user types, such as "1 cm = 2 m".
/// </summary>
[DebuggerDisplay("MeasureViewModel: {Mode}: {Result}")]
public sealed partial class MeasureViewModel : ReactiveObject, IDisposable
{
    /// <summary>The points of a line: its two ends.</summary>
    private const int LineEnds = 2;

    /// <summary>The characters a measurement description can use.</summary>
    private const int ResultCapacity = 256;

    /// <summary>The fewest corners that enclose an area.</summary>
    private const int AreaCorners = 3;

    /// <summary>Follows the tool, mode and scale; disposed with the view model.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The points clicked so far, in page space.</summary>
    private readonly List<PagePoint> _points = [];

    /// <summary>The page each declared scale was read for, so the file is read once per page.</summary>
    private readonly Dictionary<int, MeasureScale?> _declared = [];

    /// <summary>Initializes a new instance of the <see cref="MeasureViewModel"/> class.</summary>
    /// <param name="owner">The tab.</param>
    public MeasureViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        ScaleText = DefaultScale.ToString();

        // The first value of each is the current state, so only later changes act.
        _subscriptions.Add(this.WhenChanged(static x => x.IsOn).Skip(1).SubscribeSafe(_ => Clear(), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.Mode).Skip(1).SubscribeSafe(_ => Clear(), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.ScaleText).Skip(1).SubscribeSafe(_ => Update(), ErrorTrace.Write));
    }

    /// <summary>Gets or sets a value indicating whether the measuring tool is out.</summary>
    [Reactive]
    public partial bool IsOn { get; set; }

    /// <summary>Gets or sets what is measured.</summary>
    [Reactive(nameof(IsDistance), nameof(IsPerimeter), nameof(IsArea))]
    public partial MeasureMode Mode { get; set; }

    /// <summary>Gets or sets a value indicating whether distances are measured.</summary>
    public bool IsDistance
    {
        get => Mode == MeasureMode.Distance;
        set => SelectMode(value, MeasureMode.Distance);
    }

    /// <summary>Gets or sets a value indicating whether path lengths are measured.</summary>
    public bool IsPerimeter
    {
        get => Mode == MeasureMode.Perimeter;
        set => SelectMode(value, MeasureMode.Perimeter);
    }

    /// <summary>Gets or sets a value indicating whether areas are measured.</summary>
    public bool IsArea
    {
        get => Mode == MeasureMode.Area;
        set => SelectMode(value, MeasureMode.Area);
    }

    /// <summary>Gets or sets the scale as written, such as "1 cm = 2 m" or "1:100".</summary>
    [Reactive(nameof(IsScaleValid))]
    public partial string ScaleText { get; set; }

    /// <summary>Gets a value indicating whether the scale text can be read.</summary>
    public bool IsScaleValid => MeasureScale.TryParse(ScaleText, out _);

    /// <summary>Gets the page being measured on, or -1.</summary>
    public int Page { get; private set; } = -1;

    /// <summary>Gets the points of the measurement, in page space, including the moving point under the pointer.</summary>
    public IReadOnlyList<PagePoint> Points => _points;

    /// <summary>Gets a value indicating whether the measurement is finished, so the next click starts a new one.</summary>
    [Reactive(nameof(CanKeep))]
    public partial bool IsFinished { get; private set; }

    /// <summary>Gets the measurement as shown and read out, such as "Area 6.25 m²".</summary>
    [Reactive(nameof(CanKeep))]
    public partial string Result { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether the finished measurement can be kept on the page.</summary>
    public bool CanKeep => IsFinished && Result.Length > 0;

    /// <summary>Gets the scale used when neither the page nor the user gives one: true size in millimetres or inches.</summary>
    private static MeasureScale DefaultScale => RegionInfo.CurrentRegion.IsMetric ? MeasureScale.Metric : MeasureScale.Imperial;

    /// <summary>Adds a point where the page was clicked; a click on another page, or after finishing, starts again.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point, in page space.</param>
    public void AddPoint(int page, PagePoint point)
    {
        if (page != Page || IsFinished)
        {
            BeginMeasurement(page);
        }

        if (_points.Count > 0)
        {
            // The last point follows the pointer; a click fixes it and adds the next moving one.
            _points[^1] = point;
        }
        else
        {
            _points.Add(point);
        }

        if (Mode == MeasureMode.Distance && _points.Count == LineEnds)
        {
            Finish();
            return;
        }

        _points.Add(point);
        Update();
    }

    /// <summary>Moves the last point with the pointer, for a live measurement.</summary>
    /// <param name="page">The page under the pointer.</param>
    /// <param name="point">The point, in page space.</param>
    /// <returns><see langword="true"/> when the measurement changed.</returns>
    public bool MovePoint(int page, PagePoint point)
    {
        if (page != Page || IsFinished || _points.Count < 2)
        {
            return false;
        }

        _points[^1] = point;
        Update();
        return true;
    }

    /// <summary>Finishes the measurement at the last fixed point, as a double click or Enter does.</summary>
    public void Finish()
    {
        if (_points.Count > LineEnds && !IsFinished && Mode != MeasureMode.Distance)
        {
            // Drop the moving point.
            _points.RemoveAt(_points.Count - 1);
        }

        IsFinished = _points.Count >= (Mode == MeasureMode.Area ? AreaCorners : LineEnds);
        Update();
    }

    /// <summary>Clears the measurement.</summary>
    [ReactiveCommand]
    public void Clear()
    {
        _points.Clear();
        Page = -1;
        IsFinished = false;
        Result = string.Empty;
        this.RaisePropertyChanged(nameof(Points));
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Gets the scale in use: the one typed, or failing that true size.</summary>
    /// <returns>The scale.</returns>
    public MeasureScale CurrentScale() => MeasureScale.TryParse(ScaleText, out var scale) ? scale : DefaultScale;

    /// <summary>Starts a measurement on a page, taking the page's declared scale when it has one.</summary>
    /// <param name="page">The page.</param>
    private void BeginMeasurement(int page)
    {
        _points.Clear();
        IsFinished = false;
        Page = page;
        if (DeclaredScale(page) is { } declared)
        {
            ScaleText = declared.ToString();
        }
    }

    /// <summary>Reads, once per page, the scale the page declares in its viewport.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The scale, or <see langword="null"/>.</returns>
    private MeasureScale? DeclaredScale(int page)
    {
        if (_declared.TryGetValue(page, out var known))
        {
            return known;
        }

        MeasureScale? scale = null;
        try
        {
            scale = File.Exists(_owner.FilePath) ? PdfViewports.ReadScale(File.ReadAllBytes(_owner.FilePath), page) : null;
        }
        catch (IOException)
        {
            // An unreadable file has no declared scale; the typed one is used.
        }

        _declared[page] = scale;
        return scale;
    }

    /// <summary>Works the result out again.</summary>
    private void Update()
    {
        Span<char> text = stackalloc char[ResultCapacity];
        var points = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_points);
        var scale = CurrentScale();
        if (!Measurement.TryDescribe(Mode, points, scale, text, out var written))
        {
            var description = Measurement.Describe(Mode, points, scale);
            if (!string.Equals(description, Result, StringComparison.Ordinal))
            {
                Result = description;
            }
        }
        else if (!text[..written].SequenceEqual(Result))
        {
            // The text is the same on most pointer moves, so a new string is made only when it changed.
            Result = new(text[..written]);
        }

        this.RaisePropertyChanged(nameof(Points));
    }

    /// <summary>Keeps the finished measurement on the page as an annotation.</summary>
    [ReactiveCommand]
    private void Keep()
    {
        if (CanKeep && _owner.Annotations.AddMeasurement(Page, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_points), Mode == MeasureMode.Area, Result))
        {
            Clear();
        }
    }

    /// <summary>Brings the tool out.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Start() => IsOn = true;

    /// <summary>Puts the tool away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Done() => IsOn = false;

    /// <summary>Brings the tool out or puts it away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Toggle() => IsOn = !IsOn;

    /// <summary>Picks what is measured.</summary>
    /// <param name="mode">The mode.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetMode(MeasureMode mode) => Mode = mode;

    /// <summary>Sets the mode from a toggle.</summary>
    /// <param name="on">Whether the toggle was turned on.</param>
    /// <param name="mode">The toggle's mode.</param>
    private void SelectMode(bool on, MeasureMode mode)
    {
        if (on)
        {
            Mode = mode;
        }
    }
}
