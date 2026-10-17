// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>A decoded binary CMap from a font resource pack.</summary>
/// <param name="Parent">The name of the CMap it builds on, or an empty string.</param>
/// <param name="Vertical">Whether the writing mode is vertical.</param>
/// <param name="Codespaces">The codespace ranges in file order.</param>
/// <param name="Ranges">The CID ranges in file order.</param>
/// <param name="Unicode">Single-character CID-to-Unicode values.</param>
internal readonly record struct BinaryCMapData(string Parent, bool Vertical, Codespace[] Codespaces, CidRange[] Ranges, int[] Unicode);
