// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads page measurement information.</summary>
public static class PdfDocumentMeasure
{
    /// <summary>The default precision of a number format.</summary>
    private const int DefaultPrecision = 100;

    /// <summary>The numbers in a bounding box.</summary>
    private const int BoxNumbers = 4;

    /// <summary>Gets a page's viewports (<c>/VP</c>), loading the page's objects first.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The viewports.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page does not exist.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static ValueTask<PdfViewport[]> GetViewportsAsync(PdfDocument document, int pageIndex, CancellationToken cancellationToken)
    {
        var page = PdfDocumentPages.GetPage(document, pageIndex);
        var load = PdfPrefetcher.PrefetchAsync(document.Objects, page.Dictionary, PdfPrefetchKind.Page, cancellationToken);
        return load.IsCompletedSuccessfully ? PdfDocumentMeasure.ViewportsReady(document, page, cancellationToken) : PdfDocumentMeasure.ViewportsAfterAsync(document, load, page, cancellationToken);
    }

    /// <summary>Gets a page's viewports (<c>/VP</c>).</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The viewports.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfViewport[] GetViewports(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var list = page.Dictionary.Array("VP");
        var viewports = new List<PdfViewport>(list?.Count ?? 0);
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is not { } viewport)
            {
                continue;
            }

            var box = PdfDocumentMeasure.ReadPoints(viewport.Array("BBox"));
            viewports.Add(new(viewport.Rect("BBox"), viewport.Text("Name"), viewport.Dict("Measure") is { } measure ? PdfDocumentMeasure.ReadMeasure(document, measure) : null)
            {
                BoundingBox = box.Length == PdfDocumentMeasure.BoxNumbers ? box : [],
            });
        }

        return [.. viewports];
    }

    /// <summary>Reads a measure dictionary, from a viewport or an annotation's <c>/Measure</c>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="measure">The measure dictionary.</param>
    /// <returns>The measure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> is null.</exception>
    public static PdfMeasure ReadMeasure(PdfDocument document, PdfDictionary measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        var subtype = measure.NameText("Subtype") ?? "RL";
        return new(
subtype,
measure.Text("R"),
PdfDocumentMeasure.ReadNumberFormats(measure.Array("X")),
PdfDocumentMeasure.ReadNumberFormats(measure.Array("Y")),
PdfDocumentMeasure.ReadNumberFormats(measure.Array("D")),
PdfDocumentMeasure.ReadNumberFormats(measure.Array("A")),
PdfDocumentMeasure.ReadNumberFormats(measure.Array("T")),
PdfDocumentMeasure.ReadNumberFormats(measure.Array("S")),
measure.Array("O").Numbers(),
measure.Num("CYX", 1),
subtype == "GEO" ? PdfDocumentMeasure.ReadGeospatial(measure) : null);
    }

    /// <summary>Reads an array of number format dictionaries.</summary>
    /// <param name="formats">The array, or null.</param>
    /// <returns>The formats.</returns>
    private static PdfNumberFormat[] ReadNumberFormats(PdfArray? formats)
    {
        if (formats is null)
        {
            return [];
        }

        var result = new List<PdfNumberFormat>(formats.Count);
        for (var i = 0; i < formats.Count; i++)
        {
            if (formats.GetDictionary(i) is { } format)
            {
                result.Add(PdfDocumentMeasure.ReadNumberFormat(format));
            }
        }

        return [.. result];
    }

    /// <summary>Reads one number format dictionary.</summary>
    /// <param name="format">The dictionary.</param>
    /// <returns>The format.</returns>
    private static PdfNumberFormat ReadNumberFormat(PdfDictionary format) => new(
format.Text("U") ?? string.Empty,
format.Num("C", 1),
format.NameText("F") ?? "D",
format.Int("D", PdfDocumentMeasure.DefaultPrecision),
format.Flag("FD", false),
format.Text("RT") ?? ",",
format.Text("RD") ?? ".",
format.Text("PS") ?? " ",
format.Text("SS") ?? " ",
format.NameText("O") ?? "S");

    /// <summary>Reads the geospatial entries of a GEO measure (ISO 32000-2 Table 269).</summary>
    /// <param name="measure">The measure dictionary.</param>
    /// <returns>The geospatial data.</returns>
    private static PdfGeospatial ReadGeospatial(PdfDictionary measure)
    {
        var gcs = measure.Dict("GCS");
        var epsg = gcs?.Value("EPSG") ?? default;
        var text = gcs?.Text("WKT");
        return new(
            gcs?.NameText("Type"),
            text,
            epsg.IsNumber ? epsg.AsInt32() : null,
            PdfDocumentMeasure.ReadPoints(measure.Array("GPTS")),
            PdfDocumentMeasure.ReadPoints(measure.Array("LPTS")),
            PdfDocumentMeasure.ReadPoints(measure.Array("Bounds")),
            measure.Array("PDU").NameTexts())
        {
            CoordinateSystemName = WellKnownTextNames.Read(text),
            DisplayCoordinateSystem = measure.Dict("DCS") is { } display ? PdfDocumentMeasure.ReadCoordinateSystem(display) : null,
            ProjectedMatrix = PdfDocumentMeasure.ReadPoints(measure.Array("PCSM")),
        };
    }

    /// <summary>Reads a geographic or projected coordinate system dictionary (ISO 32000-2 Tables 270 and 271).</summary>
    /// <param name="system">The dictionary.</param>
    /// <returns>The coordinate system.</returns>
    private static PdfCoordinateSystem ReadCoordinateSystem(PdfDictionary system)
    {
        var epsg = system.Value("EPSG");
        var text = system.Text("WKT");
        return new(system.NameText("Type"), WellKnownTextNames.Read(text), epsg.IsNumber ? epsg.AsInt32() : null, text);
    }

    /// <summary>Reads an array that must hold only numbers, such as a point array.</summary>
    /// <param name="array">The array, or null.</param>
    /// <returns>The numbers; empty when the array is missing or an entry is not a number.</returns>
    private static double[] ReadPoints(PdfArray? array)
    {
        if (array is null)
        {
            return [];
        }

        var numbers = new double[array.Count];
        for (var i = 0; i < numbers.Length; i++)
        {
            var value = array.Get(i);
            if (!value.IsNumber)
            {
                return [];
            }

            numbers[i] = value.AsNumber();
        }

        return numbers;
    }

    /// <summary>Reads a page's viewports once its objects are loaded, reporting failures through the task.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The completed viewports, or a faulted task.</returns>
    private static ValueTask<PdfViewport[]> ViewportsReady(PdfDocument document, PdfPage page, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(PdfDocumentMeasure.GetViewports(document, page));
        }
        catch (Exception ex) when (PdfDocumentAsyncTasks.IsTaskFault(ex))
        {
            return ValueTask.FromException<PdfViewport[]>(ex);
        }
    }

    /// <summary>Waits for a load, then reads a page's viewports.</summary>
    /// <param name="document">The document.</param>
    /// <param name="load">The load.</param>
    /// <param name="page">The page.</param>
    /// <param name="cancellationToken">The token.</param>
    /// <returns>The viewports.</returns>
    private static async ValueTask<PdfViewport[]> ViewportsAfterAsync(PdfDocument document, ValueTask load, PdfPage page, CancellationToken cancellationToken)
    {
        await load.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return PdfDocumentMeasure.GetViewports(document, page);
    }
}
