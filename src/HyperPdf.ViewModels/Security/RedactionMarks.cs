// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Security;

/// <summary>How many areas a document has marked for redaction, and on how many pages.</summary>
/// <param name="Areas">The marked areas.</param>
/// <param name="Pages">The pages with at least one mark.</param>
internal readonly record struct RedactionMarks(int Areas, int Pages);
