// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The thresholds of the weak deblocking filter.</summary>
/// <param name="Tc">The clipping threshold.</param>
/// <param name="FilterP1">Whether the second sample on the P side may change.</param>
/// <param name="FilterQ1">Whether the second sample on the Q side may change.</param>
/// <param name="Maximum">The largest sample value.</param>
internal readonly record struct H265WeakLimits(int Tc, bool FilterP1, bool FilterQ1, int Maximum);
