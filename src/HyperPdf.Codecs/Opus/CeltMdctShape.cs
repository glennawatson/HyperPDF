// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The window an inverse MDCT blends the ends of its block with, and how long the transform is.</summary>
internal readonly ref struct CeltMdctShape
{
    /// <summary>Initializes a new instance of the <see cref="CeltMdctShape"/> struct.</summary>
    /// <param name="window">The rising half of the power-complementary window.</param>
    /// <param name="overlap">The samples the window blends over.</param>
    /// <param name="shift">How many times the longest transform is halved.</param>
    internal CeltMdctShape(ReadOnlySpan<float> window, int overlap, int shift)
    {
        Window = window;
        Overlap = overlap;
        Shift = shift;
    }

    /// <summary>Gets the rising half of the power-complementary window.</summary>
    internal ReadOnlySpan<float> Window { get; }

    /// <summary>Gets the samples the window blends over.</summary>
    internal int Overlap { get; }

    /// <summary>Gets how many times the longest transform is halved.</summary>
    internal int Shift { get; }
}
