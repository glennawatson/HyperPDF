// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>Where a band folds its spectrum from and which of the short blocks there had pulses.</summary>
/// <param name="Lowband">The first entry to fold from, or -1 for none.</param>
/// <param name="FirstMask">The blocks with pulses in the first channel.</param>
/// <param name="SecondMask">The blocks with pulses in the second channel.</param>
internal readonly record struct CeltFold(int Lowband, uint FirstMask, uint SecondMask);
