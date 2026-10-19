// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Media;

/// <summary>A screen annotation: an area where renditions play.</summary>
/// <param name="Title">The annotation title (<c>/T</c>), or null.</param>
/// <param name="Action">The action run when the annotation is activated (<c>/A</c>), or null.</param>
/// <param name="Triggers">The additional actions (<c>/AA</c>).</param>
[DebuggerDisplay("PdfScreenAnnotation: {Title}")]
public sealed record PdfScreenAnnotation(string? Title, PdfActionNode? Action, PdfTrigger[] Triggers);
