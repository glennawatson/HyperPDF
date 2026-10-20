// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Rendering;

/// <summary>The colour an annotation icon's body is filled with.</summary>
/// <param name="Red">The red channel from 0 to 1.</param>
/// <param name="Green">The green channel from 0 to 1.</param>
/// <param name="Blue">The blue channel from 0 to 1.</param>
internal readonly record struct IconFill(float Red, float Green, float Blue);
