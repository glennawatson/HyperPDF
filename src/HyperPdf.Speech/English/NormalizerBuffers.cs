// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>The two buffers <see cref="TextNormalizer"/> writes its steps into, one step reading what the last wrote.</summary>
internal sealed class NormalizerBuffers
{
    /// <summary>Gets the buffer the first step writes into.</summary>
    internal SpokenText First { get; } = new();

    /// <summary>Gets the buffer the second step writes into.</summary>
    internal SpokenText Second { get; } = new();
}
