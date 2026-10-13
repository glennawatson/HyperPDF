// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>An appearance stream and the matrix that fits it to its annotation's rectangle.</summary>
/// <param name="Form">A reference to the appearance stream.</param>
/// <param name="Mapping">Maps the appearance's space to the page's user space (ISO 32000-2 12.5.5, Algorithm 8.1).</param>
[DebuggerDisplay("PdfFlattenForm: {Form}")]
internal readonly record struct PdfFlattenForm(PdfValue Form, Matrix3x2 Mapping);
