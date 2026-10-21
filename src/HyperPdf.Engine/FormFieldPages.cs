// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;

namespace HyperPdf.Engine;

/// <summary>The form fields read from each page, valid only while the document's edit version equals <paramref name="Version"/>.</summary>
/// <param name="Version">The edit version the fields were read at.</param>
/// <param name="Pages">The fields of each page, or <see langword="null"/> for a page not read yet.</param>
internal sealed record FormFieldPages(long Version, FormField[]?[] Pages);
