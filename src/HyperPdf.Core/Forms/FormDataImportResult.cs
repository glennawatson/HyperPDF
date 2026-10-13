// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Forms;

/// <summary>What importing an FDF or XFDF file did to a document.</summary>
/// <param name="Succeeded">Whether the file was read and applied. When <see langword="false"/> the document is unchanged.</param>
/// <param name="FieldsApplied">The fields whose value was set.</param>
/// <param name="FieldsSkipped">The fields left alone: unknown names, read-only fields and values that do not fit.</param>
/// <param name="AnnotationsApplied">The annotations added or replaced.</param>
/// <param name="AnnotationsSkipped">The annotations left out.</param>
[DebuggerDisplay("FormDataImportResult: {Succeeded}, {FieldsApplied} fields, {AnnotationsApplied} annotations")]
public readonly record struct FormDataImportResult(bool Succeeded, int FieldsApplied, int FieldsSkipped, int AnnotationsApplied, int AnnotationsSkipped)
{
    /// <summary>Gets the result of a file that could not be read or applied.</summary>
    public static FormDataImportResult Failed { get; } = new(false, 0, 0, 0, 0);
}
