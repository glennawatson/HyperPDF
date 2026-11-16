// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>What <see cref="PdfDocumentSanitizer"/> took out of a document.</summary>
/// <param name="Scripts">The number of scripts and actions that run them removed.</param>
/// <param name="Attachments">The number of embedded files and file attachment notes removed.</param>
/// <param name="Comments">The number of notes and marks removed.</param>
/// <param name="MetadataCleared">Whether the descriptive metadata was cleared.</param>
[DebuggerDisplay("PdfSanitizeReport: {Scripts} scripts, {Attachments} attachments, {Comments} comments, metadata {MetadataCleared}")]
public sealed record PdfSanitizeReport(int Scripts, int Attachments, int Comments, bool MetadataCleared);
