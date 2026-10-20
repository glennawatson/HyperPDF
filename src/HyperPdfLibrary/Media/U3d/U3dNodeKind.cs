// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The kinds of scene node in a U3D file.</summary>
public enum U3dNodeKind
{
    /// <summary>A group of other nodes.</summary>
    Group = 0,

    /// <summary>A node that shows a model.</summary>
    Model = 1,

    /// <summary>A node that holds a light.</summary>
    Light = 2,

    /// <summary>A node that holds a view, which is a camera.</summary>
    View = 3,
}
