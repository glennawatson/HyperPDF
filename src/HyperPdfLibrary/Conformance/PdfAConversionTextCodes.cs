// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>The codes one font shows.</summary>
[DebuggerDisplay("PdfAConversionTextCodes: {Visible.Count} visible codes")]
internal sealed class PdfAConversionTextCodes
{
    /// <summary>Gets the codes shown visibly.</summary>
    internal HashSet<int> Visible { get; } = [];

    /// <summary>Gets or sets a value indicating whether the font also shows invisible text.</summary>
    internal bool Invisible { get; set; }
}
