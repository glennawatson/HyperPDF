// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Speech.English;

namespace HyperPdf.Speech.Melo;

/// <summary>The buffers owned by one Melo engine while it prepares written text for speech.</summary>
internal sealed class MeloNormalization
{
    /// <summary>Gets the text after camel case and quote preparation.</summary>
    internal SpokenText Input { get; } = new();

    /// <summary>Gets the buffers used to say numbers, dates and symbols.</summary>
    internal NormalizerBuffers Steps { get; } = new();

    /// <summary>Gets the normalized text after invariant lowercase casing.</summary>
    internal SpokenText Output { get; } = new();
}
