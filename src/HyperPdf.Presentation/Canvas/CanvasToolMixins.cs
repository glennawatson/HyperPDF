// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>Moving the pages by auto-scroll, hand panning, Space and area drags, for any toolkit's canvas.</summary>
public static class CanvasToolMixins
{
    /// <summary>Adds the page tool helpers to a canvas state.</summary>
    /// <param name="state">The canvas state.</param>
    extension(PageCanvasState state)
    {
        /// <summary>Scrolls down, page by page within the page shown and then to the next page.</summary>
        /// <param name="distance">How far.</param>
        /// <returns><see langword="true"/> when the pages moved; <see langword="false"/> at the end.</returns>
        public bool AutoScrollBy(double distance)
        {
            var host = state.Host;
            if (!host.HasScroller)
            {
                return false;
            }

            var before = host.ScrollOffset.Y;
            if (state.StepPageByPage(distance))
            {
                return host.ScrollOffset.Y > before;
            }

            var end = Math.Max(0, host.ExtentSize.Height - host.ViewportSize.Height);
            if (before >= end - CanvasMetrics.EdgeTolerance)
            {
                return false;
            }

            host.ScrollOffset = new(host.ScrollOffset.X, Math.Min(end, before + distance));
            state.ReportPosition();
            return true;
        }

        /// <summary>Moves the pages with the pointer while panning.</summary>
        /// <param name="pointer">Where the pointer is in the scrolling area.</param>
        /// <returns><see langword="true"/> while the pages are being moved.</returns>
        public bool ContinuePan(CanvasPoint pointer)
        {
            var tools = state.Tools;
            var host = state.Host;
            if (!tools.Panning || !host.HasScroller)
            {
                return false;
            }

            var movedX = pointer.X - tools.PanStart.X;
            var y = tools.PanOffset.Y - (pointer.Y - tools.PanStart.Y);
            if (state.Layout.Options.PageByPage && state.Layout.PageCount > 0)
            {
                // Page by page, a drag moves within the page shown and never shows half of the next one.
                y = state.ClampToSlot(state.Layout.GetPageNearest(tools.PanOffset.Y + CanvasMetrics.EdgeTolerance), y);
            }

            host.ScrollOffset = new(tools.PanOffset.X - movedX, y);
            return true;
        }

        /// <summary>Starts moving the pages from where the pointer is now.</summary>
        /// <param name="pointer">Where the pointer is in the scrolling area.</param>
        public void BeginPan(CanvasPoint pointer)
        {
            var tools = state.Tools;
            tools.Panning = true;
            tools.PanStart = pointer;
            tools.PanOffset = state.Host.ScrollOffset;
            if (tools.SpaceHeld)
            {
                tools.SpacePanned = true;
            }
        }

        /// <summary>Notes that Space is held, so a drag moves the pages.</summary>
        /// <param name="key">The key.</param>
        /// <param name="modifiers">The modifiers held with it.</param>
        /// <param name="started">Whether this press started the hold, as held keys repeat.</param>
        /// <returns><see langword="true"/> when the key was Space.</returns>
        public bool PressSpace(ViewerKey key, ViewerModifiers modifiers, out bool started)
        {
            started = false;
            if (key != ViewerKey.Space || (modifiers & ~ViewerModifiers.Shift) != ViewerModifiers.None)
            {
                return false;
            }

            var tools = state.Tools;
            if (!tools.SpaceHeld)
            {
                tools.SpaceHeld = true;
                tools.SpacePanned = false;
                started = true;
            }

            return true;
        }

        /// <summary>Ends a Space hold.</summary>
        /// <returns><see langword="true"/> when page by page should turn the page, because the pages were not moved while Space was held.</returns>
        public bool EndSpaceHold()
        {
            var tools = state.Tools;
            tools.SpaceHeld = false;
            return !tools.SpacePanned && !tools.Panning;
        }

        /// <summary>Forgets a held Space when the pages lose the keyboard, as its release will not arrive.</summary>
        public void ReleaseSpace()
        {
            var tools = state.Tools;
            tools.SpaceHeld = false;
            tools.SpacePanned = false;
        }

        /// <summary>Gets the area between where the drag started and where it is now.</summary>
        /// <returns>The area, whichever way the drag went.</returns>
        public LayoutRect GetArea()
        {
            var tools = state.Tools;
            var left = Math.Min(tools.AreaStart.X, tools.AreaEnd.X);
            var top = Math.Min(tools.AreaStart.Y, tools.AreaEnd.Y);
            return new(left, top, Math.Abs(tools.AreaEnd.X - tools.AreaStart.X), Math.Abs(tools.AreaEnd.Y - tools.AreaStart.Y));
        }
    }
}
