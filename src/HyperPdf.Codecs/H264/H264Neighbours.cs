// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>Which of the neighbouring blocks can be used to predict a block.</summary>
/// <param name="Left">Whether the block to the left can be used.</param>
/// <param name="Top">Whether the block above can be used.</param>
/// <param name="TopRight">Whether the block above and to the right can be used.</param>
/// <param name="TopLeft">Whether the block above and to the left can be used.</param>
internal readonly record struct H264Neighbours(bool Left, bool Top, bool TopRight, bool TopLeft);
