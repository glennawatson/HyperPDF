// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H265;

/// <summary>A node of the transform tree.</summary>
/// <param name="X">The column of the node's top-left luma sample.</param>
/// <param name="Y">The row of the node's top-left luma sample.</param>
/// <param name="XBase">The column of the parent node's top-left luma sample.</param>
/// <param name="YBase">The row of the parent node's top-left luma sample.</param>
/// <param name="Log2Size">The base 2 logarithm of the node's side.</param>
/// <param name="Depth">The depth below the coding unit.</param>
/// <param name="BlkIdx">The node's index among the four children of its parent.</param>
internal readonly record struct H265TreeNode(int X, int Y, int XBase, int YBase, int Log2Size, int Depth, int BlkIdx);
