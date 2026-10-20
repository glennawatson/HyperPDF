// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The kinds of annotation that carry sound or video the viewer does not play itself.</summary>
public enum DocumentMediaKind
{
    /// <summary>A sound annotation.</summary>
    Sound = 0,

    /// <summary>A screen annotation, whose rendition holds a media clip.</summary>
    Screen = 1,

    /// <summary>A rich media annotation, whose assets hold audio or video.</summary>
    RichMedia = 2,
}
