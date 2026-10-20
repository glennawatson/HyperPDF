// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Tests;

/// <summary>A page's open or close actions that the viewer ran.</summary>
/// <param name="Page">The zero based page index.</param>
/// <param name="Opened"><see langword="true"/> for the open actions, <see langword="false"/> for the close actions.</param>
internal readonly record struct RecordedPageAction(int Page, bool Opened);
