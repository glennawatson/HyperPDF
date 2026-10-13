// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>
/// How a page operation changed one printed page of a preseparated document, because not every one of its separation
/// pages was carried once. The links left in the output always meet ISO 32000 §14.11.4.
/// </summary>
/// <param name="Group">The zero based group in the source document, or -1 for pages whose separation dictionary was damaged and could not be kept.</param>
/// <param name="GroupPages">The separation pages the group has in the source; 0 for damaged dictionaries.</param>
/// <param name="KeptPages">The output pages still linked as separations of this printed page.</param>
/// <param name="UnlinkedPages">The output pages whose separation dictionary was removed: repeated copies, pages without a colourant, or damaged dictionaries.</param>
[DebuggerDisplay("PdfSeparationChange: group {Group} kept {KeptPages} of {GroupPages}")]
public readonly record struct PdfSeparationChange(int Group, int GroupPages, int KeptPages, int UnlinkedPages);
