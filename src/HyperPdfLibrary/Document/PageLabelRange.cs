// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>A run of pages sharing a label style.</summary>
/// <param name="Start">The first page of the run.</param>
/// <param name="Label">The label dictionary.</param>
internal readonly record struct PageLabelRange(int Start, PdfDictionary Label);
