// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A fill that shades from one colour to another across a page object.</summary>
/// <param name="Start">The first colour as six hexadecimal digits of red, green and blue.</param>
/// <param name="End">The second colour as six hexadecimal digits of red, green and blue.</param>
/// <param name="IsRadial"><see langword="true"/> to shade outward from the middle; <see langword="false"/> to shade along a line.</param>
/// <param name="AngleDegrees">For a line, the direction in degrees: 0 runs left to right and 90 runs bottom to top.</param>
[DebuggerDisplay("PageGradient: {Start:X6} to {End:X6}, radial {IsRadial}, {AngleDegrees} degrees")]
public readonly record struct PageGradient(uint Start, uint End, bool IsRadial, float AngleDegrees);
