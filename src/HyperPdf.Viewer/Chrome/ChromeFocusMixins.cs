// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Moves the keyboard between the window's controls. Each move makes the place the keyboard is at next.</summary>
internal static class ChromeFocusMixins
{
    /// <summary>Operators over where the keyboard is.</summary>
    /// <param name="focus">Where the keyboard is.</param>
    extension(ChromeFocus focus)
    {
        /// <summary>Gets the control the keyboard is on.</summary>
        /// <param name="items">The controls of the list the keyboard is in.</param>
        /// <returns>The control, or <see langword="null"/> while the keyboard is on the page.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ChromeItem? Item(List<ChromeItem> items) => (uint)focus.Index < (uint)items.Count ? items[focus.Index] : null;

        /// <summary>Moves the keyboard to the next control it can stop on, wrapping round at the ends.</summary>
        /// <param name="items">The controls.</param>
        /// <param name="inOverlay">Whether the controls are those of the open menu, dialog or search bar.</param>
        /// <param name="step">1 for the next control, -1 for the one before.</param>
        /// <returns>Where the keyboard is after, or <see langword="null"/> when there is no control it can stop on.</returns>
        internal ChromeFocus? Move(List<ChromeItem> items, bool inOverlay, int step)
        {
            // A keyboard that was somewhere else starts before the first control, or after the last one when going backwards.
            var at = focus.InOverlay == inOverlay && (uint)focus.Index < (uint)items.Count ? focus.Index : -1;
            if (at < 0 && step < 0)
            {
                at = items.Count;
            }

            for (var tries = 0; tries < items.Count; tries++)
            {
                at = (at + step + items.Count) % items.Count;
                if (ChromeFocus.CanStopOn(items[at]))
                {
                    return ChromeFocus.At(items, inOverlay, at);
                }
            }

            return null;
        }

        /// <summary>Finds the control the keyboard was on in a list that was laid out again.</summary>
        /// <param name="items">The controls, as they are now.</param>
        /// <returns>Where the keyboard is after: on the same control, or the nearest one left when it is gone.</returns>
        internal ChromeFocus Follow(List<ChromeItem> items)
        {
            if (focus.Index < 0)
            {
                return focus;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (ReferenceEquals(item.Command, focus.Command) && item.Action == focus.Action && item.Index == focus.Target && item.Kind == focus.Kind && ChromeFocus.CanStopOn(item))
                {
                    return focus with { Index = i };
                }
            }

            // The control is gone, as a button is when its tool closes. The keyboard goes to the nearest control that is left.
            var before = focus with { Index = Math.Min(focus.Index, items.Count) - 1 };
            return before.Move(items, focus.InOverlay, 1) ?? ChromeFocus.None;
        }
    }
}
