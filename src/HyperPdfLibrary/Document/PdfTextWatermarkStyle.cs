// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>The font, size and colour of a text watermark.</summary>
[DebuggerDisplay("PdfTextWatermarkStyle: {Font} {FontSize} pt")]
public sealed record PdfTextWatermarkStyle
{
    /// <summary>The default font size in points.</summary>
    private const float DefaultFontSize = 72;

    /// <summary>The default grey level.</summary>
    private const float DefaultGray = 0.5F;

    /// <summary>Gets the built-in font used when <see cref="DocumentFont"/> is not set. Built-in fonts are not embedded, so a document that claims PDF/A refuses them.</summary>
    public AppearanceFont Font { get; init; } = AppearanceFont.HelveticaBold;

    /// <summary>
    /// Gets the font dictionary of a simple (single byte) font that is already in the document, such as one that is embedded;
    /// an invalid id uses <see cref="Font"/>. Every character of the text must be in the font.
    /// </summary>
    public PdfObjectId DocumentFont { get; init; }

    /// <summary>Gets the font size in points.</summary>
    public float FontSize { get; init; } = DefaultFontSize;

    /// <summary>Gets the red level, from 0 to 1.</summary>
    public float Red { get; init; } = DefaultGray;

    /// <summary>Gets the green level, from 0 to 1.</summary>
    public float Green { get; init; } = DefaultGray;

    /// <summary>Gets the blue level, from 0 to 1.</summary>
    public float Blue { get; init; } = DefaultGray;
}
