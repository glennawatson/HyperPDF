// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>One step of an embedded go-to action's path to its target document (a target dictionary, ISO 32000-2 Table 205).</summary>
/// <param name="Relationship">The <c>/R</c> name: P when the step goes to the parent document, C when it goes to a child; empty when missing.</param>
/// <param name="FileName">For a child in the <c>/EmbeddedFiles</c> name tree, its name (<c>/N</c>); otherwise null.</param>
/// <param name="AttachmentPage">For a child held by a file attachment annotation, the zero based page of the annotation (<c>/P</c> as a number); otherwise null.</param>
/// <param name="AttachmentPageDestination">For a child held by a file attachment annotation, the named destination that gives its page (<c>/P</c> as a string); otherwise null.</param>
/// <param name="AttachmentIndex">For a child held by a file attachment annotation, the annotation's zero based index in the page's <c>/Annots</c> (<c>/A</c> as a number); otherwise null.</param>
/// <param name="AttachmentName">For a child held by a file attachment annotation, the annotation's <c>/NM</c> (<c>/A</c> as a string); otherwise null.</param>
[DebuggerDisplay("PdfEmbeddedTarget: {Relationship} {FileName}")]
public sealed record PdfEmbeddedTarget(string Relationship, string? FileName, int? AttachmentPage, string? AttachmentPageDestination, int? AttachmentIndex, string? AttachmentName);
