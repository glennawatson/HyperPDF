// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Flattening;
using HyperPdf.Core.Protection;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks that writing a copy of an encrypted document leaves the open document usable: the copies share the open document's
/// decryption handler, so closing one must not close it.
/// </summary>
public sealed class EncryptedCopyLifetimeTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>The characters of page text compared.</summary>
    private const int TextLength = 20;

    /// <summary>The owner password the document is opened with.</summary>
    private const string OwnerPassword = "owner-pass";

    /// <summary>The signing time every test uses.</summary>
    private static readonly DateTimeOffset Time = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The signer, made once because key generation is slow.</summary>
    private static readonly X509Certificate2 Signer = PdfSigning.CreateCertificate("Copy Lifetime Signer", null, false, TimeProvider.System);

    /// <summary>The kinds of copy and signing call that take a snapshot of the open document.</summary>
    public enum SnapshotUse
    {
        /// <summary>A flattened copy.</summary>
        Flatten = 0,

        /// <summary>A PDF/A copy.</summary>
        Archive = 1,

        /// <summary>A PDF 2.0 copy.</summary>
        Pdf20 = 2,

        /// <summary>A signing preview.</summary>
        SigningPreview = 3,

        /// <summary>A signed copy.</summary>
        Sign = 4,

        /// <summary>A prepared signature.</summary>
        Prepare = 5,

        /// <summary>A document timestamp.</summary>
        Timestamp = 6,
    }

    /// <summary>An encrypted document still reads, renders and saves after a copy or signing call, which used a snapshot of it.</summary>
    /// <param name="use">The call that takes the snapshot.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SnapshotUse.Flatten)]
    [Arguments(SnapshotUse.Archive)]
    [Arguments(SnapshotUse.Pdf20)]
    [Arguments(SnapshotUse.SigningPreview)]
    [Arguments(SnapshotUse.Sign)]
    [Arguments(SnapshotUse.Prepare)]
    [Arguments(SnapshotUse.Timestamp)]
    public async Task EncryptedDocumentStaysUsableAfterASnapshotCall(SnapshotUse use, CancellationToken cancellationToken)
    {
        var path = await WriteEncryptedFileAsync(cancellationToken);
        try
        {
            using var reference = new HyperPdfEngine().Open(path, OwnerPassword);
            var expectedText = reference.GetText(1, 0, TextLength);
            using var live = new HyperPdfEngine().Open(path, OwnerPassword);

            await UseSnapshotAsync(live, use, cancellationToken);

            await Assert.That(live.GetText(1, 0, TextLength)).IsEqualTo(expectedText);
            await Assert.That(new PagePixels(live, 0, RenderFlags.Annotations).Rendered).IsTrue();
            await using var saved = new MemoryStream();
            _ = DocumentFeature<IAnnotationEditor>.Cast(live)!.Save(saved);
            await Assert.That(saved.Length).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A plain document's file is free to open without sharing once the document closes after a snapshot call, which disposes its working copy.</summary>
    /// <param name="use">The call that takes the snapshot.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SnapshotUse.Flatten)]
    [Arguments(SnapshotUse.Archive)]
    [Arguments(SnapshotUse.Pdf20)]
    [Arguments(SnapshotUse.SigningPreview)]
    [Arguments(SnapshotUse.Sign)]
    [Arguments(SnapshotUse.Prepare)]
    [Arguments(SnapshotUse.Timestamp)]
    public async Task PlainFileIsNotHeldAfterASnapshotCall(SnapshotUse use, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-plain-copy-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(Pages), cancellationToken);
        try
        {
            using (var live = new HyperPdfEngine().Open(path, null))
            {
                await UseSnapshotAsync(live, use, cancellationToken);
                await Assert.That(live.GetText(1, 0, TextLength)).IsNotEmpty();
            }

            // Windows refuses an exclusive open while any handle to the file remains.
            await using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            await Assert.That(exclusive.Length).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Disposing a snapshot closes only the snapshot: the open document, plain or encrypted, still reads, renders and saves.</summary>
    /// <param name="encrypted">Whether the document is encrypted.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task DisposingASnapshotLeavesTheOpenDocumentUsable(bool encrypted, CancellationToken cancellationToken)
    {
        var path = encrypted ? await WriteEncryptedFileAsync(cancellationToken) : Path.Combine(Path.GetTempPath(), $"hyperpdf-plain-snapshot-{Guid.NewGuid():N}.pdf");
        if (!encrypted)
        {
            await File.WriteAllBytesAsync(path, TestPdf.Create(Pages), cancellationToken);
        }

        try
        {
            using var live = (HyperPdfDocument)new HyperPdfEngine().Open(path, encrypted ? OwnerPassword : null);
            var expectedText = live.GetText(1, 0, TextLength);

            var snapshot = HyperPdfOptimization.TakeSnapshot(live);
            await Assert.That(snapshot.IsDisposed).IsFalse();
            snapshot.Dispose();

            await Assert.That(snapshot.IsDisposed).IsTrue();
            await Assert.That(live.GetText(1, 0, TextLength)).IsEqualTo(expectedText);
            await Assert.That(new PagePixels(live, 0, RenderFlags.Annotations).Rendered).IsTrue();
            await using var saved = new MemoryStream();
            _ = DocumentFeature<IAnnotationEditor>.Cast(live)!.Save(saved);
            await Assert.That(saved.Length).IsGreaterThan(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Takes the snapshot with a call. A refusal is fine: the call still took and released its snapshot.</summary>
    /// <param name="live">The open document.</param>
    /// <param name="use">The call.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task.</returns>
    private static async Task UseSnapshotAsync(IDocument live, SnapshotUse use, CancellationToken cancellationToken)
    {
        var request = new SigningRequest(1, "Approved", "Brisbane", Time);
        try
        {
            switch (use)
            {
                case SnapshotUse.Flatten:
                    {
                        await using var output = new MemoryStream();
                        _ = await DocumentFeature<IFlattenCopyWriter>.Cast(live)!.WriteFlattenedCopyAsync(output, FlattenCopyOptions.Default, cancellationToken);
                        break;
                    }

                case SnapshotUse.Archive:
                    {
                        await using var output = new MemoryStream();
                        _ = await DocumentFeature<IArchiveCopyWriter>.Cast(live)!.WriteArchiveCopyAsync(output, ArchiveLevel.PdfA2B, cancellationToken);
                        break;
                    }

                case SnapshotUse.Pdf20:
                    {
                        await using var output = new MemoryStream();
                        _ = await DocumentFeature<IPdf20CopyWriter>.Cast(live)!.WritePdf20CopyAsync(output, cancellationToken);
                        break;
                    }

                case SnapshotUse.SigningPreview:
                    {
                        _ = DocumentFeature<IDocumentSigner>.Get(live)!.Preview(request);
                        break;
                    }

                case SnapshotUse.Sign:
                    {
                        _ = await DocumentFeature<IDocumentSigner>.Get(live)!.SignAsync(Signer, request, null, cancellationToken);
                        break;
                    }

                case SnapshotUse.Prepare:
                    {
                        _ = await DocumentFeature<IDocumentSigner>.Get(live)!.PrepareAsync(new([Signer.RawData], SigningKeyKind.RsaPkcs1), request, null, cancellationToken);
                        break;
                    }

                case SnapshotUse.Timestamp:
                    {
                        _ = await DocumentFeature<IDocumentSigner>.Get(live)!.AddDocumentTimestampAsync(new RefusingTimestamper(), SigningDigest.Sha256, cancellationToken);
                        break;
                    }
            }
        }
        catch (Exception ex) when (ex is SigningRefusedException or InvalidDataException or NotSupportedException)
        {
            // The snapshot was taken and released before the refusal.
        }
    }

    /// <summary>Writes an encrypted copy of a generated document to a temporary file.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The file's path.</returns>
    private static async Task<string> WriteEncryptedFileAsync(CancellationToken cancellationToken)
    {
        using var plain = new HyperPdfTestDocument(TestPdf.Create(Pages));
        await using var output = new MemoryStream();
        var request = new ProtectionRequest { Format = ProtectionFormat.Strong, UserPassword = "user-pass", OwnerPassword = OwnerPassword, Permissions = ProtectionPermissions.All };
        var result = await DocumentFeature<IDocumentProtector>.Cast(plain.Document)!.ProtectAsync(request, output, cancellationToken);
        await Assert.That(result.Written).IsTrue().Because(result.Refusal ?? string.Empty);
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-encrypted-copy-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, output.ToArray(), cancellationToken);
        return path;
    }

    /// <summary>A timestamp authority that has no answer, so the timestamp call ends in a refusal after it took its snapshot.</summary>
    private sealed class RefusingTimestamper : ISignatureTimestamper
    {
        /// <inheritdoc/>
        public int EstimatedTokenSize => 0;

        /// <inheritdoc/>
        public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, SigningDigest digest, CancellationToken cancellationToken) =>
            throw new NotSupportedException("No timestamp authority.");
    }
}
