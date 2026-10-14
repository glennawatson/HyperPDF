// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>One page's share of a set of components: the combined components of one or more pages, the page's range in them, and its own residual.</summary>
/// <param name="Combined">The components the symbols were matched over.</param>
/// <param name="First">The page's first component in <paramref name="Combined"/>.</param>
/// <param name="End">The component after the page's last.</param>
/// <param name="Own">The page's own components, which hold its residual bitmap.</param>
internal readonly record struct Jbig2PageSlice(Jbig2Components Combined, int First, int End, Jbig2Components Own);
