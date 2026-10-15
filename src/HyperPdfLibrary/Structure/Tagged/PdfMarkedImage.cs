// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One image a page draws, with the marked content it is drawn in.</summary>
/// <param name="Bounds">The image's box in viewer space.</param>
/// <param name="Mcid">The marked content id of the innermost marked content with one; -1 when none.</param>
/// <param name="IsArtifact">Whether the image is inside <c>/Artifact</c> marked content.</param>
[DebuggerDisplay("PdfMarkedImage: mcid {Mcid} artifact={IsArtifact}")]
public readonly record struct PdfMarkedImage(PdfViewerRect Bounds, int Mcid, bool IsArtifact);
