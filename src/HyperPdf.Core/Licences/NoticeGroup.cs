// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Licences;

/// <summary>The components that share one licence.</summary>
/// <param name="Licence">The SPDX licence expression.</param>
/// <param name="Entries">The components, in file order.</param>
[DebuggerDisplay("NoticeGroup: {Licence}: {Entries.Count} components")]
public sealed record NoticeGroup(string Licence, IReadOnlyList<NoticeEntry> Entries);
