// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Signing works on a private copy, so it neither edits the document nor collides with other edits and other signers.</summary>
public sealed class ConcurrentSigningTests
{
    /// <summary>The threads that sign one document together.</summary>
    private const int Signers = 8;

    /// <summary>The metadata edits made while another thread signs.</summary>
    private const int Edits = 300;

    /// <summary>The label of a transaction the caller keeps open.</summary>
    private const string CallerEdit = "Caller edit";

    /// <summary>Signing the same document from several threads at once gives a valid signed file to each of them.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParallelSignsOfOneDocumentAllSucceed(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        using var start = new Barrier(Signers);
        var job = new SignJob(document, new() { SigningTime = SigningFixtures.Time }, new(SignatureFixtures.Signer), start, cancellationToken);
        var tasks = new Task<PdfSigningResult>[Signers];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = DedicatedThread.Run(job, static work => work.SignAfterStart(), cancellationToken);
        }

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            await Assert.That(SignatureFixtures.ValidateSingle(result.Bytes).SignatureValid).IsTrue();
        }
    }

    /// <summary>Signing leaves the document, its signatures and a transaction the caller has open as they were.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task SigningLeavesTheDocumentAndAnOpenTransactionAlone(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        var options = new PdfSigningOptions { SigningTime = SigningFixtures.Time };
        var revision = Volatile.Read(ref document.State.ObjectsRevision);
        using var transaction = PdfDocumentEditing.BeginEdit(document, CallerEdit);

        var signed = PdfDocumentSigning.Sign(document, options, new(SignatureFixtures.Signer));
        var prepared = await PdfDocumentSigning.PrepareAsync(document, options, new([SignatureFixtures.Signer.RawData], PdfSignatureAlgorithm.RsaPkcs1), cancellationToken);

        using (Assert.Multiple())
        {
            await Assert.That(signed.Bytes.Length).IsGreaterThan(SignatureFixtures.Blank().Length);
            await Assert.That(prepared.ContentsCapacity).IsGreaterThan(0);
            await Assert.That(transaction.IsOpen).IsTrue();
            await Assert.That(Volatile.Read(ref document.State.ObjectsRevision)).IsEqualTo(revision);
            await Assert.That(PdfDocumentAttachments.GetSignatures(document).Count).IsEqualTo(0);
        }
    }

    /// <summary>Adding validation data builds its update on a copy, so a transaction the caller has open is not in the way.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationDataUpdateLeavesTheDocumentAlone(CancellationToken cancellationToken)
    {
        var signed = SigningFixtures.Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, new() { Certification = PdfMdpPermission.NoChanges });
        using var document = PdfDocumentReader.Open(signed, null);
        var revision = Volatile.Read(ref document.State.ObjectsRevision);
        using var transaction = PdfDocumentEditing.BeginEdit(document, CallerEdit);

        var updated = await PdfDocumentSigning.AddValidationDataAsync(document, [new([], [SignatureFixtures.Signer.RawData], [], [])], false, cancellationToken);
        using var reopened = PdfDocumentReader.Open(updated.Bytes, null);

        using (Assert.Multiple())
        {
            await Assert.That(PdfDocumentSignatureValidation.GetSecurityStore(reopened).Certificates.Length).IsEqualTo(1);
            await Assert.That(transaction.IsOpen).IsTrue();
            await Assert.That(Volatile.Read(ref document.State.ObjectsRevision)).IsEqualTo(revision);
        }
    }

    /// <summary>Edits made while another thread signs are all kept, and signing is never refused because of them.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditsMadeWhileSigningAreNotLost(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(SignatureFixtures.Blank(), null);
        using var start = new Barrier(1);
        var job = new SignJob(document, new() { SigningTime = SigningFixtures.Time, VerifyOutput = false }, new(SignatureFixtures.Signer), start, cancellationToken);
        var worker = DedicatedThread.Run(job, static work => work.SignUntilStopped(), cancellationToken);

        var lost = 0;
        for (var i = 0; i < Edits; i++)
        {
            var title = $"Title {i}";
            PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = title });
            lost += PdfDocumentMetadata.GetInfo(document).Title == title ? 0 : 1;
        }

        job.Stop();
        var signs = await worker;

        await Assert.That(lost).IsEqualTo(0);
        await Assert.That(signs).IsGreaterThan(0);
    }

    /// <summary>The arguments of a thread that signs a document.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Options">The signing options.</param>
    /// <param name="Signer">The signer.</param>
    /// <param name="Start">The barrier the signing threads meet at.</param>
    /// <param name="CancellationToken">Cancels the waiting.</param>
    private sealed record SignJob(PdfDocument Document, PdfSigningOptions Options, PdfCertificateSigner Signer, Barrier Start, CancellationToken CancellationToken)
    {
        /// <summary>Set to 1 to end <see cref="SignUntilStopped"/>.</summary>
        private int _stop;

        /// <summary>Waits for the other signing threads, then signs once.</summary>
        /// <returns>The signed file.</returns>
        internal PdfSigningResult SignAfterStart()
        {
            Start.SignalAndWait(CancellationToken);
            return PdfDocumentSigning.Sign(Document, Options, Signer);
        }

        /// <summary>Signs at least once, and until <see cref="Stop"/> is called.</summary>
        /// <returns>The number of files signed.</returns>
        internal int SignUntilStopped()
        {
            var signs = 0;
            do
            {
                _ = PdfDocumentSigning.Sign(Document, Options, Signer);
                signs++;
            }
            while (Volatile.Read(ref _stop) == 0);

            return signs;
        }

        /// <summary>Ends <see cref="SignUntilStopped"/> after the file it is signing.</summary>
        internal void Stop() => Volatile.Write(ref _stop, 1);
    }
}
