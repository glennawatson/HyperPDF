// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Json;

/// <summary>How a document is exported as JSON.</summary>
/// <param name="StreamData">What to put in for the data of each stream.</param>
/// <param name="Indented">Whether to lay the JSON out in lines and indents.</param>
[DebuggerDisplay("PdfJsonOptions: {StreamData}")]
public sealed record PdfJsonOptions(PdfJsonStreamData StreamData, bool Indented)
{
    /// <summary>Gets the options an export uses when none are given: stream data decoded, indented.</summary>
    public static PdfJsonOptions Default { get; } = new(PdfJsonStreamData.Decoded, true);
}
