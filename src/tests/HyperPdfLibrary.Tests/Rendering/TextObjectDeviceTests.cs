// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks what the interpreter tells a text-collecting device: text object starts, inline property lists, no images, every layer.</summary>
public sealed class TextObjectDeviceTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The marked content identifier in the inline property list.</summary>
    private const int Mcid = 3;

    /// <summary>The text-showing operators in the content.</summary>
    private const int Shows = 3;

    /// <summary>A text device hears each show, sees inline property lists, gets no images and is not stopped by hidden layers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextDeviceSeesShowsPropertiesAndNoImages()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Span << /MCID 3 >> BDC BT (A) Tj [(B)] TJ EMC /OC /L1 BDC (C) Tj ET EMC /Im Do BI /W 1 /H 1 /CS /G /BPC 8 ID ÿ EI", };
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode", "FF0000>");
        var layer = pdf.AddObject("<< /Type /OCG /Name (Off) >>");
        pdf.Resources = $"/XObject << /Im {image} 0 R >> /Properties << /L1 {layer} 0 R >>";
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{layer} 0 R] /D << /OFF [{layer} 0 R] >> >>";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var device = new TextDevice();
        using (var interpreter = new ContentInterpreter(PdfDocumentRendering.GetRenderCache(document), device, 0))
        {
            ContentExecution.RunPage(interpreter, PdfDocumentPages.GetPage(document, 0));
        }

        await Assert.That(device.TextObjects).IsEqualTo(Shows);
        await Assert.That(device.FirstMcid).IsEqualTo(Mcid);
        await Assert.That(device.Images).IsEqualTo(0);
    }

    /// <summary>A device that counts what a text collector is told.</summary>
    [DebuggerDisplay("TextDevice: {TextObjects} shows")]
    private sealed class TextDevice : ITextObjectDevice
    {
        /// <summary>Gets the text-showing operators reported.</summary>
        internal int TextObjects { get; private set; }

        /// <summary>Gets the images drawn.</summary>
        internal int Images { get; private set; }

        /// <summary>Gets the /MCID of the first property list with one, or -1.</summary>
        internal int FirstMcid { get; private set; } = -1;

        /// <inheritdoc/>
        public void BeginTextObject() => TextObjects++;

        /// <inheritdoc/>
        public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
        {
            if (FirstMcid < 0 && properties is not null && properties.ContainsKey(KnownName.MCID))
            {
                FirstMcid = properties.GetInt32(KnownName.MCID);
            }
        }

        /// <inheritdoc/>
        public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state) => Images++;

        /// <inheritdoc/>
        public void Save()
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void Restore()
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void Stroke(PdfPath path, ref GraphicsState state)
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
        {
            // Glyphs need a font, which this test does not load.
        }

        /// <inheritdoc/>
        public void PaintShading(PdfShading shading, ref GraphicsState state)
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void BeginGroup(in GroupInfo group)
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void EndGroup(in GroupInfo group)
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public void EndMarkedContent()
        {
            // Not needed to collect text.
        }

        /// <inheritdoc/>
        public IPictureDevice CreatePictureDevice(PdfRect cull) => throw new NotSupportedException("Text collection records no pictures.");
    }
}
