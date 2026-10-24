// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>One VLC codeword of the HT cleanup pass.</summary>
/// <param name="Bits">The codeword, first bit lowest.</param>
/// <param name="Length">The codeword's bits.</param>
/// <param name="Known">The EMB e-k bits: samples whose top magnitude bit the codeword gives.</param>
/// <param name="Ones">The EMB e1 bits: the values of those top bits.</param>
internal readonly record struct JpxTestHtCode(int Bits, int Length, int Known, int Ones);
