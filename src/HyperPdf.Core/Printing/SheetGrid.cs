// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Printing;

/// <summary>Arranges several pages on one sheet, the way print dialogs do.</summary>
public static class SheetGrid
{
    /// <summary>A4 width in points.</summary>
    private const float A4Width = 595.28F;

    /// <summary>A4 height in points.</summary>
    private const float A4Height = 841.89F;

    /// <summary>Letter width in points.</summary>
    private const float LetterWidth = 612F;

    /// <summary>Letter height in points.</summary>
    private const float LetterHeight = 792F;

    /// <summary>A3 height in points; its width is the A4 height.</summary>
    private const float A3Height = 1190.55F;

    /// <summary>A5 width in points; its height is the A4 width.</summary>
    private const float A5Width = 419.53F;

    /// <summary>Legal height in points; its width is the Letter width.</summary>
    private const float LegalHeight = 1008F;

    /// <summary>Tabloid height in points; its width is the Letter height.</summary>
    private const float TabloidHeight = 1224F;

    /// <summary>Two pages side by side.</summary>
    private const int Two = 2;

    /// <summary>A two by two grid.</summary>
    private const int Four = 4;

    /// <summary>A three by two grid.</summary>
    private const int Six = 6;

    /// <summary>A three by three grid.</summary>
    private const int Nine = 9;

    /// <summary>A four by four grid.</summary>
    private const int Sixteen = 16;

    /// <summary>Three columns or rows.</summary>
    private const int Three = 3;

    /// <summary>Gets the choices offered for pages per sheet.</summary>
    public static IReadOnlyList<int> Choices { get; } = [1, Two, Four, Six, Nine, Sixteen];

    /// <summary>Gets the grid for a number of pages per sheet; two and six pages turn the sheet sideways.</summary>
    /// <param name="pagesPerSheet">The pages per sheet.</param>
    /// <param name="columns">The columns.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="landscape">Whether the sheet is sideways.</param>
    public static void GetGrid(int pagesPerSheet, out int columns, out int rows, out bool landscape) =>
        (columns, rows, landscape) = pagesPerSheet switch
        {
            Two => (Two, 1, true),
            Four => (Two, Two, false),
            Six => (Three, Two, true),
            Nine => (Three, Three, false),
            Sixteen => (Four, Four, false),
            _ => (1, 1, false),
        };

    /// <summary>Gets a sheet's size in points, sideways when the grid asks for it.</summary>
    /// <param name="paper">The paper.</param>
    /// <param name="landscape">Whether the sheet is sideways.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public static void GetSheetSize(PaperSize paper, bool landscape, out float width, out float height)
    {
        (width, height) = paper switch
        {
            PaperSize.Letter => (LetterWidth, LetterHeight),
            PaperSize.A3 => (A4Height, A3Height),
            PaperSize.A5 => (A5Width, A4Width),
            PaperSize.Legal => (LetterWidth, LegalHeight),
            PaperSize.Tabloid => (LetterHeight, TabloidHeight),
            _ => (A4Width, A4Height),
        };
        if (landscape)
        {
            (width, height) = (height, width);
        }
    }

    /// <summary>Gets the number of sheets a number of pages fills.</summary>
    /// <param name="pageCount">The pages.</param>
    /// <param name="pagesPerSheet">The pages per sheet.</param>
    /// <returns>The sheets.</returns>
    public static int GetSheetCount(int pageCount, int pagesPerSheet) => (pageCount + Math.Max(1, pagesPerSheet) - 1) / Math.Max(1, pagesPerSheet);
}
