// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

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

    /// <summary>The offset before an unrealized selection is brought into view.</summary>
    private double? _pendingOffset;

    /// <summary>The offset the current ease started from.</summary>
    private double _easeFrom;

    /// <summary>The offset the current ease ends at.</summary>
    private double _easeTo;

    /// <summary>When the current ease started.</summary>
    private long _easeStarted;

    /// <summary>1 while an ease is running.</summary>
    private int _easing;

    /// <summary>Initializes a new instance of the <see cref="ThumbnailListBox"/> class.</summary>
    public ThumbnailListBox()
    {
        AutoScrollToSelectedItem = false;
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.WhenChanged(static x => x.SelectedIndex).Skip(1).SubscribeSafe(_ => OnSelectionChanged(), OnError));
            disposables.Add(Scope.Create(this, static list => list.StopFollowing()));
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

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (_pendingOffset is { } offset && FollowSelection(offset))
        {
            _pendingOffset = null;
        }

        return result;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Stops following the selection.</summary>
    private void StopFollowing()
    {
        _pendingOffset = null;
        _ = Interlocked.Exchange(ref _easing, 0);
    }

    /// <summary>Follows a newly selected thumbnail after it has been realized.</summary>
    private void OnSelectionChanged()
    {
        _pendingOffset = null;
        if (SelectedIndex < 0 || Scroll is not ScrollViewer scroll || scroll.Viewport.Height <= 0)
        {
            return;
        }

        // While easing, the list is judged where it is heading, so a page already on its way into view stays put.
        var offset = Volatile.Read(ref _easing) != 0 ? _easeTo : scroll.Offset.Y;
        if (FollowSelection(offset))
        {
            return;
        }

        _pendingOffset = offset;
        ScrollIntoView(SelectedIndex);
        if (FollowSelection(offset))
        {
            _pendingOffset = null;
        }
    }

    /// <summary>Moves to the selected item when it is not in full view.</summary>
    /// <param name="offset">The offset before the selection changed.</param>
    /// <returns>Whether the selected container has been realized.</returns>
    private bool FollowSelection(double offset)
    {
        if (Scroll is not ScrollViewer scroll || ContainerFromIndex(SelectedIndex) is not { } container
            || container.TranslatePoint(default, scroll) is not { } position)
        {
            return false;
        }

        var top = position.Y + scroll.Offset.Y;
        var next = ThumbnailViewport.GetOffset(offset, scroll.Viewport.Height, top, top + container.Bounds.Height, scroll.Extent.Height);
        if (Math.Abs(next - offset) > OffsetTolerance)
        {
            EaseTo(scroll, offset, next);
        }
        else if (Math.Abs(scroll.Offset.Y - offset) > OffsetTolerance)
        {
            // Realizing the item may have scrolled; the item was already in view, so the list goes back.
            scroll.Offset = new(scroll.Offset.X, offset);
        }

        return true;
    }

    /// <summary>Eases the list to a new offset, or jumps there when motion is reduced.</summary>
    /// <param name="scroll">The scroll viewer.</param>
    /// <param name="from">The offset the reader was looking at.</param>
    /// <param name="to">The new offset.</param>
    private void EaseTo(ScrollViewer scroll, double from, double to)
    {
        if (ReduceMotion || TopLevel.GetTopLevel(this) is not { } top)
        {
            _ = Interlocked.Exchange(ref _easing, 0);
            scroll.Offset = new(scroll.Offset.X, to);
            return;
        }

        // A long jump starts one viewport short, so the ease shows direction without sliding past many pages.
        var distance = to - from;
        var viewport = scroll.Viewport.Height;
        _easeFrom = Math.Abs(distance) > viewport ? to - Math.CopySign(viewport, distance) : from;
        _easeTo = to;
        _easeStarted = Stopwatch.GetTimestamp();
        scroll.Offset = new(scroll.Offset.X, _easeFrom);
        if (Interlocked.Exchange(ref _easing, 1) == 0)
        {
            top.RequestAnimationFrame(OnEaseFrame);
        }
    }

    /// <summary>Moves the ease one frame on.</summary>
    /// <param name="time">The frame time, unused because the ease measures its own start.</param>
    private void OnEaseFrame(TimeSpan time)
    {
        if (Volatile.Read(ref _easing) == 0 || Scroll is not ScrollViewer scroll)
        {
            return;
        }

        var progress = Math.Min(1, Stopwatch.GetElapsedTime(_easeStarted) / EaseDuration);
        var eased = 1 - Math.Pow(1 - progress, EasePower);
        scroll.Offset = new(scroll.Offset.X, _easeFrom + ((_easeTo - _easeFrom) * eased));
        if (progress >= 1)
        {
            _ = Interlocked.Exchange(ref _easing, 0);
            return;
        }

        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnEaseFrame);
    }
}
