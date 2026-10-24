// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Codecs.Opus;

/// <summary>The single precision constants of CELT, read straight from the bit patterns in <see cref="CeltTables"/>.</summary>
internal static class CeltFloatTables
{
    /// <summary>Gets the rising half of the power-complementary window that blends neighbouring blocks.</summary>
    internal static ReadOnlySpan<float> Window => MemoryMarshal.Cast<uint, float>(CeltTables.WindowBits);

    /// <summary>Gets the gains of the comb filter's three taps for each of its three tap sets.</summary>
    internal static ReadOnlySpan<float> CombGains => MemoryMarshal.Cast<uint, float>(CeltTables.CombGainBits);
}
