// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>The document part root (<c>/DPartRoot</c>).</summary>
/// <param name="RecordLevel">The level of the hierarchy at which metadata is recorded (<c>/RecordLevel</c>).</param>
/// <param name="NodeNames">The names of the levels, outermost first (<c>/NodeNameList</c>).</param>
/// <param name="Root">The top node, or null.</param>
[DebuggerDisplay("PdfDocumentPartRoot: level {RecordLevel}")]
public sealed record PdfDocumentPartRoot(int RecordLevel, string[] NodeNames, PdfDocumentPartNode? Root);
