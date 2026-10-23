// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests.Speech.Melo;

/// <summary>A word g2p_en's network predicted.</summary>
/// <param name="Word">The word.</param>
/// <param name="Phones">The predicted ARPAbet phones.</param>
internal sealed record MeloReferencePrediction(string Word, string[] Phones);
