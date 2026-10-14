// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>The symbol coding a shared encode uses.</summary>
/// <param name="Mode">The mode.</param>
/// <param name="IsLossless">Whether the mode reproduces the images exactly.</param>
internal readonly record struct Jbig2SharedCoding(Jbig2EncodeMode Mode, bool IsLossless);
