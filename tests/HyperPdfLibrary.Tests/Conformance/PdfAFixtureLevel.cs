// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>A PDF/A level a fixture claims.</summary>
/// <param name="Part">The part of ISO 19005.</param>
/// <param name="Conformance">The conformance level letter, or <see langword="null"/> for part 4.</param>
[DebuggerDisplay("PdfAFixtureLevel: {Flavour}")]
internal sealed record PdfAFixtureLevel(int Part, string? Conformance)
{
    /// <summary>The part based on PDF 1.7 without embedded files.</summary>
    internal const int Part2 = 2;

    /// <summary>The part based on PDF 2.0.</summary>
    internal const int Part4 = 4;

    /// <summary>Gets PDF/A-1b.</summary>
    internal static PdfAFixtureLevel A1B => new(1, "B");

    /// <summary>Gets PDF/A-2b.</summary>
    internal static PdfAFixtureLevel A2B => new(Part2, "B");

    /// <summary>Gets PDF/A-4.</summary>
    internal static PdfAFixtureLevel A4 => new(Part4, null);

    /// <summary>Gets the veraPDF flavour name, such as 1b or 4.</summary>
    internal string Flavour => string.Concat(Part.ToString(CultureInfo.InvariantCulture), Conformance?.ToLowerInvariant());

    /// <summary>Gets the claim label HyperPdf reports, such as 1B or 4.</summary>
    internal string Label => string.Concat(Part.ToString(CultureInfo.InvariantCulture), Conformance);
}
