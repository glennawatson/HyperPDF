// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>The document details a person can change.</summary>
/// <param name="Title">The title.</param>
/// <param name="Author">Who wrote it.</param>
/// <param name="Subject">What it is about.</param>
/// <param name="Keywords">Words to find it by.</param>
[DebuggerDisplay("DocumentInfoEdit: {Title}")]
public sealed record DocumentInfoEdit(string Title, string Author, string Subject, string Keywords);
