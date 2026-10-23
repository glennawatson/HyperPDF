// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.Data;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that drawing callbacks stay with the document that selected them.</summary>
public sealed class PdfDrawingServicesTests
{
    /// <summary>The glyph id returned by the left provider.</summary>
    private const int LeftGlyph = 2;

    /// <summary>The glyph id returned by the right provider.</summary>
    private const int RightGlyph = 3;

    /// <summary>The nonstandard font name used by both documents.</summary>
    private const string FaceName = "DocumentFace";

    /// <summary>Two documents with the same font name retain their own provider faces.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentsUseOwnFontProvidersAndFaceCaches()
    {
        var pdf = new RenderTestPdf(1, 1);
        var fontNumber = pdf.AddObject($"<< /Type /Font /Subtype /Type1 /BaseFont /{FaceName} >>");
        pdf.Resources = $"/Font << /F1 {fontNumber} 0 R >>";
        var bytes = pdf.ToBytes();
        var leftProvider = new DistinctFontProvider(LeftGlyph);
        var rightProvider = new DistinctFontProvider(RightGlyph);
        var leftServices = new PdfDrawingServices(new() { Fonts = leftProvider });
        var rightServices = new PdfDrawingServices(new() { Fonts = rightProvider });
        using var leftDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = leftServices });
        using var rightDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = rightServices });

        var leftSource = (SubstituteFace)((PdfSimpleFont)PdfFontLoader.Load(FontDictionary(leftDocument))!).Source;
        var rightSource = (SubstituteFace)((PdfSimpleFont)PdfFontLoader.Load(FontDictionary(rightDocument))!).Source;
        await Assert.That(leftSource.GetGlyph('A')).IsEqualTo(LeftGlyph);
        await Assert.That(rightSource.GetGlyph('A')).IsEqualTo(RightGlyph);
        await Assert.That(ReferenceEquals(leftSource, rightSource)).IsFalse();
        var cached = leftServices.FontMatcher.Match(new(FaceName, StandardFont.None, FontFlags.Nonsymbolic, 0, CjkScript.None));
        await Assert.That(ReferenceEquals(leftSource, cached)).IsTrue();
    }

    /// <summary>Two concurrent documents use their own unsupported-image decoder.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentsUseOwnUnsupportedImageDecoders()
    {
        var pdf = new RenderTestPdf(1, 1) { Content = "/Im Do" };
        var imageNumber = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode", "not a jpeg");
        pdf.Resources = $"/XObject << /Im {imageNumber} 0 R >>";
        var bytes = pdf.ToBytes();

        var leftRegistration = new PdfRenderBackendRegistration().UseSkia();
        var rightRegistration = new PdfRenderBackendRegistration().UseSkia();
        var leftDecoder = new DecoderCounter(leftRegistration.Backend!, 0xFF);
        var rightDecoder = new DecoderCounter(rightRegistration.Backend!, 0);
        var leftServices = new PdfDrawingServices(leftRegistration with { UnsupportedImageDecoder = leftDecoder.Decode });
        var rightServices = new PdfDrawingServices(rightRegistration with { UnsupportedImageDecoder = rightDecoder.Decode });
        using var leftDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = leftServices });
        using var rightDocument = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { DrawingServices = rightServices });
        var leftStream = ImageStream(leftDocument);
        var rightStream = ImageStream(rightDocument);
        var leftCache = new PdfRenderCache(leftDocument);
        var rightCache = new PdfRenderCache(rightDocument);
        using var leftCancellation = new CancellationTokenSource();
        using var rightCancellation = new CancellationTokenSource();
        try
        {
            var found = await Task.WhenAll(
                Task.Run(() => Acquire(leftCache, leftStream, leftCancellation.Token)),
                Task.Run(() => Acquire(rightCache, rightStream, rightCancellation.Token)));

            await Assert.That(found.AsSpan().IndexOf(false)).IsEqualTo(-1);
            await Assert.That(leftDecoder.Calls).IsEqualTo(1);
            await Assert.That(rightDecoder.Calls).IsEqualTo(1);
            await Assert.That(leftDecoder.LastToken).IsEqualTo(leftCancellation.Token);
            await Assert.That(rightDecoder.LastToken).IsEqualTo(rightCancellation.Token);
            await Assert.That(ReferenceEquals(leftDocument.DrawingServices, rightDocument.DrawingServices)).IsFalse();
        }
        finally
        {
            leftCache.Close();
            rightCache.Close();
        }
    }

    /// <summary>Gets the test image stream.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The stream.</returns>
    private static PdfStream ImageStream(PdfDocument document) =>
        PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.XObject)!.GetStream(document.Objects.Names.Intern("Im"))!;

    /// <summary>Gets the font dictionary of the test page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The font dictionary.</returns>
    private static PdfDictionary FontDictionary(PdfDocument document) =>
        PdfDocumentPages.GetPage(document, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("F1"))!;

    /// <summary>Gets an image and releases its lease.</summary>
    /// <param name="cache">The document cache.</param>
    /// <param name="stream">The image stream.</param>
    /// <param name="cancellationToken">Cancels the decode.</param>
    /// <returns>Whether an image was found.</returns>
    private static bool Acquire(PdfRenderCache cache, PdfStream stream, CancellationToken cancellationToken)
    {
        var entry = cache.AcquireImage(stream, 0, cancellationToken);
        entry?.Release();
        return entry is not null;
    }

    /// <summary>Creates one pixel and counts calls to an unsupported-image decoder.</summary>
    /// <param name="backend">The backend that creates the pixel.</param>
    /// <param name="red">The red channel.</param>
    private sealed class DecoderCounter(IPdfRenderBackend backend, byte red)
    {
        /// <summary>The bytes in one BGRA pixel.</summary>
        private const int PixelBytes = 4;

        /// <summary>The number of calls.</summary>
        private int _calls;

        /// <summary>The token passed to the last decode.</summary>
        private CancellationToken _lastToken;

        /// <summary>Gets the call count.</summary>
        internal int Calls => Volatile.Read(ref _calls);

        /// <summary>Gets the token passed to the last decode.</summary>
        internal CancellationToken LastToken => _lastToken;

        /// <summary>Creates a colored pixel for an unsupported stream.</summary>
        /// <param name="stream">The unsupported image stream.</param>
        /// <param name="cancellationToken">Cancels decoding.</param>
        /// <returns>The pixel.</returns>
        internal IPdfRenderImage Decode(PdfStream stream, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _lastToken = cancellationToken;
            _ = stream;
            _ = Interlocked.Increment(ref _calls);
            return backend.CreateImage(new(1, 1, PdfImagePixelFormat.Bgra8888), [0, 0, red, byte.MaxValue], PixelBytes);
        }
    }

    /// <summary>A provider whose face maps every character to one distinguishable glyph.</summary>
    /// <param name="glyph">The glyph id.</param>
    private sealed class DistinctFontProvider(int glyph) : IPdfFontProvider
    {
        /// <inheritdoc/>
        public IPdfFontFace DefaultFace => new DistinctFontFace(glyph);

        /// <inheritdoc/>
        public IPdfFontFace? Match(string family, int weight, bool italic) =>
            family == FaceName ? new DistinctFontFace(glyph) : null;

        /// <inheritdoc/>
        public IPdfFontFace? MatchCharacter(int unicode) => new DistinctFontFace(glyph);
    }

    /// <summary>A face whose glyph id distinguishes its provider.</summary>
    /// <param name="glyph">The glyph id.</param>
    private sealed class DistinctFontFace(int glyph) : IPdfFontFace
    {
        /// <summary>The normal weight.</summary>
        private const int NormalWeight = 400;

        /// <summary>The number of glyphs in the stub face.</summary>
        private const int TotalGlyphs = 4;

        /// <summary>The typical ascent in glyph units.</summary>
        private const int StubAscent = 800;

        /// <summary>The typical descent in glyph units.</summary>
        private const int StubDescent = -200;

        /// <summary>The glyph advance in glyph units.</summary>
        private const int StubAdvance = 500;

        /// <inheritdoc/>
        public string FamilyName => FaceName;

        /// <inheritdoc/>
        public int Weight => NormalWeight;

        /// <inheritdoc/>
        public bool Italic => false;

        /// <inheritdoc/>
        public int GlyphCount => TotalGlyphs;

        /// <inheritdoc/>
        public float Ascent => StubAscent;

        /// <inheritdoc/>
        public float Descent => StubDescent;

        /// <inheritdoc/>
        public int GetGlyph(int unicode) => glyph;

        /// <inheritdoc/>
        public float GetAdvance(int glyph) => StubAdvance;

        /// <inheritdoc/>
        public PdfPath? BuildOutline(int glyph) => null;

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
