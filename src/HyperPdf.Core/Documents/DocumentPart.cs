// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A part of a document that groups a run of pages, such as one record of a mailing (ISO 32000-2 §14.12).</summary>
/// <param name="Name">The part's name, from its metadata, or a plain label such as "Part 2" when it has none.</param>
/// <param name="Details">The rest of its metadata as "key: value" pairs, or an empty string.</param>
/// <param name="StartPage">The zero based first page of the part, or <see langword="null"/> when it groups other parts.</param>
/// <param name="EndPage">The zero based last page of the part, or <see langword="null"/> when it groups other parts.</param>
/// <param name="Children">The parts inside it, in order.</param>
[DebuggerDisplay("DocumentPart: {Name} {StartPage}-{EndPage}")]
public sealed record DocumentPart(string Name, string Details, int? StartPage, int? EndPage, IReadOnlyList<DocumentPart> Children);
