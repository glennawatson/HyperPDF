// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>The entries of a /FontDescriptor that drawing uses (ISO 32000-2, 9.8.1, Table 120).</summary>
/// <param name="Flags">The flags, with Italic added when the italic angle is negative.</param>
/// <param name="HasFlags">Whether the descriptor gives /Flags.</param>
/// <param name="ItalicAngle">The italic angle in degrees.</param>
/// <param name="Ascent">The ascent in glyph units, or zero.</param>
/// <param name="Descent">The descent in glyph units, made negative, or zero.</param>
/// <param name="Weight">The /FontWeight; zero when unknown.</param>
/// <param name="MissingWidth">The /MissingWidth, or <see langword="null"/>.</param>
/// <param name="FontFile">The embedded font program stream, or <see langword="null"/>.</param>
/// <param name="FileKind">The key the program came from.</param>
[DebuggerDisplay("FontDescriptor: {Flags} {FileKind}")]
internal sealed record FontDescriptor(
    FontFlags Flags,
    bool HasFlags,
    float ItalicAngle,
    float Ascent,
    float Descent,
    int Weight,
    float? MissingWidth,
    PdfStream? FontFile,
    FontFileKind FileKind)
{
    /// <summary>A descent larger than this is a positive number written by mistake; Table 120 says /Descent is negative or zero.</summary>
    private const float PositiveDescentLimit = 10;

    /// <summary>The numbers in a /FontBBox.</summary>
    private const int BoxNumbers = 4;

    /// <summary>Gets an empty descriptor for fonts that have none.</summary>
    internal static FontDescriptor Empty { get; } = new(FontFlags.None, false, 0, 0, 0, 0, null, null, FontFileKind.None);

    /// <summary>Gets a value indicating whether the font is embedded.</summary>
    internal bool IsEmbedded => FontFile is not null;

    /// <summary>Reads a font descriptor.</summary>
    /// <param name="descriptor">The descriptor, or <see langword="null"/>.</param>
    /// <returns>The entries.</returns>
    internal static FontDescriptor Read(PdfDictionary? descriptor)
    {
        if (descriptor is null)
        {
            return Empty;
        }

        ReportFaults(descriptor);
        var hasFlags = descriptor.ContainsKey(KnownName.Flags);
        var flags = (FontFlags)descriptor.GetInt32(KnownName.Flags, (int)FontFlags.Nonsymbolic);
        var italicAngle = descriptor.GetSingle(KnownName.ItalicAngle);
        if (italicAngle < 0)
        {
            flags |= FontFlags.Italic;
        }

        var descent = descriptor.GetSingle(KnownName.Descent);
        var file = FindFontFile(descriptor, out var kind);
        return new(
            flags,
            hasFlags,
            italicAngle,
            descriptor.GetSingle(KnownName.Ascent),
            descent > PositiveDescentLimit ? -descent : descent,
            ReadWeight(descriptor),
            descriptor.Get(KnownName.MissingWidth).IsNumber ? descriptor.GetSingle(KnownName.MissingWidth) : null,
            file,
            kind);
    }

    /// <summary>Reports numeric entries of the wrong type and a /FontBBox that is not four numbers; the entries are ignored.</summary>
    /// <param name="descriptor">The descriptor.</param>
    private static void ReportFaults(PdfDictionary descriptor)
    {
        if (descriptor.Owner?.Context is not { } context)
        {
            return;
        }

        ReadOnlySpan<KnownName> numericKeys =
            [KnownName.Flags, KnownName.ItalicAngle, KnownName.Ascent, KnownName.Descent, KnownName.CapHeight, KnownName.StemV, KnownName.MissingWidth, KnownName.FontWeight];
        var faulty = false;
        foreach (var key in numericKeys)
        {
            faulty |= !descriptor.Get(key).IsNull && !descriptor.Get(key).IsNumber;
        }

        Span<float> corners = stackalloc float[BoxNumbers];
        faulty |= !descriptor.Get(KnownName.FontBBox).IsNull && (descriptor.GetArray(KnownName.FontBBox)?.ReadNumbers(corners) ?? 0) < BoxNumbers;
        if (faulty)
        {
            PdfOpenContext.Report(context, PdfDiagnosticCode.BadFontDescriptor, "A font descriptor has entries of the wrong type, which are ignored.", 0, -1);
        }
    }

    /// <summary>Reads the weight from /FontWeight (ISO 32000-2, 9.8.1, Table 120). /StemV is not a weight, so it is not used.</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <returns>The weight, or zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadWeight(PdfDictionary descriptor) => Math.Max(0, descriptor.GetInt32(KnownName.FontWeight));

    /// <summary>Finds the embedded program: /FontFile, then /FontFile2, then /FontFile3.</summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="kind">The key it came from.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    private static PdfStream? FindFontFile(PdfDictionary descriptor, out FontFileKind kind)
    {
        if (descriptor.GetStream(KnownName.FontFile) is { } type1)
        {
            kind = FontFileKind.Type1;
            return type1;
        }

        if (descriptor.GetStream(KnownName.FontFile2) is { } trueType)
        {
            kind = FontFileKind.TrueType;
            return trueType;
        }

        var compact = descriptor.GetStream(KnownName.FontFile3);
        kind = compact is null ? FontFileKind.None : FontFileKind.Compact;
        return compact;
    }
}
