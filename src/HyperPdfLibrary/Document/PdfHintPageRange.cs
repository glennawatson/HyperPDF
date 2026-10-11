// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>One page's byte range and object group from a linearization page-offset hint table.</summary>
/// <param name="Offset">The page group's file offset.</param>
/// <param name="Length">The page group's length in bytes.</param>
/// <param name="FirstObjectNumber">The first object number in the group.</param>
/// <param name="ObjectCount">The number of objects in the group.</param>
[DebuggerDisplay("PdfHintPageRange: {Offset}+{Length}, {ObjectCount} objects")]
public readonly record struct PdfHintPageRange(long Offset, long Length, int FirstObjectNumber, int ObjectCount);
