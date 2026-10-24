// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A long-term reference picture named by a slice header.</summary>
/// <param name="PocLsb">The low bits of the picture's order count.</param>
/// <param name="Used">Whether the current picture refers to it.</param>
/// <param name="MsbPresent">Whether the high bits of the order count are given.</param>
/// <param name="DeltaPocMsbCycle">How many cycles of the low bits the picture is behind the current one, summed over the entries before it.</param>
internal readonly record struct H265LongTermRef(int PocLsb, bool Used, bool MsbPresent, int DeltaPocMsbCycle);
