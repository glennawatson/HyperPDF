// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Search;

/// <summary>A file opened for a folder search, or the result to give when it could not be opened.</summary>
/// <param name="Document">The open file, or <see langword="null"/> when it could not be opened.</param>
/// <param name="Failure">The result to give when the file could not be opened.</param>
internal sealed record OpenedFolderFile(IDocument? Document, FolderSearchFile? Failure);
