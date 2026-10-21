// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Reading;

/// <summary>The place the Focus Mode view was last asked to bring into view.</summary>
/// <param name="Version">Counts the requests, so asking for the same place twice is two requests; 0 before the first.</param>
/// <param name="PageIndex">The page.</param>
/// <param name="Fraction">How far down the page, from 0 (top) to 1 (bottom); the block nearest it is shown at the top.</param>
[DebuggerDisplay("FocusScrollRequest {Version}: Page {PageIndex} at {Fraction}")]
public readonly record struct FocusScrollRequest(int Version, int PageIndex, double Fraction)
{
    /// <summary>Gets a value indicating whether this is a request, rather than the empty value before the first one.</summary>
    public bool IsRequest => Version > 0;

    /// <summary>Makes the request that follows this one.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="fraction">How far down the page, from 0 to 1.</param>
    /// <returns>The next request.</returns>
    public FocusScrollRequest Next(int pageIndex, double fraction) => new(Version + 1, pageIndex, fraction);
}
