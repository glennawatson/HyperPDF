// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>One box of an ISO base media file: its type and where its content lies.</summary>
/// <param name="Type">The four character type, as a number.</param>
/// <param name="Start">The index of the first byte of the content.</param>
/// <param name="End">The index after the last byte of the content.</param>
internal readonly record struct Mp4Box(uint Type, int Start, int End);
