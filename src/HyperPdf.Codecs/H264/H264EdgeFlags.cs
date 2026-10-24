// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Which macroblock edges of a macroblock are filtered.</summary>
/// <param name="Left">Whether the edge shared with the macroblock to the left is filtered.</param>
/// <param name="Top">Whether the edge shared with the macroblock above is filtered.</param>
internal readonly record struct H264EdgeFlags(bool Left, bool Top);
