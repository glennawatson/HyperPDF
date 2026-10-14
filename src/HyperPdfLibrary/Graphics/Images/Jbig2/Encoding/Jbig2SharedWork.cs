// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>What a shared encode has found in its images.</summary>
/// <param name="Inks">Each image's ink rows, in rented arrays at least as long as the rows.</param>
/// <param name="Parts">Each image's components, or null for an image with no symbols to share.</param>
/// <param name="Combined">The symbol components of all the images that have some, in image order.</param>
internal readonly record struct Jbig2SharedWork(byte[][] Inks, Jbig2Components?[] Parts, Jbig2Components Combined);
