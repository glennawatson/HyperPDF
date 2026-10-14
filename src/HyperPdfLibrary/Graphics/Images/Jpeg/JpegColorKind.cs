// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The colour model of the components an encoder writes.</summary>
internal enum JpegColorKind
{
    /// <summary>One gray component.</summary>
    Gray = 0,

    /// <summary>Luma and two chroma components (YCbCr), with a JFIF header.</summary>
    YCbCr = 1,

    /// <summary>Four ink components stored inverted, with an Adobe marker whose transform is 0.</summary>
    Cmyk = 2,

    /// <summary>Luma, two chroma components and inverted black (YCCK), with an Adobe marker whose transform is 2.</summary>
    Ycck = 3,
}
