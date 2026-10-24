// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The long-term reference pictures a sequence parameter set lists for slices to pick from.</summary>
/// <param name="Present">Whether slices may use long-term reference pictures.</param>
/// <param name="PocLsb">The low bits of the picture order count of each candidate.</param>
/// <param name="Used">Whether the current picture refers to each candidate.</param>
internal sealed record H265LongTermSps(bool Present, int[] PocLsb, bool[] Used);
