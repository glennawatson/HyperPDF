// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>How a bookmark's title is shown: its colour (<c>/C</c>) and style flags (<c>/F</c>), ISO 32000-2 12.3.3, Table 151.</summary>
/// <param name="Red">The red component, from 0 to 1.</param>
/// <param name="Green">The green component, from 0 to 1.</param>
/// <param name="Blue">The blue component, from 0 to 1.</param>
/// <param name="Italic">Whether the title is shown in italics.</param>
/// <param name="Bold">Whether the title is shown in bold.</param>
[DebuggerDisplay("PdfBookmarkStyle: ({Red}, {Green}, {Blue}) italic {Italic} bold {Bold}")]
public sealed record PdfBookmarkStyle(float Red, float Green, float Blue, bool Italic, bool Bold)
{
    /// <summary>Gets the default style: black, neither italic nor bold.</summary>
    public static PdfBookmarkStyle Default { get; } = new(0, 0, 0, false, false);
}
