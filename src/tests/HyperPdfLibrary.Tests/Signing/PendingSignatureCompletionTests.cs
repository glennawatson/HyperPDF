// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>A pending signature is finished once: its file is written by one completion and handed out once.</summary>
public sealed class PendingSignatureCompletionTests
{
    /// <summary>The threads that try to finish one pending signature together.</summary>
    private const int Completers = 4;

    /// <summary>The size of a signature value that is too short to be real but fits.</summary>
    private const int ShortValueLength = 16;

    /// <summary>A second completion is refused and the first result is not changed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondCompletionIsRefusedAndTheFirstResultStays(CancellationToken cancellationToken)
    {
        var pending = await PrepareAsync(cancellationToken);
        var first = await PdfDocumentSigning.CompleteAsync(pending, Sign(pending), cancellationToken);
        var firstCopy = first.Bytes.ToArray();

        await Assert.That(async () => await PdfDocumentSigning.CompleteAsync(pending, new byte[ShortValueLength], cancellationToken)).Throws<InvalidOperationException>();

        await Assert.That(first.Bytes.AsSpan().SequenceEqual(firstCopy)).IsTrue();
        await Assert.That(SignatureFixtures.ValidateSingle(first.Bytes).SignatureValid).IsTrue();
    }

    /// <summary>A container completion after a completion is refused too.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ContainerCompletionAfterACompletionIsRefused(CancellationToken cancellationToken)
    {
        var pending = await PrepareAsync(cancellationToken);
        _ = await PdfDocumentSigning.CompleteAsync(pending, Sign(pending), cancellationToken);

        await Assert.That(async () => await PdfDocumentSigning.CompleteWithContainerAsync(pending, new byte[ShortValueLength], cancellationToken)).Throws<InvalidOperationException>();
    }

    /// <summary>A value that does not fit leaves the pending signature open for a smaller one.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValueThatDoesNotFitLeavesTheSignatureOpen(CancellationToken cancellationToken)
    {
        var pending = await PrepareAsync(cancellationToken);
        var tooLarge = new byte[pending.ContentsCapacity + 1];

        await Assert.That(async () => await PdfDocumentSigning.CompleteAsync(pending, tooLarge, cancellationToken)).Throws<PdfException>();
        var result = await PdfDocumentSigning.CompleteAsync(pending, Sign(pending), cancellationToken);

        await Assert.That(SignatureFixtures.ValidateSingle(result.Bytes).SignatureValid).IsTrue();
    }

    /// <summary>Completions that overlap finish the signature once; the others are refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlappingCompletionsFinishTheSignatureOnce(CancellationToken cancellationToken)
    {
        var pending = await PrepareAsync(cancellationToken);
        using var start = new Barrier(Completers);
        var job = new CompleteJob(pending, Sign(pending), start, cancellationToken);
        var tasks = new Task<bool>[Completers];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = DedicatedThread.RunAsync(job, static work => work.TryCompleteAfterStart(), cancellationToken);
        }

        var outcomes = await Task.WhenAll(tasks);

        await Assert.That(outcomes.Count(static finished => finished)).IsEqualTo(1);
    }

    /// <summary>A new completion token replaces the completed preparation operation's cancellation lifetime.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CompletionUsesItsOwnCancellationToken(CancellationToken cancellationToken)
    {
        using var preparation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pending = await PrepareAsync(preparation.Token);
        await preparation.CancelAsync();
        var result = await PdfDocumentSigning.CompleteAsync(pending, Sign(pending), cancellationToken);

        await Assert.That(SignatureFixtures.ValidateSingle(result.Bytes).SignatureValid).IsTrue();
    }

    /// <summary>Cancelled completion leaves a pending signature available for another operation.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task CancelledCompletionLeavesPendingSignatureOpen(CancellationToken cancellationToken)
    {
        var pending = await PrepareAsync(cancellationToken);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var value = Sign(pending);

        await Assert.That(async () => await PdfDocumentSigning.CompleteAsync(pending, value, cancelled.Token)).Throws<OperationCanceledException>();
        var result = await PdfDocumentSigning.CompleteAsync(pending, value, cancellationToken);

        await Assert.That(SignatureFixtures.ValidateSingle(result.Bytes).SignatureValid).IsTrue();
    }

    /// <summary>Prepares a signature of a blank file for a signer that works apart from the library.</summary>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <returns>The pending signature.</returns>
    private static async ValueTask<PdfPendingSignature> PrepareAsync(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        return await PdfDocumentSigning.PrepareAsync(
            document,
            new() { SigningTime = SigningFixtures.Time, VerifyOutput = false },
            new([SignatureFixtures.Signer.RawData], PdfSignatureAlgorithm.RsaPkcs1),
            cancellationToken);
    }

    /// <summary>Signs a pending signature's signed attributes with the test signer's key.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <returns>The signature value.</returns>
    private static byte[] Sign(PdfPendingSignature pending)
    {
        using var key = SignatureFixtures.Signer.GetRSAPrivateKey()!;
        return key.SignData(pending.SignedAttributes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    /// <summary>The arguments of a thread that finishes a pending signature.</summary>
    /// <param name="Pending">The pending signature.</param>
    /// <param name="Value">The signature value.</param>
    /// <param name="Start">The barrier the completing threads meet at.</param>
    /// <param name="CancellationToken">Cancels the waiting.</param>
    private sealed record CompleteJob(PdfPendingSignature Pending, byte[] Value, Barrier Start, CancellationToken CancellationToken)
    {
        /// <summary>Waits for the other threads, then tries to finish the signature.</summary>
        /// <returns><see langword="true"/> when this completion finished the signature; <see langword="false"/> when it was refused.</returns>
        internal async Task<bool> TryCompleteAfterStart()
        {
            Start.SignalAndWait(CancellationToken);
            try
            {
                _ = await PdfDocumentSigning.CompleteAsync(Pending, Value, CancellationToken);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
