// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Archiving;

namespace HyperPdf.Core.Flattening;

/// <summary>What saving a flattened copy did: what became page content, what was left alone, or why it was refused.</summary>
/// <param name="PagesChanged">The number of pages whose content changed.</param>
/// <param name="FormFieldsFlattened">The number of form field widgets drawn into page content.</param>
/// <param name="AnnotationsFlattened">The number of comments, highlights and other annotations drawn into page content.</param>
/// <param name="FormRemoved">Whether the form was removed because every field was flattened.</param>
/// <param name="Notes">What was left as it was or changed in a way worth telling the user, in plain words.</param>
/// <param name="Problems">Why the copy was refused; empty when it was written.</param>
[DebuggerDisplay("FlattenCopyResult: {FormFieldsFlattened} fields, {AnnotationsFlattened} annotations, flattened {Flattened}")]
public sealed record FlattenCopyResult(
    int PagesChanged,
    int FormFieldsFlattened,
    int AnnotationsFlattened,
    bool FormRemoved,
    IReadOnlyList<ArchiveNote> Notes,
    IReadOnlyList<ArchiveNote> Problems)
{
    /// <summary>Gets a value indicating whether the copy was written.</summary>
    public bool Flattened => Problems.Count == 0;
}
