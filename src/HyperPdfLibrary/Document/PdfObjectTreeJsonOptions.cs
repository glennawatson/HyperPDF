// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>What <see cref="PdfObjectTreeJson"/> writes.</summary>
[DebuggerDisplay("PdfObjectTreeJsonOptions: decoded stream bytes {MaxDecodedBytes}, indented {Indented}")]
public sealed record PdfObjectTreeJsonOptions
{
    /// <summary>Gets the most decoded bytes written for each stream, as Base64; zero writes only the stream's length and filters.</summary>
    public int MaxDecodedBytes { get; init; }

    /// <summary>Gets a value indicating whether the JSON is indented.</summary>
    public bool Indented { get; init; } = true;
}
