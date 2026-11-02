// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The adaptive frequency counts one object's blocks share while they are decoded, one histogram for each adaptive context.</summary>
internal sealed class U3dContextSet
{
    /// <summary>The histograms, made when a context is first used. Slot 0 is the plain context and is never used.</summary>
    private readonly U3dHistogram?[] _histograms = new U3dHistogram?[U3dContexts.AdaptiveCount + 1];

    /// <summary>Gets the histogram of an adaptive context, making it on first use.</summary>
    /// <param name="context">The adaptive context, from 1 up to the number of adaptive contexts.</param>
    /// <returns>The histogram.</returns>
    internal U3dHistogram For(uint context) => _histograms[context] ??= new();
}
