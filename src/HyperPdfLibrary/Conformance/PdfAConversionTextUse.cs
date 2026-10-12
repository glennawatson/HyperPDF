// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Finds the codes each font shows, by running every page's content and every annotation's appearances.</summary>
internal static class PdfAConversionTextUse
{
    /// <summary>Runs the content.</summary>
    /// <param name="session">The conversion.</param>
    /// <returns>The codes by font dictionary, or <see langword="null"/> when some content could not be run to its end.</returns>
    internal static Dictionary<PdfDictionary, PdfAConversionTextCodes>? Collect(PdfAConversionSession session)
    {
        var document = session.Document;
        var device = new PdfAConversionTextDevice();
        var cache = PdfDocumentRendering.GetRenderCache(document);
        for (var i = 0; i < document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            using var interpreter = new ContentInterpreter(cache, device, 0, float.PositiveInfinity, session.CancellationToken);
            try
            {
                ContentExecution.RunPage(interpreter, page);
                RunAppearances(session, interpreter, page);
            }
            catch (Exception ex) when (ContentExecution.IsRecoverable(ex))
            {
                return null;
            }
        }

        return device.Fonts;
    }

    /// <summary>Runs the appearance streams of a page's annotations.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="interpreter">The interpreter.</param>
    /// <param name="page">The page.</param>
    private static void RunAppearances(PdfAConversionSession session, ContentInterpreter interpreter, PdfPage page)
    {
        var store = session.Store;
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            var normal = StoreReading.Resolve(store, annotations.GetDictionary(i)?.GetDictionary(KnownName.AP)?.GetRaw(KnownName.N) ?? default);
            if (normal.AsStream() is { } stream)
            {
                ContentExecution.RunForm(interpreter, stream, Matrix3x2.Identity, page.Resources);
                continue;
            }

            var states = normal.AsDictionary();
            for (var j = 0; states is not null && j < states.Count; j++)
            {
                if (StoreReading.Resolve(store, states.GetValueAt(j)).AsStream() is { } state)
                {
                    ContentExecution.RunForm(interpreter, state, Matrix3x2.Identity, page.Resources);
                }
            }
        }
    }
}
