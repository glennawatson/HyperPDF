// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Reads FDF files (ISO 32000-1, 12.7.7). An FDF file has the syntax of a PDF file with an <c>/FDF</c> dictionary in its
/// catalog, so the library's own object parser reads it, repairing a missing cross-reference table as it does for PDF.
/// </summary>
public static class FdfReader
{
    /// <summary>How far into the file the header may start.</summary>
    private const int HeaderSearchLength = 1024;

    /// <summary>Gets the bytes of the FDF header start.</summary>
    private static ReadOnlySpan<byte> Header => "%FDF-"u8;

    /// <summary>Reads an FDF file with the default bounds.</summary>
    /// <param name="fdf">The file's bytes.</param>
    /// <returns>The data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fdf"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not FDF, is damaged, or is too long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfInterchangeData Read(byte[] fdf) => Read(fdf, PdfInterchangeOptions.Default);

    /// <summary>Reads an FDF file.</summary>
    /// <param name="fdf">The file's bytes.</param>
    /// <param name="options">The bounds.</param>
    /// <returns>The data.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not FDF, is damaged, or is too long.</exception>
    public static PdfInterchangeData Read(byte[] fdf, PdfInterchangeOptions options)
    {
        ArgumentNullException.ThrowIfNull(fdf);
        ArgumentNullException.ThrowIfNull(options);
        if (fdf.Length > options.MaxLength)
        {
            throw new PdfException(PdfError.Format, "The FDF file is longer than the limit.");
        }

        var header = fdf.AsSpan(0, Math.Min(fdf.Length, HeaderSearchLength)).IndexOf(Header);
        if (header < 0)
        {
            throw new PdfException(PdfError.Format, "The file is not an FDF document.");
        }

        using var store = StoreOpening.OpenFdf(fdf);
        var root = store.Catalog.GetDictionary(store.Names.Intern("FDF")) ?? throw new PdfException(PdfError.Format, "The FDF file has no FDF dictionary.");
        var data = new PdfInterchangeData();
        FdfDictionaryReader.Read(root, data);
        return data;
    }
}
