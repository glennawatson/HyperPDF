// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>What one step of a recorded picture does.</summary>
internal enum PictureOpKind
{
    /// <summary>Remembers the clip.</summary>
    Save = 0,

    /// <summary>Goes back to the clip last remembered.</summary>
    Restore = 1,

    /// <summary>Narrows the clip to a path.</summary>
    Clip = 2,

    /// <summary>Fills a path.</summary>
    Fill = 3,

    /// <summary>Strokes a path.</summary>
    Stroke = 4,

    /// <summary>Draws an image into the unit square of its matrix.</summary>
    Image = 5,

    /// <summary>Paints everything inside the clip, as the shading operator does.</summary>
    Paint = 6,

    /// <summary>Draws another picture under the step's matrix.</summary>
    Picture = 7,

    /// <summary>Draws the triangles of a mesh shading.</summary>
    Mesh = 8,

    /// <summary>Starts the off-screen layer of a transparency group.</summary>
    BeginLayer = 9,

    /// <summary>Puts the layer started last back onto what is under it.</summary>
    EndLayer = 10,

    /// <summary>Fades the layer started last by a soft mask: a picture whose brightness or alpha says how much shows.</summary>
    Mask = 11,
}
