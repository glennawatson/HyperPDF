// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>An image that a PDF/A fixture draws: a JPEG 2000 file, or raw 8-bit samples.</summary>
/// <param name="Data">The JPX file, or the raw samples.</param>
/// <param name="Width">The image width.</param>
/// <param name="Height">The image height.</param>
/// <param name="ColorSpaceName">The device colour space name for the image dictionary, or <see langword="null"/> to leave it to the JPX file.</param>
/// <param name="IsJpx">Whether <paramref name="Data"/> is a JPX file; otherwise it is raw samples with 8 bits per component.</param>
internal sealed record PdfAFixtureImage(byte[] Data, int Width, int Height, string? ColorSpaceName, bool IsJpx);
