// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>An OpenType layout feature to turn on or off when text is shaped (OpenType, "Feature tags").</summary>
/// <param name="Tag">The four-letter feature tag, such as <c>liga</c>, <c>smcp</c> or <c>kern</c>.</param>
/// <param name="Value">Zero turns the feature off; one turns it on. For a feature that offers alternates, such as <c>salt</c>, a larger value picks that alternate.</param>
[DebuggerDisplay("PdfFontFeature: {Tag}={Value}")]
public readonly record struct PdfFontFeature(string Tag, int Value)
{
    /// <summary>Makes a request to turn a feature on.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <returns>The request.</returns>
    public static PdfFontFeature On(string tag) => new(tag, 1);

    /// <summary>Makes a request to turn a feature off.</summary>
    /// <param name="tag">The feature tag.</param>
    /// <returns>The request.</returns>
    public static PdfFontFeature Off(string tag) => new(tag, 0);
}
