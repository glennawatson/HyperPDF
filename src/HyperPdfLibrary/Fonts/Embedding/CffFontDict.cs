// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>A Font DICT's contribution to a subset: the entries copied as they are and the Private DICT with its local subroutines.</summary>
/// <param name="Kept">The Font DICT's entries other than <c>Private</c> and <c>FontName</c>; empty for a font that is not CID-keyed.</param>
/// <param name="PrivateKept">The Private DICT's entries other than <c>Subrs</c>.</param>
/// <param name="Subrs">The local subroutines, empty when the Private DICT has none.</param>
[DebuggerDisplay("CffFontDict: {Subrs.Count} local subrs")]
internal readonly record struct CffFontDict(byte[] Kept, byte[] PrivateKept, CffIndex Subrs);
