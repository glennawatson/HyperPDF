// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.H264;

/// <summary>The two reference lists of a B slice.</summary>
/// <param name="L0">Reference list 0.</param>
/// <param name="L1">Reference list 1.</param>
internal readonly record struct H264RefLists(H264RefPicture[] L0, H264RefPicture[] L1);
