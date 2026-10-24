// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;
using TUnit.Assertions;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks optional document feature lookup and its lifetime.</summary>
[NotInParallel]
public sealed class DocumentFeatureLookupTests
{
    /// <summary>A contract that neither engine implements.</summary>
    private interface IUnsupportedFeature : IDocumentFeature
    {
        /// <summary>Gets the marker value.</summary>
        int Marker { get; }
    }

    /// <summary>HyperPDF returns the same lazily created feature instance for its owner.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HyperPdfFeatureInstancesAreStableAndWarmLookupsAllocateNothing()
    {
        using var pair = new HyperPdfTestDocument(TestPdf.Create(1));
        const int Warmup = 128;
        const int Measured = 1024;
        var first = DocumentFeature<IAnnotationEditor>.Get(pair.Document);

        for (var index = 0; index < Warmup; index++)
        {
            _ = DocumentFeature<IAnnotationEditor>.Get(pair.Document);
        }

        var mismatches = 0;
        var allocated = AllocationProbe.Minimum(() =>
        {
            for (var index = 0; index < Measured; index++)
            {
                if (!ReferenceEquals(first, DocumentFeature<IAnnotationEditor>.Get(pair.Document)))
                {
                    mismatches++;
                }
            }
        });
        await Assert.That(first).IsNotNull();
        await Assert.That(mismatches).IsEqualTo(0);
        await Assert.That(allocated).IsEqualTo(0L);
    }

    /// <summary>Unsupported features return null, while explicit casts retain their failure behavior.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsupportedAndNullFeatureRequestsHaveDefinedResults()
    {
        using var pair = new HyperPdfTestDocument(TestPdf.Create(1));

        await Assert.That(DocumentFeature<IUnsupportedFeature>.Get(pair.Document)).IsNull();
        await Assert.That(DocumentFeature<IUnsupportedFeature>.Cast(null)).IsNull();
        await Assert.That(() => pair.Document.GetFeature(null!)).Throws<ArgumentNullException>();
        await Assert.That(() => DocumentFeature<IUnsupportedFeature>.Cast(pair.Document)).Throws<InvalidCastException>();
    }

    /// <summary>A previously obtained feature still observes owner disposal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FeatureCallsRespectOwnerDisposal()
    {
        using var pair = new HyperPdfTestDocument(TestPdf.Create(1));
        var layout = DocumentFeature<ITextLayoutSource>.Get(pair.Document)!;
        pair.Document.Dispose();
        var characters = new List<PageCharacter>();

        await Assert.That(() => DocumentFeature<ITextLayoutSource>.Get(pair.Document)).Throws<ObjectDisposedException>();
        layout.GetCharacters(0, characters);
        await Assert.That(characters).IsEmpty();
    }
}
