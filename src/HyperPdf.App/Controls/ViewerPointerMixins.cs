// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Input;

namespace HyperPdf.App.Controls;

/// <summary>Names Avalonia's pointer buttons and cursors the way the view models do.</summary>
internal static class ViewerPointerMixins
{
    /// <summary>Adds the view model names to a pointer point.</summary>
    /// <param name="point">The pointer point.</param>
    extension(PointerPoint point)
    {
        /// <summary>Gets the buttons held, as the view models name them.</summary>
        internal ViewerButtons ViewerButtons
        {
            get
            {
                var buttons = ViewerButtons.None;
                if (point.Properties.IsLeftButtonPressed)
                {
                    buttons |= ViewerButtons.Left;
                }

                if (point.Properties.IsMiddleButtonPressed)
                {
                    buttons |= ViewerButtons.Middle;
                }

                if (point.Properties.IsRightButtonPressed)
                {
                    buttons |= ViewerButtons.Right;
                }

                return buttons;
            }
        }
    }

    /// <summary>Adds the Avalonia cursor of a pointer shape.</summary>
    /// <param name="cursor">The pointer shape the view models ask for.</param>
    extension(ViewerCursor cursor)
    {
        /// <summary>Gets the Avalonia cursor for the shape.</summary>
        /// <returns>The cursor, or <see langword="null"/> when the pointer should follow what is under it.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Cursor? ToCursor() => cursor switch
        {
            ViewerCursor.Arrow => Cursor.Default,
            ViewerCursor.Hand => PageCanvas.HandCursor,
            ViewerCursor.Cross => PageCanvas.CrossCursor,
            ViewerCursor.Text => PageCanvas.TextCursor,
            ViewerCursor.Move => PageCanvas.MoveCursor,
            _ => null,
        };
    }
}
