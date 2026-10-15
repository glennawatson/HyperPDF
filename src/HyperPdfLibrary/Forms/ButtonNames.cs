// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>A name for a button being considered and the same name for the button that was switched, to tell whether they match.</summary>
/// <param name="Current">The considered button's name.</param>
/// <param name="Switched">The switched button's name.</param>
[DebuggerDisplay("ButtonNames: {Current} against {Switched}")]
internal readonly record struct ButtonNames(PdfName Current, PdfName Switched);
