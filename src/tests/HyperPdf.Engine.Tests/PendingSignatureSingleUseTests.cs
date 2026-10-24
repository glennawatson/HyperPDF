// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks that a prepared signature can be completed once, because completing writes the value into the prepared file.</summary>
public sealed class PendingSignatureSingleUseTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 1;

    /// <summary>The completions started together.</summary>
    private const int Completions = 8;

    /// <summary>The signing time every test uses.</summary>
    private static readonly DateTimeOffset Time = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The signer, made once because key generation is slow.</summary>
    private static readonly X509Certificate2 Signer = PdfSigning.CreateCertificate("Single Use Signer", null, false, TimeProvider.System);

    /// <summary>A second completion is refused plainly and leaves the first result as it was.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondCompletionIsRefusedAndTheFirstResultIsKept(CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(Pages);
        var signer = DocumentFeature<IDocumentSigner>.Get(source.Document)!;
        var pending = await PrepareAsync(signer, cancellationToken);
        var first = await signer.CompleteAsync(pending, Sign(pending), cancellationToken);
        var before = first.Bytes.ToArray();

        var refusal = await Assert.That(async () => await signer.CompleteAsync(pending, Sign(pending), cancellationToken)).Throws<SigningRefusedException>();
        var again = await Assert.That(async () => await signer.CompleteWithContainerAsync(pending, new byte[1], cancellationToken)).Throws<SigningRefusedException>();

        await Assert.That(first.Bytes).IsEquivalentTo(before);
        await Assert.That(refusal!.Reasons.Count).IsEqualTo(1);
        await Assert.That(again!.Reasons.Count).IsEqualTo(1);
    }

    /// <summary>Of several completions started together, one succeeds and the rest are refused.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyOneOfSeveralConcurrentCompletionsSucceeds(CancellationToken cancellationToken)
    {
        using var source = new HyperPdfTestDocument(Pages);
        var signer = DocumentFeature<IDocumentSigner>.Get(source.Document)!;
        var pending = await PrepareAsync(signer, cancellationToken);
        var value = Sign(pending);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task<bool>[Completions];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() => CompleteAsync(signer, pending, value, start.Task, cancellationToken), cancellationToken);
        }

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        await Assert.That(results.Count(static succeeded => succeeded)).IsEqualTo(1);
    }

    /// <summary>Completes a signature once the start is released.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="pending">The pending signature.</param>
    /// <param name="value">The signature value.</param>
    /// <param name="start">Releases every caller together.</param>
    /// <param name="cancellationToken">Cancels the completion.</param>
    /// <returns><see langword="true"/> when the completion succeeded; <see langword="false"/> when it was refused.</returns>
    private static async Task<bool> CompleteAsync(IDocumentSigner signer, IPendingSignature pending, byte[] value, Task start, CancellationToken cancellationToken)
    {
        await start;
        try
        {
            _ = await signer.CompleteAsync(pending, value, cancellationToken);
            return true;
        }
        catch (SigningRefusedException)
        {
            return false;
        }
    }

    /// <summary>Prepares a signature with the test certificate.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="cancellationToken">Cancels the preparation.</param>
    /// <returns>The pending signature.</returns>
    private static async Task<IPendingSignature> PrepareAsync(IDocumentSigner signer, CancellationToken cancellationToken) =>
        await signer.PrepareAsync(new([Signer.RawData], SigningKeyKind.RsaPkcs1), new(0, "Approved", string.Empty, Time), null, cancellationToken);

    /// <summary>Signs the signed attributes of a pending signature with the test key.</summary>
    /// <param name="pending">The pending signature.</param>
    /// <returns>The signature value.</returns>
    private static byte[] Sign(IPendingSignature pending)
    {
        using var key = Signer.GetRSAPrivateKey()!;
        return key.SignData(pending.SignedAttributes.Span, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
