// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>What a push button shows: where the caption goes, the icon and the encoded caption.</summary>
/// <param name="Position">The text position (<c>/MK /TP</c>).</param>
/// <param name="Icon">The icon form, or <see langword="null"/> when the button shows none.</param>
/// <param name="Codes">The encoded caption; empty when the button shows none.</param>
[DebuggerDisplay("FormPushParts: position {Position}")]
internal readonly record struct FormPushParts(int Position, PdfStream? Icon, FormCodes Codes);
