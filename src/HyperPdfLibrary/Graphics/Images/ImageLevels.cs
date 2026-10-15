// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>The halvings an image is decoded with.</summary>
/// <param name="Codec">The finest resolution levels the codec omits.</param>
/// <param name="Box">The halvings averaged into the pixels while rows are converted, for images whose codec cannot omit levels.</param>
internal readonly record struct ImageLevels(int Codec, int Box);
