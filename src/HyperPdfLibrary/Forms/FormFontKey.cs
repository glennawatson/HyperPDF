// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A font dictionary bound to its name in appearance resources.</summary>
/// <param name="Dictionary">The font dictionary.</param>
/// <param name="Name">Its resource name.</param>
[DebuggerDisplay("FormFontKey: {Name}")]
internal readonly record struct FormFontKey(PdfDictionary Dictionary, PdfName Name);
