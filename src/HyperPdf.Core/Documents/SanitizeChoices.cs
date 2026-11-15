// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>What to take out of a document before it is shared.</summary>
/// <param name="Metadata">Clear the title, author, subject, keywords, creator and producer, and the application data stored beside the pages.</param>
/// <param name="Scripts">Remove JavaScript and the actions that run it.</param>
/// <param name="Attachments">Remove embedded files and file attachment notes.</param>
/// <param name="Comments">Remove every note and mark except links and form fields.</param>
[DebuggerDisplay("SanitizeChoices: metadata {Metadata}, scripts {Scripts}, attachments {Attachments}, comments {Comments}")]
public sealed record SanitizeChoices(bool Metadata, bool Scripts, bool Attachments, bool Comments);
