// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Checks annotation timestamps belong to their document.</summary>
public sealed class AnnotationClockTests
{
    /// <summary>Each document uses its own clock when creating an annotation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewAnnotationsUseTheirDocumentClock()
    {
        var firstTime = DateTimeOffset.UnixEpoch;
        var secondTime = firstTime.AddYears(1);
        var bytes = TestPdf.Create(1);
        using var first = new HyperPdfTestDocument(bytes, new HyperPdfEngine(PdfFontLoader.Load, new FixedTimeProvider(firstTime)));
        using var second = new HyperPdfTestDocument(bytes, new HyperPdfEngine(PdfFontLoader.Load, new FixedTimeProvider(secondTime)));

        var firstAnnotation = PdfAnnotations.Create(first.Editor.Store, KnownName.Text, default);
        var secondAnnotation = PdfAnnotations.Create(second.Editor.Store, KnownName.Text, default);
        HyperPdfAnnotationReading.Finish(first.Editor, firstAnnotation, null, string.Empty, []);
        HyperPdfAnnotationReading.Finish(second.Editor, secondAnnotation, null, string.Empty, []);

        await Assert.That(PdfAnnotations.GetDate(firstAnnotation, KnownName.M)).IsEqualTo(firstTime);
        await Assert.That(PdfAnnotations.GetDate(secondAnnotation, KnownName.M)).IsEqualTo(secondTime);
    }

    /// <summary>Returns one timestamp for the document that owns it.</summary>
    /// <param name="time">The timestamp to return.</param>
    private sealed class FixedTimeProvider(DateTimeOffset time) : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => time;
    }
}
