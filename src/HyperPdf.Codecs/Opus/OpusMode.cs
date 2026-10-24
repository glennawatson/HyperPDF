// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The coding an Opus frame uses.</summary>
internal enum OpusMode
{
    /// <summary>Speech coding by the SILK layer alone.</summary>
    Silk = 0,

    /// <summary>SILK for the low band and CELT for the high band.</summary>
    Hybrid = 1,

    /// <summary>Transform coding by the CELT layer alone.</summary>
    Celt = 2,
}
