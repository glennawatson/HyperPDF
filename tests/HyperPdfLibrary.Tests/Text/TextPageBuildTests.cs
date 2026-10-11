// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks text build state when extraction starts another extraction on the same thread.</summary>
[NotInParallel]
public sealed class TextPageBuildTests
{
    /// <summary>A nested document extraction cannot reset the outer document's text buffers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedDocumentExtractionKeepsOuterText()
    {
        using var inner = PdfDocumentReader.OpenWith(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (INNER) Tj ET").ToBytes(), new PdfOpenOptions
        {
            FontFactory = static dictionary => new TextTestFont(dictionary, false, false),
        });
        string? innerText = null;
        using var outer = PdfDocumentReader.OpenWith(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (AB) Tj ET").ToBytes(), new PdfOpenOptions
        {
            FontFactory = dictionary => new NestedFont(dictionary, () => innerText = PdfDocumentText.GetTextPage(inner, 0).Text),
        });

        var outerText = PdfDocumentText.GetTextPage(outer, 0).Text;
        await Assert.That(innerText).IsEqualTo("INNER");
        await Assert.That(outerText).IsEqualTo("AB");
    }

    /// <summary>Starts an independent extraction before the outer extraction draws its second glyph.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="extractInner">Extracts the inner document.</param>
    private sealed class NestedFont(PdfDictionary dictionary, Action extractInner) : PdfFont(dictionary)
    {
        /// <summary>The test font that supplies glyph behavior.</summary>
        private readonly TextTestFont _inner = new(dictionary, false, false);

        /// <summary>Whether the inner document has been extracted.</summary>
        private bool _extracted;

        /// <inheritdoc/>
        public override int ReadCode(ReadOnlySpan<byte> bytes, out int code) => _inner.ReadCode(bytes, out code);

        /// <inheritdoc/>
        public override float GetWidth(int code) => _inner.GetWidth(code);

        /// <inheritdoc/>
        public override PdfPath? GetOutline(int code) => _inner.GetOutline(code);

        /// <inheritdoc/>
        public override int GetUnicode(int code, Span<char> destination)
        {
            if (code == 'B' && !_extracted)
            {
                _extracted = true;
                extractInner();
            }

            return _inner.GetUnicode(code, destination);
        }
    }
}
