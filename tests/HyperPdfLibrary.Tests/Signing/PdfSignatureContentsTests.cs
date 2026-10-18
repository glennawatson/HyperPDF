// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signing;
using HyperPdfLibrary.Tests.Editing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Checks signature-reservation ownership and lease lifetime.</summary>
public sealed class PdfSignatureContentsTests
{
    /// <summary>A reservation beyond the shared pool's small-buffer limit.</summary>
    private const int LargeReservation = 80 * 1024;

    /// <summary>The larger retry reservation.</summary>
    private const int LargerReservation = LargeReservation * 2;

    /// <summary>A workspace retains zero-filled bytes across retries of different sizes.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reserve_Retry_KeepsEarlierContentsUntilDisposal(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var workspace = PdfSigningWorkspace.Open(document);
        var first = workspace.Reserve(LargeReservation, cancellationToken);
        var second = workspace.Reserve(LargerReservation, cancellationToken);
        var repeated = workspace.Reserve(LargeReservation, cancellationToken);
        var allZero = first.Buffer.AsSpan(0, first.Length).IndexOfAnyExcept((byte)0) < 0
            && second.Buffer.AsSpan(0, second.Length).IndexOfAnyExcept((byte)0) < 0;
        var same = ReferenceEquals(first.Buffer, repeated.Buffer);
        var separate = !ReferenceEquals(first.Buffer, second.Buffer);

        await Assert.That(allZero).IsTrue();
        await Assert.That(same).IsTrue();
        await Assert.That(separate).IsTrue();
        await Assert.That(first.Length).IsEqualTo(LargeReservation);
        await Assert.That(second.Length).IsEqualTo(LargerReservation);
    }

    /// <summary>Disposing a working copy returns its reservation to the original caller.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reserve_DisposedWorkspace_ReturnsToOriginalOwner(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var workspace = PdfSigningWorkspace.Open(document);
        var contents = workspace.Reserve(LargeReservation, cancellationToken);
        var owner = workspace.ScratchPools;
        var originalOwner = ReferenceEquals(owner, document.Objects.ScratchPools);
        workspace.Dispose();
        workspace.Dispose();
        var returned = owner.Bytes.Rent(LargeReservation);
        var same = ReferenceEquals(contents.Buffer, returned);
        owner.Bytes.Return(returned);

        await Assert.That(originalOwner).IsTrue();
        await Assert.That(workspace.Document.IsDisposed).IsTrue();
        await Assert.That(document.IsDisposed).IsFalse();
        await Assert.That(same).IsTrue();
    }

    /// <summary>Concurrent workspaces never share a live contents array.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reserve_SeparateWorkspaces_DoNotShareLiveContents(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var first = PdfSigningWorkspace.Open(document);
        using var second = PdfSigningWorkspace.Open(document);
        var firstContents = first.Reserve(LargeReservation, cancellationToken);
        var secondContents = second.Reserve(LargeReservation, cancellationToken);

        await Assert.That(ReferenceEquals(firstContents.Buffer, secondContents.Buffer)).IsFalse();
    }

    /// <summary>Invalid capacities fail before any reservation is allocated.</summary>
    /// <param name="capacity">The invalid reservation.</param>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(int.MaxValue)]
    public async Task Reserve_InvalidCapacity_ThrowsDomainError(int capacity, CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        using var workspace = PdfSigningWorkspace.Open(document);

        await Assert.That(() => workspace.Reserve(capacity, cancellationToken)).Throws<PdfException>();
        await Assert.That(document.Objects.ScratchPools.RetainedBytes).IsEqualTo(0);
    }

    /// <summary>Ended workspaces cannot create a new lease.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Reserve_DisposedWorkspace_ThrowsObjectDisposed(CancellationToken cancellationToken)
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateFlat(1), null);
        var workspace = PdfSigningWorkspace.Open(document);
        workspace.Dispose();

        await Assert.That(() => workspace.Reserve(LargeReservation, cancellationToken)).Throws<ObjectDisposedException>();
    }
}
