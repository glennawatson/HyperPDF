// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Exporting;

/// <summary>How pages are exported.</summary>
/// <param name="Dpi">The resolution of a picture in dots to the inch; drawings ignore it.</param>
/// <param name="IncludeMarkup">Whether comments, form fields and other marks on the page are drawn.</param>
[DebuggerDisplay("PageFileExportOptions: {Dpi} dpi")]
public sealed record PageFileExportOptions(int Dpi, bool IncludeMarkup)
{
    /// <summary>Gets the resolution used when none is chosen.</summary>
    public static int DefaultDpi { get; } = 150;

    /// <summary>Gets the lowest resolution allowed, in dots to the inch.</summary>
    public static int MinimumDpi { get; } = 10;

    /// <summary>Gets the highest resolution allowed, in dots to the inch.</summary>
    public static int MaximumDpi { get; } = 2400;

    /// <summary>Gets the options a window starts with: 150 dots to the inch, with the marks on the page.</summary>
    public static PageFileExportOptions Default { get; } = new(DefaultDpi, true);
}
