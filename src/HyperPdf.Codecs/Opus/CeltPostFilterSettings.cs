// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The parameters of the comb filter that follows a CELT block.</summary>
/// <param name="Period">The pitch period in samples.</param>
/// <param name="Gain">The gain of the filter.</param>
/// <param name="Tapset">Which of the three tap sets is used.</param>
internal readonly record struct CeltPostFilterSettings(int Period, float Gain, int Tapset);
