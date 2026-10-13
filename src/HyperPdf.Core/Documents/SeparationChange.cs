// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A printed page of a preseparated document whose separation pages a page edit could not keep together.</summary>
/// <param name="PrintedPage">The printed page's one-based number in the source, or 0 for pages whose separation details were damaged and were removed.</param>
/// <param name="GroupPages">The separation pages the printed page has in the source; 0 for damaged details.</param>
/// <param name="KeptPages">The pages still linked as separations of this printed page.</param>
/// <param name="UnlinkedPages">The pages that are no longer marked as separations, such as repeated copies.</param>
[DebuggerDisplay("SeparationChange: printed page {PrintedPage} kept {KeptPages} of {GroupPages}")]
public readonly record struct SeparationChange(int PrintedPage, int GroupPages, int KeptPages, int UnlinkedPages);
