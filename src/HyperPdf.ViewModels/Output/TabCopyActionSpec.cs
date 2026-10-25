// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Output;

/// <summary>What one copy command of a tab needs: its menu texts, when it is available and how its window is made.</summary>
/// <typeparam name="TRequest">The view model of the window the command opens.</typeparam>
/// <param name="AvailableText">The menu text when the open document's engine supports the command.</param>
/// <param name="UnavailableText">The menu text when it does not, which says why.</param>
/// <param name="IsAvailable">Whether the open document's engine supports the command.</param>
/// <param name="CreateRequest">Makes the window's view model for a tab, or <see langword="null"/> when the document is gone.</param>
[DebuggerDisplay("TabCopyActionSpec: {AvailableText}")]
internal sealed record TabCopyActionSpec<TRequest>(
    string AvailableText,
    string UnavailableText,
    Func<IDocument, bool> IsAvailable,
    Func<DocumentTabViewModel, TRequest?> CreateRequest)

    where TRequest : class, ICopyWindowRequest;
