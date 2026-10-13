// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>What removing fields from a document's form took out.</summary>
/// <param name="FieldsRemoved">The number of fields removed from the field tree.</param>
/// <param name="FormRemoved">Whether the /AcroForm was removed because no field was left.</param>
/// <param name="XfaRemoved">Whether the form had XFA data, which is removed with the form or because the fields changed.</param>
[DebuggerDisplay("PdfFieldRemovalResult: {FieldsRemoved} fields, form removed {FormRemoved}")]
internal readonly record struct PdfFieldRemovalResult(int FieldsRemoved, bool FormRemoved, bool XfaRemoved);
