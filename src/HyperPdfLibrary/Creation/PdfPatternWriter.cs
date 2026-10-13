// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Adds patterns (ISO 32000-2 8.7) to a document as reusable resources: shading patterns that fill with a shading
/// (8.7.4) and coloured tiling patterns that repeat a content stream (8.7.3).
/// </summary>
public static class PdfPatternWriter
{
    /// <summary>The <c>/PatternType</c> of a tiling pattern.</summary>
    private const int TilingPattern = 1;

    /// <summary>The <c>/PatternType</c> of a shading pattern.</summary>
    private const int ShadingPattern = 2;

    /// <summary>The <c>/PaintType</c> of a pattern whose cell paints its own colours.</summary>
    private const int ColoredPaint = 1;

    /// <summary>The <c>/TilingType</c> that keeps the cell's spacing constant (8.7.3.1).</summary>
    private const int ConstantSpacing = 1;

    /// <summary>The entries of a pattern dictionary.</summary>
    private const int PatternEntries = 9;

    /// <summary>The numbers in a matrix.</summary>
    private const int MatrixLength = 6;

    /// <summary>Adds a pattern that fills with a shading.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="shading">The shading dictionary's object, from <see cref="PdfShadingWriter"/>.</param>
    /// <param name="matrix">The six-number matrix from pattern space to the page's default space, or empty for the identity.</param>
    /// <returns>The pattern's object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The shading is not valid, or the matrix is neither empty nor six numbers.</exception>
    public static PdfObjectId AddShadingPattern(IPdfObjectSink sink, PdfObjectId shading, ReadOnlySpan<float> matrix)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (!shading.IsValid)
        {
            throw new ArgumentException("The shading is not valid.", nameof(shading));
        }

        var pattern = new PdfDictionary(sink.Owner, PatternEntries);
        pattern.Set(KnownName.Type, PdfValue.FromName(KnownName.Pattern));
        pattern.Set(KnownName.PatternType, PdfValue.FromInteger(ShadingPattern));
        pattern.Set(KnownName.Shading, PdfValue.FromReference(shading));
        SetMatrix(sink, pattern, matrix);
        return sink.Add(PdfValue.FromDictionary(pattern));
    }

    /// <summary>Adds a coloured tiling pattern that repeats a cell drawn by a content stream.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="cell">The cell's bounding box in pattern space.</param>
    /// <param name="horizontalStep">The horizontal distance between cells; not zero.</param>
    /// <param name="verticalStep">The vertical distance between cells; not zero.</param>
    /// <param name="content">The operators that draw one cell.</param>
    /// <param name="resources">The resources the content uses.</param>
    /// <param name="matrix">The six-number matrix from pattern space to the page's default space, or empty for the identity.</param>
    /// <returns>The pattern's object.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The cell is empty, a step is zero, or the matrix is neither empty nor six numbers.</exception>
    public static PdfObjectId AddTilingPattern(
        IPdfObjectSink sink,
        in PdfRectangle cell,
        float horizontalStep,
        float verticalStep,
        ReadOnlySpan<byte> content,
        PdfPageResources resources,
        ReadOnlySpan<float> matrix)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(resources);
        if (cell.IsEmpty || horizontalStep == 0F || verticalStep == 0F || !float.IsFinite(horizontalStep) || !float.IsFinite(verticalStep))
        {
            throw new ArgumentException("A tiling pattern needs a cell with an area and non-zero steps.");
        }

        var pattern = new PdfDictionary(sink.Owner, PatternEntries);
        pattern.Set(KnownName.Type, PdfValue.FromName(KnownName.Pattern));
        pattern.Set(KnownName.PatternType, PdfValue.FromInteger(TilingPattern));
        pattern.Set(KnownName.PaintType, PdfValue.FromInteger(ColoredPaint));
        pattern.Set(KnownName.TilingType, PdfValue.FromInteger(ConstantSpacing));
        pattern.Set(KnownName.BBox, PdfValue.FromArray(cell.ToArray(sink.Owner)));
        pattern.Set(KnownName.XStep, PdfValue.FromReal(horizontalStep));
        pattern.Set(KnownName.YStep, PdfValue.FromReal(verticalStep));
        pattern.Set(KnownName.Resources, PdfValue.FromDictionary(resources.ToDictionary()));
        SetMatrix(sink, pattern, matrix);
        return sink.Add(PdfValue.FromStream(CreatedStreams.CompressIfSmaller(pattern, content)));
    }

    /// <summary>Sets a pattern's <c>/Matrix</c> when one is given.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="pattern">The pattern dictionary.</param>
    /// <param name="matrix">The matrix, or empty.</param>
    /// <exception cref="ArgumentException">The matrix is neither empty nor six numbers.</exception>
    private static void SetMatrix(IPdfObjectSink sink, PdfDictionary pattern, ReadOnlySpan<float> matrix)
    {
        if (matrix.IsEmpty)
        {
            return;
        }

        if (matrix.Length != MatrixLength)
        {
            throw new ArgumentException("A matrix has six numbers.", nameof(matrix));
        }

        pattern.Set(KnownName.Matrix, PdfValue.FromArray(PdfArray.FromNumbers(sink.Owner, matrix)));
    }
}
