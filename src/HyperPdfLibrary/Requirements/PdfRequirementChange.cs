// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>A requirement a save removed because an edit left the feature it names out of the file. Show <see cref="Message"/> to the user.</summary>
/// <param name="Type">The requirement type that was removed, such as <c>Collection</c>.</param>
/// <param name="Message">What was removed and why, in plain words.</param>
[DebuggerDisplay("PdfRequirementChange: {Type}")]
public sealed record PdfRequirementChange(string Type, string Message);
