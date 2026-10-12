// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Annotations;

/// <summary>How a signature or initials mark was made.</summary>
public enum SignatureMarkStyle
{
    /// <summary>Typed text.</summary>
    Typed = 0,

    /// <summary>Strokes drawn with a mouse, pen or finger.</summary>
    Drawn = 1,

    /// <summary>A picture of a signature, for example a scan or photo.</summary>
    Image = 2,
}
