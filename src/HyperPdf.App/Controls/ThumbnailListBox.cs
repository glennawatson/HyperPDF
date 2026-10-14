// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using HyperPdf.App.Services;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Keeps thumbnails still while the selected one is in full view, and otherwise brings it to the middle with a short
/// ease. Long jumps cover most of the distance at once, so only the last stretch moves.
/// </summary>
[DebuggerDisplay("ThumbnailListBox: Selected thumbnail: {SelectedIndex}")]
public sealed class ThumbnailListBox : ListBox, IActivatableView
{
    /// <summary>Defines the <see cref="ReduceMotion"/> property.</summary>
    public static readonly StyledProperty<bool> ReduceMotionProperty = AvaloniaProperty.Register<ThumbnailListBox, bool>(nameof(ReduceMotion));

    /// <summary>The tolerance for unchanged layout coordinates.</summary>
    private const double OffsetTolerance = 1e-6;

    /// <summary>The power of the ease out curve.</summary>
    private const double EasePower = 3;

    /// <summary>How long the list takes to ease to the selection.</summary>
    private static readonly TimeSpan EaseDuration = TimeSpan.FromMilliseconds(160);

    /// <summary>The ease running now, or <see langword="null"/> when the list is still.</summary>
    private ThumbnailEase? _running;

    /// <summary>Initializes a new instance of the <see cref="ThumbnailListBox"/> class.</summary>
    public ThumbnailListBox()
    {
        AutoScrollToSelectedItem = false;
        _ = this.WhenActivated(disposables =>
        {
            // Each new selection is followed in turn; following a newer one replaces the frames of the one before.
            disposables.Add(this.WhenChanged(static x => x.SelectedIndex)
                .Skip(1)
                .SelectWith(this, static (list, _) => list)
                .SwitchMap(static list => list.FollowSelection())
                .SubscribeSafe(static ease => ease.List.StepEase(ease), ErrorTrace.Write));

            // Taken off screen, the list forgets the ease it was running.
            disposables.Add(Scope.Create(this, static list => list._running = null));
        });
    }

    /// <summary>Gets or sets a value indicating whether the list jumps to the selection instead of easing.</summary>
    public bool ReduceMotion
    {
        get => GetValue(ReduceMotionProperty);
        set => SetValue(ReduceMotionProperty, value);
    }

    /// <inheritdoc/>
    protected override Type StyleKeyOverride => typeof(ListBox);

    /// <summary>Gets an ease's frames, which end once it has finished or another ease has replaced it.</summary>
    /// <param name="top">The window.</param>
    /// <param name="ease">The ease.</param>
    /// <returns>The ease, once per frame.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<ThumbnailEase> EaseFrames(TopLevel top, ThumbnailEase ease) =>
        top.AnimationFrames
            .SelectWith(ease, static (running, _) => running)
            .TakeWhile(static running => ReferenceEquals(running.List._running, running));

    /// <summary>
    /// Works out how the list follows the newly selected thumbnail: it stays put, jumps, or eases. A thumbnail that is not
    /// laid out yet is brought into view first and followed after the layout pass that places it.
    /// </summary>
    /// <returns>The frames of the ease, if there is one.</returns>
    private IObservable<ThumbnailEase> FollowSelection()
    {
        if (SelectedIndex < 0 || Scroll is not ScrollViewer scroll || scroll.Viewport.Height <= 0)
        {
            return Signal.Empty<ThumbnailEase>();
        }

        // While easing, the list is judged where it is heading, so a page already on its way into view stays put.
        var start = new FollowStart(this, scroll, _running?.To ?? scroll.Offset.Y);
        if (TargetOffset(start) is not { } target)
        {
            ScrollIntoView(SelectedIndex);
            return Signal.Merge(Signal.Return(RxVoid.Default), this.Events().LayoutUpdated.Select(static _ => RxVoid.Default))
                .SelectWith(start, static (follow, _) => follow.List.TargetOffset(follow))
                .KeepValues
                .Take(1)
                .SelectWith(start, static (follow, to) => new FollowTarget(follow, to))
                .SwitchMap(static follow => follow.Start.List.EaseTo(follow.Start, follow.To));
        }

        return EaseTo(start, target);
    }

    /// <summary>Gets the offset that puts the selected thumbnail in full view, once the thumbnail is laid out.</summary>
    /// <param name="start">Where the list started from.</param>
    /// <returns>The offset, or <see langword="null"/> while the thumbnail is not laid out.</returns>
    private double? TargetOffset(FollowStart start)
    {
        var scroll = start.Scroll;
        if (ContainerFromIndex(SelectedIndex) is not { } container || container.TranslatePoint(default, scroll) is not { } position)
        {
            return null;
        }

        var top = position.Y + scroll.Offset.Y;
        return ThumbnailViewport.GetOffset(start.Offset, scroll.Viewport.Height, top, top + container.Bounds.Height, scroll.Extent.Height);
    }

    /// <summary>Eases the list to a new offset, jumps there when motion is reduced, or keeps the ease already running.</summary>
    /// <param name="start">Where the list started from.</param>
    /// <param name="to">The new offset.</param>
    /// <returns>The frames of the ease, or none when the list does not ease.</returns>
    private IObservable<ThumbnailEase> EaseTo(FollowStart start, double to)
    {
        var scroll = start.Scroll;
        var from = start.Offset;
        var top = TopLevel.GetTopLevel(this);
        if (Math.Abs(to - from) <= OffsetTolerance)
        {
            if (_running is { } running && top is not null)
            {
                // The selection is in view where the running ease is heading, so that ease carries on.
                return EaseFrames(top, running);
            }

            if (Math.Abs(scroll.Offset.Y - from) > OffsetTolerance)
            {
                // Realizing the item may have scrolled; the item was already in view, so the list goes back.
                scroll.Offset = new(scroll.Offset.X, from);
            }

            return Signal.Empty<ThumbnailEase>();
        }

        if (ReduceMotion || top is null)
        {
            _running = null;
            scroll.Offset = new(scroll.Offset.X, to);
            return Signal.Empty<ThumbnailEase>();
        }

        // A long jump starts one viewport short, so the ease shows direction without sliding past many pages.
        var distance = to - from;
        var viewport = scroll.Viewport.Height;
        var easeFrom = Math.Abs(distance) > viewport ? to - Math.CopySign(viewport, distance) : from;
        scroll.Offset = new(scroll.Offset.X, easeFrom);
        var ease = new ThumbnailEase(this, scroll, easeFrom, to, Stopwatch.GetTimestamp());
        _running = ease;
        return EaseFrames(top, ease);
    }

    /// <summary>Moves an ease one frame on, and ends it once the list has arrived.</summary>
    /// <param name="ease">The ease.</param>
    private void StepEase(ThumbnailEase ease)
    {
        var progress = Math.Min(1, Stopwatch.GetElapsedTime(ease.Started) / EaseDuration);
        var eased = 1 - Math.Pow(1 - progress, EasePower);
        ease.Scroll.Offset = new(ease.Scroll.Offset.X, ease.From + ((ease.To - ease.From) * eased));
        if (progress >= 1 && ReferenceEquals(_running, ease))
        {
            _running = null;
        }
    }

    /// <summary>Where the list was when a new thumbnail was selected.</summary>
    /// <param name="List">The list.</param>
    /// <param name="Scroll">The list's scroll viewer.</param>
    /// <param name="Offset">The offset the list is judged from: where it is, or where its running ease is heading.</param>
    private sealed record FollowStart(ThumbnailListBox List, ScrollViewer Scroll, double Offset);

    /// <summary>Where the list goes to show a newly selected thumbnail.</summary>
    /// <param name="Start">Where the list started from.</param>
    /// <param name="To">The offset that shows the thumbnail.</param>
    private sealed record FollowTarget(FollowStart Start, double To);

    /// <summary>A running ease of the list to a new offset.</summary>
    /// <param name="List">The list.</param>
    /// <param name="Scroll">The list's scroll viewer.</param>
    /// <param name="From">The offset the ease started from.</param>
    /// <param name="To">The offset the ease ends at.</param>
    /// <param name="Started">When the ease started, as a <see cref="Stopwatch"/> timestamp.</param>
    private sealed record ThumbnailEase(ThumbnailListBox List, ScrollViewer Scroll, double From, double To, long Started);
}
