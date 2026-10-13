// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks which generated appearances the cache keeps.</summary>
public sealed class GeneratedAppearanceCacheTests
{
    /// <summary>The revision of the document's objects used by the tests.</summary>
    private const int Revision = 1;

    /// <summary>An appearance is kept and handed out again at the same revision.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnAppearanceIsKeptAtItsRevision()
    {
        var cache = new GeneratedAppearanceCache();
        var annotation = new PdfDictionary(null);
        var made = new int[1];

        var first = cache.GetOrCreate(annotation, Revision, made, Create);
        var second = cache.GetOrCreate(annotation, Revision, made, Create);

        await Assert.That(made[0]).IsEqualTo(1);
        await Assert.That(second).IsEqualTo(first);
        await Assert.That(cache.Count).IsEqualTo(1);
    }

    /// <summary>An appearance made while the cache was cleared is returned to its caller but not stored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnAppearanceMadeAcrossAClearIsNotStored()
    {
        var cache = new GeneratedAppearanceCache();
        var annotation = new PdfDictionary(null);

        var created = cache.GetOrCreate(
            annotation,
            Revision,
            0,
            state =>
            {
                // A font change clears the cache while this appearance is still being laid out with the old fonts.
                cache.Clear();
                return new GeneratedAppearance(new(new PdfDictionary(null), []), null, default);
            });

        await Assert.That(created).IsNotNull();
        await Assert.That(cache.Count).IsEqualTo(0);
    }

    /// <summary>Makes an appearance and counts it.</summary>
    /// <param name="made">Holds the count of appearances made.</param>
    /// <returns>The appearance.</returns>
    private static GeneratedAppearance? Create(int[] made)
    {
        made[0]++;
        return new(new(new PdfDictionary(null), []), null, default);
    }
}
