// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Whether a list's items start with a bullet or a number.</summary>
public enum PdfListKind
{
    /// <summary>Each item starts with a bullet.</summary>
    Bullet = 0,

    /// <summary>Each item starts with its number.</summary>
    Numbered = 1,
}
