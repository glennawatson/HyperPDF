// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>A configuration of a rich media annotation: the instances that play together.</summary>
/// <param name="Subtype">The content type: 3D, Flash, Sound or Video; null when missing.</param>
/// <param name="Name">The configuration name, or null.</param>
/// <param name="Instances">The instances.</param>
[DebuggerDisplay("PdfRichMediaConfiguration: {Subtype} {Name}")]
public sealed record PdfRichMediaConfiguration(string? Subtype, string? Name, PdfRichMediaInstance[] Instances);
