// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>The kinds of test page <see cref="Jbig2TestPages"/> builds.</summary>
internal enum Jbig2TestPageKind
{
    /// <summary>Random black pixels.</summary>
    Noise = 0,

    /// <summary>Lines of glyphs.</summary>
    Text = 1,

    /// <summary>No black pixels.</summary>
    Blank = 2,
}
