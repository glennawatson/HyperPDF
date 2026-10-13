// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Xml;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Reads XFDF (ISO 19444-1) files. The XML is read forward-only with <see cref="XmlReader"/>; a document type
/// declaration is refused, no resolver is set, and the length is bounded, so entity and size attacks fail early.
/// </summary>
public static class XfdfReader
{
    /// <summary>The reader settings for the default bound, shared because a reader only reads them.</summary>
    private static readonly XmlReaderSettings DefaultSettings = CreateSettings(PdfInterchangeOptions.Default.MaxLength);

    /// <summary>Reads an XFDF file with the default bounds.</summary>
    /// <param name="xml">The file's bytes, in any encoding XML allows.</param>
    /// <returns>The data.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="xml"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not well formed XFDF, has a document type declaration, or is too long.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfInterchangeData Read(byte[] xml) => Read(xml, PdfInterchangeOptions.Default);

    /// <summary>Reads an XFDF file.</summary>
    /// <param name="xml">The file's bytes, in any encoding XML allows.</param>
    /// <param name="options">The bounds.</param>
    /// <returns>The data.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not well formed XFDF, has a document type declaration, or is too long.</exception>
    public static PdfInterchangeData Read(byte[] xml, PdfInterchangeOptions options)
    {
        ArgumentNullException.ThrowIfNull(xml);
        ArgumentNullException.ThrowIfNull(options);
        if (xml.Length > options.MaxLength)
        {
            throw new PdfException(PdfError.Format, "The XFDF file is longer than the limit.");
        }

        using MemoryStream stream = new(xml, writable: false);
        return Read(stream, options);
    }

    /// <summary>Reads an XFDF file from a stream; the stream is left open.</summary>
    /// <param name="stream">The stream, at the start of the file.</param>
    /// <param name="options">The bounds.</param>
    /// <returns>The data.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">The file is not well formed XFDF, has a document type declaration, or is too long.</exception>
    public static PdfInterchangeData Read(Stream stream, PdfInterchangeOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.MaxLength == PdfInterchangeOptions.Default.MaxLength ? DefaultSettings : CreateSettings(options.MaxLength);
        var data = new PdfInterchangeData();
        try
        {
            using var reader = XmlReader.Create(stream, settings);
            new XfdfParser(reader, data).Parse();
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException)
        {
            throw new PdfException($"The XFDF file is not well formed: {exception.Message}", exception);
        }

        return data;
    }

    /// <summary>Makes reader settings that refuse type declarations and bound the length.</summary>
    /// <param name="maxLength">The most characters read.</param>
    /// <returns>The settings.</returns>
    private static XmlReaderSettings CreateSettings(long maxLength) => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = maxLength,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        CloseInput = false,
    };
}
