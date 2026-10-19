// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>A movie annotation.</summary>
/// <param name="Title">The annotation title (<c>/T</c>), used by movie actions to find it, or null.</param>
/// <param name="File">The movie file as written, or null.</param>
/// <param name="Aspect">The width and height of the movie frame, or an empty array.</param>
/// <param name="Rotation">The movie's rotation in degrees.</param>
/// <param name="HasPoster">Whether the movie has a poster image.</param>
/// <param name="Activation">How the movie plays.</param>
[DebuggerDisplay("PdfMovieAnnotation: {Title} {File}")]
public sealed record PdfMovieAnnotation(string? Title, string? File, int[] Aspect, int Rotation, bool HasPoster, PdfMovieActivation Activation);
