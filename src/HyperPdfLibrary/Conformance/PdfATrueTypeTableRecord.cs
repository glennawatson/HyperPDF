// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>A table directory entry of a TrueType program.</summary>
/// <param name="Offset">The table offset.</param>
/// <param name="Length">The table length.</param>
internal readonly record struct PdfATrueTypeTableRecord(long Offset, long Length);
