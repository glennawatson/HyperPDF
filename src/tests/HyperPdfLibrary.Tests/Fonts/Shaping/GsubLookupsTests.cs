// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.Tests.Fonts.Shaping;

/// <summary>Checks run-owned ligature identifiers used to attach marks.</summary>
public sealed class GsubLookupsTests
{
    /// <summary>The boundary of the former 24-bit identifier range.</summary>
    private const int PackedIdentifierLimit = 0xFFFFFF;

    /// <summary>The second identifier in a run.</summary>
    private const int SecondIdentifier = 2;

    /// <summary>Independent runs do not advance each other's identifier state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NextLigatureId_SeparateBuffers_UsesSeparateState()
    {
        using var first = new GlyphBuffer(1);
        using var second = new GlyphBuffer(1);
        var firstId = GsubLookups.NextLigatureId(ref first.LigatureIds);
        var secondId = GsubLookups.NextLigatureId(ref second.LigatureIds);
        var nextId = GsubLookups.NextLigatureId(ref first.LigatureIds);

        await Assert.That(firstId).IsEqualTo(1);
        await Assert.That(secondId).IsEqualTo(1);
        await Assert.That(nextId).IsEqualTo(SecondIdentifier);
    }

    /// <summary>Reusing a buffer begins a fresh identifier range.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NextLigatureId_ClearedBuffer_StartsNewRun()
    {
        using var buffer = new GlyphBuffer(1);
        _ = GsubLookups.NextLigatureId(ref buffer.LigatureIds);
        buffer.Clear(1);

        await Assert.That(GsubLookups.NextLigatureId(ref buffer.LigatureIds)).IsEqualTo(1);
    }

    /// <summary>Long runs do not silently reuse identifiers at the 24-bit boundary.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NextLigatureId_PackedBoundary_DoesNotWrap()
    {
        using var buffer = new GlyphBuffer(1);
        buffer.LigatureIds = PackedIdentifierLimit;

        await Assert.That(GsubLookups.NextLigatureId(ref buffer.LigatureIds)).IsEqualTo(PackedIdentifierLimit + 1);
    }

    /// <summary>An exhausted range reports a domain error without changing the state.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task NextLigatureId_ExhaustedRange_ThrowsDomainError()
    {
        using var buffer = new GlyphBuffer(1);
        buffer.LigatureIds = int.MaxValue;

        await Assert.That(() => GsubLookups.NextLigatureId(ref buffer.LigatureIds)).Throws<PdfException>();
        await Assert.That(buffer.LigatureIds).IsEqualTo(int.MaxValue);
    }
}
