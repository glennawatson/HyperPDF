// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Describes the font /F1 of a one-page test PDF.</summary>
[DebuggerDisplay("FontSpec: {Subtype}")]
internal sealed record FontSpec
{
    /// <summary>Gets the font /Subtype, such as TrueType or Type0.</summary>
    internal string Subtype { get; init; } = "TrueType";

    /// <summary>Gets extra font dictionary entries, such as /Widths or /Encoding.</summary>
    internal string Entries { get; init; } = string.Empty;

    /// <summary>Gets the /BaseFont name.</summary>
    internal string BaseFont { get; init; } = "Test";

    /// <summary>Gets the embedded program, or <see langword="null"/> for a font without a descriptor file.</summary>
    internal byte[]? Program { get; init; }

    /// <summary>Gets the descriptor key of the program: FontFile, FontFile2 or FontFile3.</summary>
    internal string FileKey { get; init; } = "FontFile2";

    /// <summary>Gets extra entries of the program stream, such as /Subtype /Type1C.</summary>
    internal string FileEntries { get; init; } = string.Empty;

    /// <summary>Gets the descriptor /Flags, or a negative number to write no descriptor.</summary>
    internal int Flags { get; init; } = 32;

    /// <summary>Gets extra font descriptor entries, such as /MissingWidth.</summary>
    internal string DescriptorEntries { get; init; } = string.Empty;

    /// <summary>Gets the /ToUnicode CMap source, or <see langword="null"/>.</summary>
    internal string? ToUnicode { get; init; }

    /// <summary>Gets the /Subtype of the descendant of a /Type0 font.</summary>
    internal string CidSubtype { get; init; } = "CIDFontType2";

    /// <summary>Gets extra entries of the descendant of a /Type0 font, such as /W.</summary>
    internal string CidEntries { get; init; } = string.Empty;

    /// <summary>Gets the /CIDToGIDMap stream data of a /Type0 font's descendant, or <see langword="null"/>.</summary>
    internal byte[]? CidToGidMap { get; init; }

    /// <summary>Gets the /Encoding of a /Type0 font: a name such as /Identity-H, or CMap source written as a stream.</summary>
    internal string CidEncoding { get; init; } = "/Identity-H";

    /// <summary>Gets the page content.</summary>
    internal string Content { get; init; } = string.Empty;

    /// <summary>Gets the Adobe character collection of a Type 0 descendant.</summary>
    internal string CidOrdering { get; init; } = "Identity";
}
