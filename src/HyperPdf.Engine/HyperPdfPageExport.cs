// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdf.Core.Printing;
using HyperPdfLibrary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;
namespace HyperPdf.Engine;

/// <summary>Implements PageExport over the document's owned state.</summary>
internal static class HyperPdfPageExport
{
    /// <summary>The fraction of a poster tile shared with its neighbour.</summary>
    internal const float PosterOverlap = 0.04F;

    /// <summary>The print margin in points.</summary>
    internal const float PrintMargin = 18F;

    /// <summary>The number of margins on each axis.</summary>
    internal const int MarginsPerAxis = 2;

    /// <summary>The number of pages across a booklet sheet.</summary>
    internal const int BookletColumns = 2;

    /// <summary>A quarter turn in degrees.</summary>
    internal const int QuarterTurn = 90;

    /// <summary>Three quarters of a turn in degrees.</summary>
    internal const int ThreeQuarterTurn = 270;

    /// <summary>Works out the part of a page's media box one poster tile shows, a little larger than an even share so neighbours overlap.</summary>
    /// <param name="media">The media box.</param>
    /// <param name="tile">The tile, left to right then top to bottom.</param>
    /// <param name="tiles">The tiles across and down.</param>
    /// <returns>The tile's box.</returns>
    internal static PdfRectangle GetTileBox(in PdfRectangle media, int tile, int tiles)
    {
        var width = media.Width / tiles;
        var height = media.Height / tiles;
        var column = tile % tiles;
        var row = tile / tiles;
        return new(
            Math.Max(media.Left, media.Left + (column * width) - (width * PosterOverlap)),
            Math.Max(media.Bottom, media.Top - ((row + 1) * height) - (height * PosterOverlap)),
            Math.Min(media.Right, media.Left + ((column + 1) * width) + (width * PosterOverlap)),
            Math.Min(media.Top, media.Top - (row * height) + (height * PosterOverlap)));
    }

    /// <summary>Places a page in a cell of a sheet, as large as fits, centred.</summary>
    /// <param name="item">The page.</param>
    /// <param name="slot">The cell, left to right then top to bottom.</param>
    /// <param name="columns">The cells across.</param>
    /// <param name="rows">The cells down.</param>
    /// <param name="cell">The size of a cell.</param>
    /// <returns>The placement.</returns>
    internal static PdfFormPlacement Place(in SheetPage item, int slot, int columns, int rows, Vector2 cell)
    {
        var x = (slot % columns) * cell.X;
        var y = (rows - (slot / columns) - 1) * cell.Y;
        var acrossFit = cell.X / item.Width;
        var downFit = cell.Y / item.Height;
        var scale = Math.Min(acrossFit, downFit);
        if (acrossFit > downFit)
        {
            x += (cell.X - (item.Width * scale)) / MarginsPerAxis;
        }
        else
        {
            y += (cell.Y - (item.Height * scale)) / MarginsPerAxis;
        }

        return new(item.Form, new(scale, 0, 0, scale, x, y));
    }

    /// <summary>Works out where a page's box lands on paper, centred and scaled as chosen.</summary>
    /// <param name="form">The page's form, in user space.</param>
    /// <param name="page">The page.</param>
    /// <param name="paper">The upright paper size.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="sheet">The sheet size in the page's unrotated space; turned from upright for wide content.</param>
    /// <returns>The placement. The page keeps its rotation, which does part of the turning for wide content.</returns>
    internal static PdfFormPlacement GetFitPlacement(PdfObjectId form, PdfPage page, Vector2 paper, in SheetLayout layout, out Vector2 sheet)
    {
        var box = page.CropBox;
        var turned = page.Rotation is QuarterTurn or ThreeQuarterTurn;
        var wide = turned ? box.Height > box.Width : box.Width > box.Height;
        sheet = wide != turned ? new(paper.Y, paper.X) : paper;
        const float Margins = MarginsPerAxis * PrintMargin;
        var scale = PrintScale.For(layout.Scaling, layout.ScalePercent, box.Width, box.Height, sheet.X - Margins, sheet.Y - Margins);
        var x = ((sheet.X - (box.Width * scale)) / MarginsPerAxis) - (box.Left * scale);
        var y = ((sheet.Y - (box.Height * scale)) / MarginsPerAxis) - (box.Bottom * scale);
        return new(form, new(scale, 0, 0, scale, x, y));
    }

    /// <summary>Puts the pages on sheets, as many to a sheet as the grid has cells.</summary>
    /// <param name="builder">The new document.</param>
    /// <param name="items">The pages in order; <see langword="null"/> leaves a cell blank.</param>
    /// <param name="columns">The cells across.</param>
    /// <param name="rows">The cells down.</param>
    /// <param name="sheet">The sheet size in points.</param>
    /// <returns>The number of sheets added.</returns>
    internal static int LaySheets(PdfDocumentBuilder builder, List<SheetPage?> items, int columns, int rows, Vector2 sheet)
    {
        var perSheet = columns * rows;
        var cell = new Vector2(sheet.X / columns, sheet.Y / rows);
        var placements = new List<PdfFormPlacement>(perSheet);
        var sheets = 0;
        for (var start = 0; start < items.Count; start += perSheet)
        {
            placements.Clear();
            for (var i = start; i < Math.Min(start + perSheet, items.Count); i++)
            {
                if (items[i] is { } item)
                {
                    placements.Add(Place(item, i - start, columns, rows, cell));
                }
            }

            _ = builder.AddSheet(sheet.X, sheet.Y, CollectionsMarshal.AsSpan(placements));
            sheets++;
        }

        return sheets;
    }

    /// <summary>Gets a sheet's size in points.</summary>
    /// <param name="paper">The paper.</param>
    /// <param name="landscape">Whether the sheet is sideways.</param>
    /// <returns>The size.</returns>
    internal static Vector2 GetSheetSize(PaperSize paper, bool landscape)
    {
        SheetGrid.GetSheetSize(paper, landscape, out var width, out var height);
        return new(width, height);
    }

    /// <summary>Determines whether every page index exists.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pages">The page indices.</param>
    /// <returns><see langword="true"/> when all are valid.</returns>
    internal static bool AllExist(HyperPdfDocument self, ReadOnlySpan<int> pages)
    {
        foreach (var page in pages)
        {
            if ((uint)page >= (uint)self.PageCount)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Builds the new document's pages for a layout.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="layout">The layout.</param>
    /// <returns>The number of pages or sheets added.</returns>
    internal static int Compose(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, ReadOnlySpan<int> pages, in SheetLayout layout)
    {
        var filter = layout.IncludeAnnotations ? PdfAnnotationFilter.All : PdfAnnotationFilter.WidgetsOnly;
        if (layout.Imposition == PrintImposition.Booklet)
        {
            return ComposeBooklet(self, builder, importer, pages, layout, filter);
        }

        if (layout.Imposition == PrintImposition.Poster)
        {
            return ComposePoster(self, builder, importer, pages, layout, filter);
        }

        if (layout.PagesPerSheet <= 1 && !layout.FitToPaper)
        {
            return ComposePlain(self, importer, pages, filter);
        }

        return layout.PagesPerSheet <= 1
            ? ComposeFitted(self, builder, importer, pages, layout, filter)
            : ComposeGrid(self, builder, importer, pages, layout, filter);
    }

    /// <summary>Copies the pages as they are, skipping any that cannot be read.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="filter">The annotations kept.</param>
    /// <returns>The number of pages copied.</returns>
    internal static int ComposePlain(HyperPdfDocument self, PdfPageImporter importer, ReadOnlySpan<int> pages, PdfAnnotationFilter filter)
    {
        var copied = 0;
        foreach (var index in pages)
        {
            try
            {
                _ = importer.ImportPage(PdfDocumentPages.GetPage(self.Document, index), filter);
                copied++;
            }
            catch (Exception ex) when (ex is PdfException or InvalidDataException)
            {
                // A page that cannot be read is left out so the rest still print.
            }
        }

        return copied;
    }

    /// <summary>Puts each page on paper of its own orientation, sized as chosen.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns>The number of sheets added.</returns>
    internal static int ComposeFitted(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, ReadOnlySpan<int> pages, in SheetLayout layout, PdfAnnotationFilter filter)
    {
        var sheets = 0;
        var paper = GetSheetSize(layout.Paper, false);
        foreach (var index in pages)
        {
            try
            {
                var page = PdfDocumentPages.GetPage(self.Document, index);
                var form = importer.ImportForm(page, page.CropBox, filter, false);
                var placement = GetFitPlacement(form, page, paper, layout, out var sheet);
                _ = builder.AddSheet(sheet.X, sheet.Y, page.Rotation, [placement]);
                sheets++;
            }
            catch (Exception ex) when (ex is PdfException or InvalidDataException)
            {
                // A page that cannot be read is left out so the rest still print.
            }
        }

        return sheets;
    }

    /// <summary>Puts several pages on each sheet.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns>The number of sheets added.</returns>
    internal static int ComposeGrid(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, ReadOnlySpan<int> pages, in SheetLayout layout, PdfAnnotationFilter filter)
    {
        SheetGrid.GetGrid(layout.PagesPerSheet, out var columns, out var rows, out var landscape);
        if (columns == 1 && rows == 1)
        {
            // PDFium copies pages for a one by one grid rather than resizing them to the paper.
            return ComposeFlat(self, builder, importer, pages, filter);
        }

        var forms = new Dictionary<int, SheetPage?>();
        var items = new List<SheetPage?>(pages.Length);
        foreach (var index in pages)
        {
            var item = GetForm(self, importer, forms, index, filter);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return LaySheets(builder, items, columns, rows, GetSheetSize(layout.Paper, landscape));
    }

    /// <summary>Puts the pages in booklet order, two to a sideways sheet.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based, in reading order.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns>The number of sheets added.</returns>
    internal static int ComposeBooklet(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, ReadOnlySpan<int> pages, in SheetLayout layout, PdfAnnotationFilter filter)
    {
        var order = new List<int>(Booklet.SheetCount(pages.Length) * (BookletColumns * BookletColumns));
        Booklet.Arrange(pages.Length, order);
        var forms = new Dictionary<int, SheetPage?>();
        var items = new List<SheetPage?>(order.Count);
        foreach (var position in order)
        {
            // A blank page leaves its cell empty.
            items.Add(position < 0 ? null : GetForm(self, importer, forms, pages[position], filter));
        }

        return LaySheets(builder, items, BookletColumns, 1, GetSheetSize(layout.Paper, true));
    }

    /// <summary>Spreads each page over tiles-squared sheets, each showing part of the page.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="layout">The layout.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns>The number of sheets added.</returns>
    internal static int ComposePoster(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, ReadOnlySpan<int> pages, in SheetLayout layout, PdfAnnotationFilter filter)
    {
        // PDFium lays a single page on a single-page sheet by copying it, so each tile is a page the size of its box.
        var tiles = Math.Max(1, layout.PosterTiles);
        var sheets = 0;
        foreach (var index in pages)
        {
            for (var tile = 0; tile < tiles * tiles; tile++)
            {
                sheets += TryAddFlatSheet(self, builder, importer, index, tile, tiles, filter) ? 1 : 0;
            }
        }

        return sheets;
    }

    /// <summary>Adds pages as sheets of their own size, with their printable annotations drawn into the content.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="pages">The pages, zero-based.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns>The number of sheets added.</returns>
    internal static int ComposeFlat(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, ReadOnlySpan<int> pages, PdfAnnotationFilter filter)
    {
        var sheets = 0;
        foreach (var index in pages)
        {
            sheets += TryAddFlatSheet(self, builder, importer, index, 0, 0, filter) ? 1 : 0;
        }

        return sheets;
    }

    /// <summary>Adds a page, or one tile of it, as a sheet the size of its box that keeps the page's rotation.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="builder">The new document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="index">The page.</param>
    /// <param name="tile">The poster tile, when <paramref name="tiles"/> is above zero.</param>
    /// <param name="tiles">The poster tiles across and down, or zero for the whole visible page.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns><see langword="true"/> when added; a page that cannot be read is left out so the rest still print.</returns>
    internal static bool TryAddFlatSheet(HyperPdfDocument self, PdfDocumentBuilder builder, PdfPageImporter importer, int index, int tile, int tiles, PdfAnnotationFilter filter)
    {
        try
        {
            var page = PdfDocumentPages.GetPage(self.Document, index);
            var box = tiles > 0 ? GetTileBox(page.MediaBox, tile, tiles) : page.CropBox;
            var form = importer.ImportForm(page, box, filter, false);
            PdfFormPlacement placement = new(form, Matrix3x2.CreateTranslation(-box.Left, -box.Bottom));
            _ = builder.AddSheet(box.Width, box.Height, page.Rotation, [placement]);
            return true;
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException)
        {
            return false;
        }
    }

    /// <summary>Copies a page as a form, once however often it is used.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="importer">Copies the pages.</param>
    /// <param name="forms">The forms already made, by page.</param>
    /// <param name="index">The page.</param>
    /// <param name="filter">The annotations drawn.</param>
    /// <returns>The form, or <see langword="null"/> when the page cannot be read.</returns>
    internal static SheetPage? GetForm(HyperPdfDocument self, PdfPageImporter importer, Dictionary<int, SheetPage?> forms, int index, PdfAnnotationFilter filter)
    {
        if (forms.TryGetValue(index, out var existing))
        {
            return existing;
        }

        SheetPage? item = null;
        try
        {
            var page = PdfDocumentPages.GetPage(self.Document, index);
            item = new(importer.ImportForm(page, page.CropBox, filter, true), page.Width, page.Height);
        }
        catch (Exception ex) when (ex is PdfException or InvalidDataException)
        {
            // A page that cannot be read is left blank.
        }

        forms[index] = item;
        return item;
    }
}
