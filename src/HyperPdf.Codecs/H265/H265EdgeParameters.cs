// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>The settings that decide how an edge segment is filtered.</summary>
/// <param name="Strength">The boundary filtering strength.</param>
/// <param name="QpAverage">The rounded average of the quantisers of the two sides, plus any chroma offset.</param>
/// <param name="TcOffsetDiv2">Half the clipping threshold offset of the slice that holds the Q side.</param>
/// <param name="BetaOffsetDiv2">Half the strength threshold offset of the slice that holds the Q side.</param>
/// <param name="BitDepth">The bit depth of the colour component.</param>
/// <param name="KeepP">Whether the samples on the P side must be left as they are.</param>
/// <param name="KeepQ">Whether the samples on the Q side must be left as they are.</param>
internal readonly record struct H265EdgeParameters(int Strength, int QpAverage, int TcOffsetDiv2, int BetaOffsetDiv2, int BitDepth, bool KeepP, bool KeepQ);
