// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Media;

namespace HyperPdf.Engine;

/// <summary>The media of a document, with the snapshot of multimedia annotations they were found in; a new snapshot after an edit makes them stale.</summary>
/// <param name="Source">The snapshot of multimedia annotations the library gave.</param>
/// <param name="Entries">The media found in the snapshot, with their streams.</param>
/// <param name="Media">What the viewer shows about each, in the same order.</param>
[DebuggerDisplay("MediaCache: {Entries.Length} entries")]
internal sealed record MediaCache(PdfMultimediaAnnotation[] Source, MediaEntry[] Entries, DocumentMedia[] Media);
