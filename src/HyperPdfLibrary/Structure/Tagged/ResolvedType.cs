// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A structure type after the role maps.</summary>
/// <param name="Type">The standard type, or <see cref="PdfStructureType.Unknown"/>.</param>
/// <param name="MappedType">The last type name the maps reached.</param>
/// <param name="IsMathMl">Whether the maps ended in the MathML namespace, whose types PDF 2.0 allows without further mapping.</param>
[DebuggerDisplay("ResolvedType: {MappedType} as {Type}")]
internal readonly record struct ResolvedType(PdfStructureType Type, string MappedType, bool IsMathMl);
