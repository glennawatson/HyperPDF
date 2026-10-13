// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Finishing every embedded font of a document finishes them all, also after a failure and for a second caller.</summary>
public sealed class FinishAllFontsTests
{
    /// <summary>The time a second caller is given to return early before the test decides it waited.</summary>
    private static readonly TimeSpan EarlyReturnWindow = TimeSpan.FromMilliseconds(300);

    /// <summary>A font that fails to finish stays in line, so the next call finishes it and the fonts after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontsAfterAFailureAreFinishedByTheNextCall()
    {
        using var release = new ManualResetEventSlim(false);
        var sink = new FaultingSink(new PdfDocumentBuilder(), release);
        var fonts = new[] { Create(sink), Create(sink), Create(sink) };
        sink.FailNextSet = true;

        await Assert.That(() => PdfEmbeddedFont.FinishAll(sink)).Throws<InvalidOperationException>();
        PdfEmbeddedFont.FinishAll(sink);

        foreach (var font in fonts)
        {
            await Assert.That(font.IsFinished).IsTrue();
        }
    }

    /// <summary>A second caller does not return until a font the first caller is finishing is written.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondCallerWaitsForFontsTheFirstIsFinishing(CancellationToken cancellationToken)
    {
        using var release = new ManualResetEventSlim(false);
        var sink = new FaultingSink(new PdfDocumentBuilder(), release);
        var font = Create(sink);
        sink.HoldNextSet = true;
        var first = DedicatedThread.Run(sink, FinishAll, cancellationToken);
        await sink.SetEntered.Task.WaitAsync(cancellationToken);

        var second = DedicatedThread.Run(sink, FinishAll, cancellationToken);
        var returnedEarly = await Task.WhenAny(second, Task.Delay(EarlyReturnWindow, cancellationToken)) == second;
        release.Set();
        await Task.WhenAll(first, second);

        await Assert.That(returnedEarly).IsFalse();
        await Assert.That(font.IsFinished).IsTrue();
    }

    /// <summary>Creates a font in a sink.</summary>
    /// <param name="sink">The sink.</param>
    /// <returns>The font.</returns>
    private static PdfEmbeddedFont Create(IPdfObjectSink sink) => PdfEmbeddedFont.Create(sink, EmbeddedFontPdf.Open(TestFont.Create(), 0));

    /// <summary>Finishes the fonts of a sink.</summary>
    /// <param name="sink">The sink.</param>
    private static void FinishAll(FaultingSink sink) => PdfEmbeddedFont.FinishAll(sink);

    /// <summary>A sink that passes everything to a builder, and can fail or hold the next write of an object's value.</summary>
    /// <param name="inner">The builder that holds the objects.</param>
    /// <param name="release">The event that lets a held write go on.</param>
    private sealed class FaultingSink(PdfDocumentBuilder inner, ManualResetEventSlim release) : IPdfObjectSink
    {
        /// <summary>Gets the signal that a held write has begun.</summary>
        public TaskCompletionSource SetEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Gets or sets a value indicating whether the next write fails.</summary>
        public bool FailNextSet { get; set; }

        /// <summary>Gets or sets a value indicating whether the next write waits for the release event.</summary>
        public bool HoldNextSet { get; set; }

        /// <inheritdoc/>
        public PdfNameTable Names => inner.Names;

        /// <inheritdoc/>
        public PdfObjectStore? Owner => inner.Owner;

        /// <inheritdoc/>
        public PdfObjectId Reserve() => inner.Reserve();

        /// <inheritdoc/>
        public PdfObjectId Add(PdfValue value) => inner.Add(value);

        /// <inheritdoc/>
        public void Set(PdfObjectId id, PdfValue value)
        {
            if (FailNextSet)
            {
                FailNextSet = false;
                throw new InvalidOperationException("The sink failed.");
            }

            if (HoldNextSet)
            {
                HoldNextSet = false;
                SetEntered.SetResult();
                release.Wait();
            }

            inner.Set(id, value);
        }
    }
}
