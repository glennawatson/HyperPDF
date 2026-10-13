// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>One choice of a combo box or list box (<c>/Opt</c>, ISO 32000-2 12.7.4.4).</summary>
/// <param name="Value">The export value the field holds when the choice is selected.</param>
/// <param name="Label">The text shown to the user; empty to show the value.</param>
[DebuggerDisplay("PdfFieldOption: {Value} {Label}")]
public readonly record struct PdfFieldOption(string Value, string Label)
{
    /// <summary>Makes a choice that shows its own value.</summary>
    /// <param name="value">The value and the text shown.</param>
    /// <returns>The choice.</returns>
    public static PdfFieldOption FromValue(string value) => new(value, string.Empty);
}
