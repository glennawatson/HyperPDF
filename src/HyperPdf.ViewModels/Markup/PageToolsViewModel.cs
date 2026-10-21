// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Navigation;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Moving around and grabbing content in a tab: the select text, hand, zoom to area and snapshot tools, and auto-scroll.</summary>
[DebuggerDisplay("PageToolsViewModel: {PageTool}")]
public sealed partial class PageToolsViewModel : ReactiveObject
{
    /// <summary>The file name suggested when a snapshot is saved as a picture.</summary>
    private const string SnapshotFileName = "snapshot.png";

    /// <summary>Gets the suggested name of a saved snapshot.</summary>
    public string SnapshotName => SnapshotFileName;

    /// <summary>Gets or sets what dragging on the pages does.</summary>
    [Reactive(nameof(IsSelectTextTool), nameof(IsHandTool), nameof(IsZoomAreaTool), nameof(IsSnapshotTool))]
    public partial PageTool PageTool { get; set; }

    /// <summary>Gets a value indicating whether dragging selects text.</summary>
    public bool IsSelectTextTool => PageTool == PageTool.SelectText;

    /// <summary>Gets a value indicating whether dragging moves the pages.</summary>
    public bool IsHandTool => PageTool == PageTool.Hand;

    /// <summary>Gets a value indicating whether dragging a rectangle zooms to it.</summary>
    public bool IsZoomAreaTool => PageTool == PageTool.ZoomArea;

    /// <summary>Gets a value indicating whether dragging a rectangle copies a picture of it.</summary>
    public bool IsSnapshotTool => PageTool == PageTool.Snapshot;

    /// <summary>Gets or sets a value indicating whether the pages scroll by themselves.</summary>
    [Reactive]
    public partial bool IsAutoScrolling { get; set; }

    /// <summary>Gets the auto-scroll speed, from <see cref="AutoScroller.MinSpeed"/> to <see cref="AutoScroller.MaxSpeed"/>.</summary>
    [Reactive(nameof(AutoScrollText))]
    public partial int AutoScrollSpeed { get; private set; } = AutoScroller.DefaultSpeed;

    /// <summary>Gets the auto-scroll bar's text: the speed and how to change it or stop.</summary>
    public string AutoScrollText =>
        string.Create(CultureInfo.CurrentCulture, $"Scrolling by itself at speed {AutoScrollSpeed} of {AutoScroller.MaxSpeed}. Up and Down change the speed. Escape or a click stops.");

    /// <summary>Gets the interaction asking where to save a snapshot when the clipboard cannot hold pictures.</summary>
    public Interaction<string, string?> SaveImageInteraction { get; } = new();

    /// <summary>Changes the auto-scroll speed by some steps, within its limits.</summary>
    /// <param name="steps">The steps; positive is faster.</param>
    /// <returns><see langword="true"/> when the speed changed.</returns>
    public bool ChangeAutoScrollSpeed(int steps)
    {
        var speed = AutoScroller.ClampSpeed(AutoScrollSpeed + steps);
        if (speed == AutoScrollSpeed)
        {
            return false;
        }

        AutoScrollSpeed = speed;
        return true;
    }

    /// <summary>Chooses what dragging on the pages does.</summary>
    /// <param name="tool">The tool.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetPageTool(PageTool tool) => PageTool = tool;

    /// <summary>Turns the Snapshot tool on, or back to selecting text when it is on.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleSnapshotTool() => PageTool = PageTool == PageTool.Snapshot ? PageTool.SelectText : PageTool.Snapshot;

    /// <summary>Starts or stops auto-scroll.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleAutoScroll() => IsAutoScrolling = !IsAutoScrolling;

    /// <summary>Stops auto-scroll.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StopAutoScroll() => IsAutoScrolling = false;

    /// <summary>Makes auto-scroll faster.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FasterAutoScroll() => _ = ChangeAutoScrollSpeed(1);

    /// <summary>Makes auto-scroll slower.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SlowerAutoScroll() => _ = ChangeAutoScrollSpeed(-1);
}
