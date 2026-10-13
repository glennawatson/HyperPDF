// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>What renaming a field did, or why it was refused.</summary>
/// <param name="Renamed">Whether the field was renamed.</param>
/// <param name="OldName">The field's fully qualified name before.</param>
/// <param name="NewName">The field's fully qualified name now; the old name when it was refused.</param>
/// <param name="Reason">A plain reason the rename was refused; <see langword="null"/> when it was done.</param>
/// <param name="UpdatedReferences">The number of names in actions (submit, reset, import and hide) that were updated to follow the rename.</param>
/// <param name="Unresolved">The places that refer to the old name and that were left as they were, such as scripts.</param>
[DebuggerDisplay("PdfFieldRename: {OldName} to {NewName}, renamed {Renamed}")]
public sealed record PdfFieldRename(bool Renamed, string OldName, string NewName, string? Reason, int UpdatedReferences, PdfFormReference[] Unresolved);
