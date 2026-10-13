// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A planned change to a field.</summary>
/// <param name="Spec">The field after the change.</param>
/// <param name="Parts">The parts to rewrite.</param>
/// <param name="Flags">The field flags after the change, with every bit the field already had.</param>
[DebuggerDisplay("FormFieldChange: {Parts}")]
internal sealed record FormFieldChange(FormFieldSpec Spec, FormFieldParts Parts, PdfFieldFlags Flags);
