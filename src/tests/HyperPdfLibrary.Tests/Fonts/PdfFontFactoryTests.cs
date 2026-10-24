// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Text;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks that font loading belongs to each document.</summary>
public sealed class PdfFontFactoryTests
{
    /// <summary>Two live documents read through different font loaders without changing one another.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SimultaneousDocumentsUseTheirOwnFontFactories(CancellationToken cancellationToken)
    {
        var bytes = TextTestDocument.Create("BT /F1 10 Tf 10 100 Td (A) Tj ET").ToBytes();
        var leftLoads = 0;
        var rightLoads = 0;
        using var left = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { FontFactory = LoadLeft });
        using var right = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { FontFactory = LoadRight });

        var leftRead = PdfDocumentText.GetTextPageAsync(left, 0, cancellationToken).AsTask();
        var rightRead = PdfDocumentText.GetTextPageAsync(right, 0, cancellationToken).AsTask();
        await Task.WhenAll(leftRead, rightRead);

        await Assert.That(leftLoads).IsGreaterThan(0);
        await Assert.That(rightLoads).IsGreaterThan(0);
        await Assert.That((await leftRead).Text).IsEqualTo("L");
        await Assert.That((await rightRead).Text).IsEqualTo("R");

        PdfFont LoadLeft(PdfDictionary dictionary)
        {
            _ = Interlocked.Increment(ref leftLoads);
            return new FixedTextFont(dictionary, 'L');
        }

        PdfFont LoadRight(PdfDictionary dictionary)
        {
            _ = Interlocked.Increment(ref rightLoads);
            return new FixedTextFont(dictionary, 'R');
        }
    }

    /// <summary>Maps any one-byte code to a fixed character.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="character">The character returned for each code.</param>
    private sealed class FixedTextFont(PdfDictionary dictionary, char character) : PdfFont(dictionary)
    {
        /// <summary>The glyph metrics and outline.</summary>
        private readonly TextTestFont _font = new(dictionary, false, false);

        /// <inheritdoc/>
        public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
        {
            code = bytes[0];
            return 1;
        }

        /// <inheritdoc/>
        public override float GetWidth(int code) => _font.GetWidth(code);

        /// <inheritdoc/>
        public override PdfPath? GetOutline(int code) => _font.GetOutline(code);

        /// <inheritdoc/>
        public override int GetUnicode(int code, Span<char> destination)
        {
            destination[0] = character;
            return 1;
        }
    }
}
