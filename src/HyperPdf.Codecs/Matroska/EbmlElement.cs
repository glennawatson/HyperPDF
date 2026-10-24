// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>One EBML element held in memory: its ID and where its content lies.</summary>
/// <param name="Id">The ID.</param>
/// <param name="Start">The index of the first byte of the content.</param>
/// <param name="End">The index after the last byte of the content.</param>
internal readonly record struct EbmlElement(uint Id, int Start, int End);
