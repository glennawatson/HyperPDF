// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A place in the document that refers to a field by name and that the library could not update.</summary>
/// <param name="Location">Where the reference is, in plain words, for example <c>field Total (calculate action)</c>.</param>
/// <param name="Description">What was left as it was and why.</param>
[DebuggerDisplay("PdfFormReference: {Location}")]
public sealed record PdfFormReference(string Location, string Description);
