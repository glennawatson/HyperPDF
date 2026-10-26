// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Exporting;

/// <summary>What the export window tells people about a file format, and how the exporter uses it.</summary>
/// <param name="Format">The format.</param>
/// <param name="NameKey">The format's name.</param>
/// <param name="DescriptionKey">What the format is good for, in plain words.</param>
/// <param name="Extension">The file name ending, with its dot.</param>
/// <param name="IsRaster">Whether the page is drawn to pixels at a resolution; vector formats ignore the resolution.</param>
/// <param name="AllowsSeveralPages">Whether one file can hold more than one page.</param>
[DebuggerDisplay("PageFileFormatInfo: {Name}")]
public sealed record PageFileFormatInfo(PageFileFormat Format, string NameKey, string DescriptionKey, string Extension, bool IsRaster, bool AllowsSeveralPages)
{
    /// <summary>Gets the translated format name.</summary>
    public string Name => UiText.Get(NameKey);

    /// <summary>Gets the translated format description.</summary>
    public string Description => UiText.Get(DescriptionKey);
}
