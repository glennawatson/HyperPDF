// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A value made while the cache is cleared is used by its caller but not kept.</summary>
public sealed class ObjectCacheClearTests
{
    /// <summary>The key of the cached value.</summary>
    private const int Key = 7;

    /// <summary>The times a value was made when the first was made across a clear.</summary>
    private const int MadeTwice = 2;

    /// <summary>A value made before a clear is returned once and made again on the next request.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValueMadeAcrossAClearIsNotKept()
    {
        var cache = new ObjectCache<int, string>();
        var factory = new CountingFactory(cache);

        var first = cache.GetOrCreate(Key, factory, static (key, state) => state.MakeAndClear(key));
        var second = cache.GetOrCreate(Key, factory, static (key, state) => state.Make(key));
        var third = cache.GetOrCreate(Key, factory, static (key, state) => state.Make(key));

        await Assert.That(first).IsEqualTo($"stale {Key}");
        await Assert.That(second).IsEqualTo($"fresh {Key}");
        await Assert.That(third).IsEqualTo($"fresh {Key}");
        await Assert.That(factory.Made).IsEqualTo(MadeTwice);
    }

    /// <summary>A value made with no clear in between is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValueMadeWithoutAClearIsKept()
    {
        var cache = new ObjectCache<int, string>();
        var factory = new CountingFactory(cache);

        var first = cache.GetOrCreate(Key, factory, static (key, state) => state.Make(key));
        var second = cache.GetOrCreate(Key, factory, static (key, state) => state.Make(key));

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(cache.Count).IsEqualTo(1);
        await Assert.That(factory.Made).IsEqualTo(1);
    }

    /// <summary>Makes values for a cache and counts them.</summary>
    /// <param name="cache">The cache the values are made for.</param>
    private sealed class CountingFactory(ObjectCache<int, string> cache)
    {
        /// <summary>Gets the number of values made.</summary>
        public int Made { get; private set; }

        /// <summary>Makes a value.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        public string Make(int key)
        {
            Made++;
            return $"fresh {key}";
        }

        /// <summary>Makes a value while the cache is cleared, as a clear on another thread would.</summary>
        /// <param name="key">The key.</param>
        /// <returns>The value.</returns>
        public string MakeAndClear(int key)
        {
            Made++;
            cache.Clear();
            return $"stale {key}";
        }
    }
}
