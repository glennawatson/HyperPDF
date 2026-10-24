// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>A top level box of a file: where it starts and how long it is.</summary>
/// <param name="Type">The four character type, as a number.</param>
/// <param name="Offset">The file offset of the box's header.</param>
/// <param name="Size">The size of the box including its header.</param>
/// <param name="IsComplete">Whether the whole box is in the file.</param>
internal readonly record struct Mp4TopBox(uint Type, long Offset, long Size, bool IsComplete);
