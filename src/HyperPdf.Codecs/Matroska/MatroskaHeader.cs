// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>The header of an element in a file: its ID and where it and its content lie.</summary>
/// <param name="Id">The ID.</param>
/// <param name="Offset">The file offset of the header.</param>
/// <param name="PayloadStart">The file offset of the first byte of the content.</param>
/// <param name="End">The file offset after the content, or <see cref="Ebml.UnknownSize"/> when the size is not known.</param>
internal readonly record struct MatroskaHeader(uint Id, long Offset, long PayloadStart, long End);
