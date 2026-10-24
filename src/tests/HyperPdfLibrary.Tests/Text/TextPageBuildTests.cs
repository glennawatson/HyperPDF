// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks text build state when extraction starts another extraction on the same thread.</summary>
[NotInParallel]
public sealed class TextPageBuildTests
{
    /// <summary>More than the states closed documents may leave, so one rent of each kind drains them.</summary>
    private const int SharedStateLimit = 8;

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

    /// <summary>A closed document hands its cleaned build state to later documents sharing its host services.</summary>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The first extraction did not return its state.</exception>
    [Test]
    public async Task ClosedDocumentSharesItsBuildState()
    {
        TextPageBuildState state;
        var options = new PdfOpenOptions { DrawingServices = SkiaTestRegistration.CreateServices(), FontFactory = static dictionary => new TextTestFont(dictionary, false, false) };
        using (var first = PdfDocumentReader.OpenWith(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (A) Tj ET").ToBytes(), options))
        {
            _ = PdfDocumentText.ExtractText(first, 0);
            state = first.State.TextBuild ?? throw new InvalidOperationException("The first extraction did not return its state.");
        }

        using var second = PdfDocumentReader.OpenWith(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (B) Tj ET").ToBytes(), options);
        var rented = new List<TextPageBuildState>();
        for (var i = 0; i <= SharedStateLimit; i++)
        {
            rented.Add(TextPageBuild.Rent(second));
        }

        var found = rented.Exists(item => ReferenceEquals(item, state));
        foreach (var item in rented)
        {
            TextPageBuild.Return(second, item);
        }

        await Assert.That(found).IsTrue();
        await Assert.That(PdfDocumentText.ExtractText(second, 0).Text).IsEqualTo("B");
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
