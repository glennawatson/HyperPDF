// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks reusable text buffers after interrupted extraction.</summary>
[NotInParallel]
public sealed class TextSpaceCleanupTests
{
    /// <summary>The index of B in the compacted result.</summary>
    private const int FinalLetter = 3;

    /// <summary>Unicode lookups allowed before an interruption.</summary>
    private const int LookupsBeforeInterruption = 10;

    /// <summary>Cancellation and a font failure clear the leased state before a successful retry.</summary>
    /// <param name="cancel">Whether to interrupt with cancellation or a font failure.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task InterruptedExtractionClearsPopulatedBuffers(bool cancel, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var probe = new InterruptionProbe(cancellation, cancel);
        using var document = PdfDocumentReader.OpenWith(TextTestDocument.Create("BT /F1 10 Tf 10 100 Td [(  A) -600 (   B  )] TJ ET").ToBytes(), new PdfOpenOptions
        {
            FontFactory = dictionary => new InterruptingFont(dictionary, probe),
        });
        await PdfDocumentPages.PrefetchPageAsync(document, 0, cancellationToken);

        // The interpreter reads each glyph's text first; the wide TJ adjustment then makes the line assembly look up the font's space.
        Exception? failure = null;
        try
        {
            _ = await PdfDocumentText.GetTextPageAsync(document, 0, cancellation.Token);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            failure = exception;
        }

        var state = document.State.TextBuild;
        var textCountAfterFailure = state?.Chars.Count ?? -1;
        var charCountAfterFailure = state?.LineChars.Count ?? -1;
        var callsBeforeFailure = probe.UnicodeCalls;
        probe.Armed = false;
        var recovered = await PdfDocumentText.GetTextPageAsync(document, 0, cancellationToken);

        await Assert.That(callsBeforeFailure).IsGreaterThanOrEqualTo(LookupsBeforeInterruption);
        await Assert.That(failure is OperationCanceledException).IsEqualTo(cancel);
        await Assert.That(failure is IOException).IsEqualTo(!cancel);
        await Assert.That(charCountAfterFailure).IsEqualTo(0);
        await Assert.That(textCountAfterFailure).IsEqualTo(0);
        await Assert.That(ReferenceEquals(state, document.State.TextBuild)).IsTrue();
        await Assert.That(recovered.Text).IsEqualTo(" A B ");
        await Assert.That(recovered.CharCount).IsEqualTo(recovered.Text.Length);
        await Assert.That(recovered.GetChar(FinalLetter).Unicode).IsEqualTo('B');
        await Assert.That(recovered.GetChar(FinalLetter).Kind).IsEqualTo(PdfTextCharKind.Normal);
        await Assert.That(recovered.GetChar(FinalLetter).Box.Left).IsGreaterThan(recovered.GetChar(1).Box.Left);
        await Assert.That(state!.LineChars.Count).IsEqualTo(0);
        await Assert.That(state.Chars.Count).IsEqualTo(0);
    }

    /// <summary>Records the real build state at the font callback.</summary>
    /// <param name="cancellation">The token source owned by the test.</param>
    /// <param name="cancel">Whether to interrupt by cancelling.</param>
    private sealed class InterruptionProbe(CancellationTokenSource cancellation, bool cancel)
    {
        /// <summary>Gets or sets whether Unicode lookup can interrupt extraction.</summary>
        internal bool Armed { get; set; } = true;

        /// <summary>Gets the number of Unicode lookups observed.</summary>
        internal int UnicodeCalls { get; private set; }

        /// <summary>Interrupts after several glyphs have been read.</summary>
        /// <exception cref="OperationCanceledException">The test cancelled extraction.</exception>
        /// <exception cref="IOException">The test font failed during assembly.</exception>
        internal void Observe()
        {
            UnicodeCalls++;

            if (!Armed)
            {
                return;
            }

            if (UnicodeCalls < LookupsBeforeInterruption)
            {
                return;
            }

            Armed = false;
            if (cancel)
            {
                cancellation.Cancel();
                cancellation.Token.ThrowIfCancellationRequested();
            }

            throw new IOException("The test font failed while assembling text.");
        }
    }

    /// <summary>Delegates test glyphs and interrupts their Unicode lookup during text assembly.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="probe">The interruption boundary.</param>
    private sealed class InterruptingFont(PdfDictionary dictionary, InterruptionProbe probe) : PdfFont(dictionary)
    {
        /// <summary>The existing deterministic test font.</summary>
        private readonly TextTestFont _inner = new(dictionary, false, false);

        /// <inheritdoc/>
        public override int ReadCode(ReadOnlySpan<byte> bytes, out int code) => _inner.ReadCode(bytes, out code);

        /// <inheritdoc/>
        public override float GetWidth(int code) => _inner.GetWidth(code);

        /// <inheritdoc/>
        public override PdfPath? GetOutline(int code) => _inner.GetOutline(code);

        /// <inheritdoc/>
        public override int GetUnicode(int code, Span<char> destination)
        {
            probe.Observe();
            return _inner.GetUnicode(code, destination);
        }
    }
}
