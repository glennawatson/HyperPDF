// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>The font program and its optional CID mapping streams.</summary>
/// <param name="Kind">The program format.</param>
/// <param name="Data">The program bytes.</param>
/// <param name="CidSet">The glyph-presence bitmap.</param>
/// <param name="CidToGidMap">The CID-to-glyph map.</param>
internal readonly record struct EmbeddedProgramData(EmbeddedProgramKind Kind, byte[] Data, byte[]? CidSet, byte[]? CidToGidMap);
