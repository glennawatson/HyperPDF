// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Controls;

/// <summary>
/// Reactive streams and key helpers for the page tools on <see cref="PageCanvas"/>: auto-scroll, holding Space to move the
/// pages, and the keys of the Measure and area tools. What a pointer does with them is decided by the presentation layer.
/// </summary>
internal static class PageCanvasToolMixins
{
    /// <summary>Adds the page tool, auto-scroll and Space streams and helpers to a page canvas.</summary>
    /// <param name="canvas">The page canvas.</param>
    extension(PageCanvas canvas)
    {
        /// <summary>Gets the Space key releases while Space was held on the canvas.</summary>
        internal IObservable<KeyEventArgs> SpaceKeyUps => canvas.Events().KeyUp
            .WhereWith(canvas, static (c, e) => e.Key == Key.Space && c.SpaceHeld);

        /// <summary>Gets the animation frames while auto-scroll is active on the current tab.</summary>
        /// <param name="tabs">The tab stream.</param>
        /// <returns>The frame timestamps while auto-scrolling.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<TimeSpan> AutoScrollFrames(IObservable<DocumentTabViewModel?> tabs) =>
            tabs.Follow(static tab => tab.AutoScrolling)
                .DistinctUntilChanged()
                .SelectWith(canvas, static (c, on) => on ? Signal.Defer(c.StartGlide) : Signal.Never<TimeSpan>())
                .SwitchMap(static frames => frames);

        /// <summary>
        /// Starts a glide: the keyboard comes to the pages so the arrows change the speed and Escape stops, and the window's
        /// frames move the pages until the subscription ends.
        /// </summary>
        /// <returns>The frames, or none when the canvas is not in a window.</returns>
        internal IObservable<TimeSpan> StartGlide()
        {
            if (TopLevel.GetTopLevel(canvas) is not { } top)
            {
                return Signal.Never<TimeSpan>();
            }

            _ = canvas.Focus();
            canvas.AutoScroller.Reset();
            canvas.AutoScrollLast = Stopwatch.GetTimestamp();
            _ = canvas.SetAutoScrollRunning(1);

            // Finally has no state-passing form, so the end of the glide reaches the flag through this closure.
            return top.AnimationFrames.Finally(() => canvas.SetAutoScrollRunning(0));
        }

        /// <summary>Moves the pages on by the time since the last frame, and ends auto-scroll at the end of the document.</summary>
        internal void StepAutoScroll()
        {
            if (canvas.Scroller is null || canvas.Tab is not { PageTools.IsAutoScrolling: true } tab)
            {
                return;
            }

            var now = Stopwatch.GetTimestamp();
            var seconds = Stopwatch.GetElapsedTime(canvas.AutoScrollLast, now).TotalSeconds;
            canvas.AutoScrollLast = now;
            var distance = canvas.AutoScroller.Advance(seconds, tab.PageTools.AutoScrollSpeed, tab.ReduceMotion);
            if (distance > 0 && !canvas.AutoScrollBy(distance))
            {
                // Turning auto-scroll off ends the glide's frames.
                tab.PageTools.IsAutoScrolling = false;
            }
        }

        /// <summary>Scrolls down, page by page within the page shown and then to the next page.</summary>
        /// <param name="distance">How far.</param>
        /// <returns><see langword="true"/> when the pages moved; <see langword="false"/> at the end.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool AutoScrollBy(double distance) => canvas.State.AutoScrollBy(distance);

        /// <summary>Handles letting go of Space: the presentation layer decides, and the canvas does what it asks.</summary>
        /// <param name="e">The key release.</param>
        internal void CompleteSpaceHold(KeyEventArgs e)
        {
            canvas.ApplyKeyEffects(canvas.State.ReleaseKey(e.ViewerKey, e.ViewerModifiers));
            e.Handled = true;
        }

        /// <summary>Forgets a held Space when the pages lose the keyboard, as its release will not arrive.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseSpace() => canvas.State.ReleaseSpace();

        /// <summary>Gets the pointer for the chosen tool when it is not over anything special.</summary>
        /// <returns>The cursor, or <see langword="null"/> for select text, whose pointer follows what is under it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Cursor? ToolCursor() => canvas.State.GetToolCursor().ToCursor();

    }
}
