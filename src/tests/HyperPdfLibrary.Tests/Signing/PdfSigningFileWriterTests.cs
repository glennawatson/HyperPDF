// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Checks signed byte-range preparation and bounded contents patching.</summary>
public sealed class PdfSigningFileWriterTests
{
    /// <summary>The length of the original-file prefix.</summary>
    private const int PrefixLength = 7;

    /// <summary>The reservation in the sample.</summary>
    private const int Capacity = 2;

    /// <summary>The hexadecimal characters written for each byte.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>Gets a synthetic signature update with a fixed-width byte-range placeholder.</summary>
    private static ReadOnlySpan<byte> Sample =>
        "prefix\n[0 1111111111 2222222222 3333333333] /Contents <0000> tail"u8;

    /// <summary>Invalid incremental-update offsets produce domain errors.</summary>
    /// <param name="offset">The update offset.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    [Arguments(int.MaxValue)]
    public async Task Prepare_InvalidUpdateOffset_ThrowsDomainError(int offset)
    {
        var bytes = Sample.ToArray();

        await Assert.That(() => PdfSigningFileWriter.Prepare(bytes, offset, Capacity, CancellationToken.None)).Throws<PdfException>();
    }

    /// <summary>Invalid lengths cannot overflow a span slice or reservation calculation.</summary>
    /// <param name="capacity">The reservation.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(int.MinValue)]
    [Arguments(int.MaxValue)]
    public async Task Prepare_InvalidReservation_ThrowsDomainError(int capacity)
    {
        var bytes = Sample.ToArray();

        await Assert.That(() => PdfSigningFileWriter.Prepare(bytes, PrefixLength, capacity, CancellationToken.None)).Throws<PdfException>();
    }

    /// <summary>Only the byte range and reserved contents change.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryWriteContents_FittingValue_PreservesSignedRanges()
    {
        var bytes = Sample.ToArray();
        var prepared = PdfSigningFileWriter.Prepare(bytes, PrefixLength, Capacity, CancellationToken.None);
        var head = prepared.File.AsSpan(0, prepared.ContentsStart).ToArray();
        var tail = prepared.File.AsSpan(prepared.ContentsEnd).ToArray();
        var written = PdfSigningFileWriter.TryWriteContents(prepared, [1, 0xAB], CancellationToken.None);
        var contents = Encoding.ASCII.GetString(bytes.AsSpan(prepared.ContentsStart + 1, Capacity * DigitsPerByte));

        await Assert.That(written).IsTrue();
        await Assert.That(contents).IsEqualTo("01AB");
        await Assert.That(prepared.File.AsSpan(0, prepared.ContentsStart).ToArray()).IsEquivalentTo(head);
        await Assert.That(prepared.File.AsSpan(prepared.ContentsEnd).ToArray()).IsEquivalentTo(tail);
    }

    /// <summary>A value larger than the reservation leaves the complete file unchanged.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryWriteContents_ExcessiveValue_DoesNotWrite()
    {
        var bytes = Sample.ToArray();
        var prepared = PdfSigningFileWriter.Prepare(bytes, PrefixLength, Capacity, CancellationToken.None);
        var before = bytes.ToArray();
        var written = PdfSigningFileWriter.TryWriteContents(prepared, [1, 0xAB, 0xCD], CancellationToken.None);

        await Assert.That(written).IsFalse();
        await Assert.That(bytes).IsEquivalentTo(before);
    }

    /// <summary>Malformed or absent reservations produce a domain error.</summary>
    /// <param name="contents">The malformed update.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments("[0 1111111111 2222222222 3333333333] /Contents <000>")]
    [Arguments("[0 1111111111 2222222222 3333333333] /Contents <000X>")]
    [Arguments("/Contents <0000>")]
    [Arguments("[0 1111111111 2222222222 3333333333]")]
    public async Task Prepare_MalformedUpdate_ThrowsDomainError(string contents)
    {
        var bytes = Encoding.ASCII.GetBytes(contents);

        await Assert.That(() => PdfSigningFileWriter.Prepare(bytes, 0, Capacity, CancellationToken.None)).Throws<PdfException>();
    }

    /// <summary>A later valid reservation can follow an unrelated, shorter contents string.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Prepare_WrongCandidateBeforeReservation_FindsExactLength()
    {
        var bytes = "[0 1111111111 2222222222 3333333333] /Contents <00> /Contents <0000> tail"u8.ToArray();
        var prepared = PdfSigningFileWriter.Prepare(bytes, 0, Capacity, CancellationToken.None);
        var tail = Encoding.ASCII.GetString(prepared.File.AsSpan(prepared.ContentsEnd));

        await Assert.That(tail).IsEqualTo(" tail");
    }

    /// <summary>Cancelled writes do not change the prepared file.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TryWriteContents_Cancelled_LeavesFileUnchanged()
    {
        var bytes = Sample.ToArray();
        var prepared = PdfSigningFileWriter.Prepare(bytes, PrefixLength, Capacity, CancellationToken.None);
        var before = bytes.ToArray();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(() => PdfSigningFileWriter.TryWriteContents(prepared, [1], cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(bytes).IsEquivalentTo(before);
    }
}
