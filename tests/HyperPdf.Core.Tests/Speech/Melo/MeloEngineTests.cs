// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Speech.Melo;

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>Tests Melo's text preparation without loading a voice model.</summary>
public sealed class MeloEngineTests
{
    /// <summary>Reused preprocessing and normalization buffers retain no words from earlier sentences.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReusesNormalizationBuffers()
    {
        var buffers = new MeloNormalization();
        var first = MeloEngine.Normalize("Read $1.50.", false, buffers).ToString();
        var second = MeloEngine.Normalize("HelloWorld", false, buffers).ToString();
        var empty = MeloEngine.Normalize(string.Empty, false, buffers).ToString();

        await Assert.That(first).IsEqualTo("read 1 dollar and 50 cents.");
        await Assert.That(second).IsEqualTo("hello world");
        await Assert.That(empty).IsEqualTo(string.Empty);
    }
}
