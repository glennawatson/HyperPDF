// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A hint stream's location in a linearized PDF.</summary>
/// <param name="Offset">The offset from the start of the file.</param>
/// <param name="Length">The encoded stream object's length in bytes.</param>
[DebuggerDisplay("PdfHintRange: {Offset}+{Length}")]
public readonly record struct PdfHintRange(long Offset, long Length);
