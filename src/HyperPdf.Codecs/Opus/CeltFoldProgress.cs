// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>What the bands before a band left for the folding.</summary>
/// <param name="LowbandOffset">The band to fold from, or zero for none yet.</param>
/// <param name="UpdateLowband">Whether the last band had enough bits to be folded from.</param>
internal readonly record struct CeltFoldProgress(int LowbandOffset, bool UpdateLowband);
