// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.PdfUa;

/// <summary>Owns reusable collections without retaining a scanned document or tree.</summary>
/// <param name="PagesByObject">The PagesByObject working collection.</param>
/// <param name="Drawn">The Drawn working collection.</param>
/// <param name="Missing">The Missing working collection.</param>
/// <param name="Chain">The Chain working collection.</param>
/// <param name="NewKids">The NewKids working collection.</param>
/// <param name="Deleted">The Deleted working collection.</param>
/// <param name="Marks">The Marks working collection.</param>
/// <param name="FreeSets">The FreeSets working collection.</param>
/// <param name="Order">The Order working collection.</param>
internal sealed record PdfUaStructureReferenceBuffers(
    Dictionary<int, int> PagesByObject,
    Dictionary<int, HashSet<int>> Drawn,
    Dictionary<int, HashSet<int>> Missing,
    HashSet<PdfStructureElement> Chain,
    Dictionary<int, PdfArray> NewKids,
    HashSet<int> Deleted,
    List<ScannedMark> Marks,
    List<HashSet<int>> FreeSets,
    List<int> Order);
