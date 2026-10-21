// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// The latest change a part of a tab made to what is drawn on the pages, such as an annotation added or a field filled
/// in. The tab follows each part's latest change and redraws the pages it names.
/// </summary>
/// <param name="Version">Counts the part's changes, so a second change to the same page is a new value.</param>
/// <param name="PageIndex">The zero based page that changed, or <see cref="AllPages"/>.</param>
[DebuggerDisplay("PageChange: {Version} on page {PageIndex}")]
public readonly record struct PageChange(int Version, int PageIndex)
{
    /// <summary>Gets the page index that stands for every page.</summary>
    public static int AllPages => -1;

    /// <summary>Gets a value indicating whether this is a change, rather than the state before the first one.</summary>
    public bool IsChange => Version > 0;

    /// <summary>Gets the change that follows this one.</summary>
    /// <param name="pageIndex">The page that changed, or <see cref="AllPages"/>.</param>
    /// <returns>The next change.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PageChange Next(int pageIndex) => new(Version + 1, pageIndex);
}
