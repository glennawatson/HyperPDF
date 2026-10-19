// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>A web capture content set: the page objects that came from one fetch.</summary>
/// <param name="Subtype">SPS (page set) or SIS (image set).</param>
/// <param name="Id">The content set's identifier.</param>
/// <param name="ContentType">The MIME type of the content, or null.</param>
/// <param name="Sources">Where the content came from.</param>
/// <param name="ObjectCount">The number of objects in the set.</param>
[DebuggerDisplay("PdfWebCaptureContentSet: {Subtype} {ObjectCount} objects")]
public sealed record PdfWebCaptureContentSet(string Subtype, byte[] Id, string? ContentType, PdfWebCaptureSource[] Sources, int ObjectCount);
