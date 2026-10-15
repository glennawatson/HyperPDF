// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Rendering;

/// <summary>A named set of <see cref="RenderSharpness"/> choices.</summary>
public enum SharpnessPreset
{
    /// <summary>Text, lines and pictures are drawn with soft edges.</summary>
    Smooth = 0,

    /// <summary>Text is drawn with hard edges; lines and pictures keep soft edges.</summary>
    Sharp = 1,

    /// <summary>Text, lines and pictures are drawn with hard edges, and lines are fitted to whole pixels.</summary>
    Crisp = 2,

    /// <summary>The individual choices match none of the named sets.</summary>
    Custom = 3,
}
