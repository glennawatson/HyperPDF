// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.ViewModels;

/// <summary>A completed page operation and the selection to restore after refreshing the page structure.</summary>
/// <param name="Changed">Whether the operation changed the open document.</param>
/// <param name="SelectionAfterRefresh">The selection after refreshing, or null to retain valid selection.</param>
internal readonly record struct PageManagementOperationResult(bool Changed, Func<IEnumerable<int>>? SelectionAfterRefresh);
