// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A widget ready to copy.</summary>
/// <param name="Dictionary">The dictionary to copy in the widget's place.</param>
/// <param name="KeepParent">Whether its <c>/Parent</c> names a field that was copied.</param>
[DebuggerDisplay("PreparedWidget: KeepParent={KeepParent}")]
internal readonly record struct PreparedWidget(PdfDictionary Dictionary, bool KeepParent);
