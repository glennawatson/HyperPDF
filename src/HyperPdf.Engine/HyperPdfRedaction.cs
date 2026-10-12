// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Redaction;
using HyperPdfLibrary.Redaction;
namespace HyperPdf.Engine;

/// <summary>Implements Redaction over the document's owned state.</summary>
internal static class HyperPdfRedaction
{
    /// <summary>Turns the app's choices into the library's options.</summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The options.</returns>
    internal static PdfRedactionOptions ToOptions(RedactionSettings settings) => PdfRedactionOptions.Default with
    {
        Images = settings.Images switch
        {
            RedactionImageChoice.Keep => PdfRedactionImageMode.None,
            RedactionImageChoice.Remove => PdfRedactionImageMode.Remove,
            _ => PdfRedactionImageMode.BlankPixels,
        },
        LineArt = settings.LineArt switch
        {
            RedactionLineArtChoice.Keep => PdfRedactionLineArtMode.None,
            RedactionLineArtChoice.RemoveTouched => PdfRedactionLineArtMode.RemoveTouched,
            _ => PdfRedactionLineArtMode.RemoveCovered,
        },
        RemoveInvisibleText = settings.RemoveHiddenText,
        Annotations = settings.RemoveLinksAndComments ? PdfRedactionAnnotationMode.RemoveTouched : PdfRedactionAnnotationMode.Keep,
        ScrubMetadata = settings.ScrubMetadata,
    };
}
