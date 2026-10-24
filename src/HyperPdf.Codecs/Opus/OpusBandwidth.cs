// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The audio bandwidth of an Opus frame.</summary>
internal enum OpusBandwidth
{
    /// <summary>Up to 4 kHz.</summary>
    Narrow = 0,

    /// <summary>Up to 6 kHz.</summary>
    Medium = 1,

    /// <summary>Up to 8 kHz.</summary>
    Wide = 2,

    /// <summary>Up to 12 kHz.</summary>
    SuperWide = 3,

    /// <summary>Up to 20 kHz.</summary>
    Full = 4,
}
