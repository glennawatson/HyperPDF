// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>Gives the page-to-canvas mapping Avalonia's points and rectangles.</summary>
internal static class PageTransformMixins
{
    /// <summary>Adds the Avalonia conversions to a page transform.</summary>
    /// <param name="transform">The transform.</param>
    extension(PageTransform transform)
    {
        /// <summary>Converts a page point to the canvas.</summary>
        /// <param name="point">The page point.</param>
        /// <returns>The canvas point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Point ToCanvas(PagePoint point)
        {
            var mapped = transform.MapToCanvas(point);
            return new(mapped.X, mapped.Y);
        }

        /// <summary>Converts a page rectangle to the canvas.</summary>
        /// <param name="rect">The page rectangle.</param>
        /// <returns>The canvas rectangle.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Rect ToCanvas(PageRect rect)
        {
            var mapped = transform.MapToCanvas(rect);
            return new(mapped.X, mapped.Y, mapped.Width, mapped.Height);
        }

        /// <summary>Converts a canvas point to page space.</summary>
        /// <param name="point">The canvas point.</param>
        /// <returns>The page point.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal PagePoint ToPage(Point point) => transform.MapToPage(point.X, point.Y);
    }
}
