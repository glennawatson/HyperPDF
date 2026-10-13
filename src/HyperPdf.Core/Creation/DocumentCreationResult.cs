// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Creation;

/// <summary>What a created document contains and what it could not promise.</summary>
/// <param name="PageCount">The number of pages written.</param>
/// <param name="IsTagged">Whether the document has a structure tree.</param>
/// <param name="ClaimsPdfUa">Whether the metadata claims PDF/UA.</param>
/// <param name="Notes">Plain-language notes for the user, such as why the PDF/UA claim was left out.</param>
[DebuggerDisplay("DocumentCreationResult: {PageCount} pages, tagged {IsTagged}, PDF/UA {ClaimsPdfUa}")]
public sealed record DocumentCreationResult(int PageCount, bool IsTagged, bool ClaimsPdfUa, IReadOnlyList<string> Notes);
