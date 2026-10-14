// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>The adaptive contexts a text region is coded with.</summary>
/// <param name="Contexts">The contexts of the integer coders, followed by the symbol ID contexts.</param>
/// <param name="RefinementContexts">The refinement contexts.</param>
/// <param name="CodeLength">The bits in a symbol ID.</param>
/// <param name="Refine">Whether instances carry a refinement flag.</param>
internal readonly record struct Jbig2TextCoding(byte[] Contexts, byte[] RefinementContexts, int CodeLength, bool Refine);
