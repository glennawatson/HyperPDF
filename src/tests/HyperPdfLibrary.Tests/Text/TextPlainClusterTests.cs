// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Checks the plain-text shortcut that skips cluster segmentation.</summary>
public sealed class TextPlainClusterTests
{
    /// <summary>Plain Latin text is recognised and every character starts a cluster.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsciiAndLatinOneAreCountedPlain()
    {
        foreach (var text in new[] { string.Empty, "Hello, world.\n\tTabbed", "café ß © ÿ" })
        {
            await Assert.That(TextClusters.IsPlain(text)).IsTrue();
            await Assert.That(AllStart(text)).IsTrue();
        }
    }

    /// <summary>Text that can join characters is never counted plain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JoiningTextIsNotPlain()
    {
        string[] texts =
        [
            "é",
            "क्ष",
            "कि",
            "👨‍👩‍👧",
            "🇦🇺",
            "a\r\nb",
            "a️b",
            "؀a",
            "א",
        ];
        foreach (var text in texts)
        {
            await Assert.That(TextClusters.IsPlain(text)).IsFalse();
        }
    }

    /// <summary>Every string the shortcut calls plain really has one cluster per character.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryBmpCharacterCalledPlainIsItsOwnCluster()
    {
        for (var value = 0; value <= char.MaxValue; value++)
        {
            var pair = new string([(char)value, 'x', (char)value]);
            if (TextClusters.IsPlain(pair))
            {
                await Assert.That(AllStart(pair)).IsTrue();
            }
        }
    }

    /// <summary>Checks that every character of a text starts a cluster.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether every cluster start bit is set.</returns>
    private static bool AllStart(string text)
    {
        var starts = TextClusters.FindStarts(text);
        for (var i = 0; i < text.Length; i++)
        {
            if (TextClusters.StartOf(starts, i) != i)
            {
                return false;
            }
        }

        return true;
    }
}
