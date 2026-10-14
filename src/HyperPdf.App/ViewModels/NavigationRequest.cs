// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.ViewModels;

/// <summary>The latest place the reader asked the pages to scroll to.</summary>
/// <param name="Version">Counts the requests, so asking twice for the same place is a new value.</param>
/// <param name="PageIndex">The zero based destination page.</param>
/// <param name="Target">An area of the page to bring into view, or <see langword="null"/> for the top of the page.</param>
/// <param name="OffsetFraction">The vertical position within the page from 0 to 1, used when <paramref name="Target"/> is null.</param>
[DebuggerDisplay("NavigationRequest: Page {PageIndex}")]
public readonly record struct NavigationRequest(int Version, int PageIndex, PageRect? Target, double OffsetFraction)
{
    /// <summary>Gets a value indicating whether this is a request, rather than the state before the first one.</summary>
    public bool IsRequest => Version > 0;

    /// <summary>Gets the request that follows this one.</summary>
    /// <param name="pageIndex">The zero based destination page.</param>
    /// <param name="target">An area of the page to bring into view, or <see langword="null"/> for the top of the page.</param>
    /// <param name="offsetFraction">The vertical position within the page from 0 to 1, used when <paramref name="target"/> is null.</param>
    /// <returns>The next request.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NavigationRequest Next(int pageIndex, PageRect? target, double offsetFraction) => new(Version + 1, pageIndex, target, offsetFraction);
}
