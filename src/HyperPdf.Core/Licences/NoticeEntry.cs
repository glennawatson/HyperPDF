// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Licences;

/// <summary>One component in the third-party notices.</summary>
/// <param name="Name">The component's name.</param>
/// <param name="Licence">The SPDX licence expression, such as <c>MIT</c>.</param>
/// <param name="Version">The version, or empty.</param>
/// <param name="Origin">Where it comes from, such as "NuGet package" or "Downloaded when you turn this on".</param>
/// <param name="Copyright">The copyright notice, or empty.</param>
/// <param name="Link">A link to the project, or empty.</param>
/// <param name="Text">The full licence text.</param>
[DebuggerDisplay("NoticeEntry: {Name} {Version} ({Licence})")]
public sealed record NoticeEntry(string Name, string Licence, string Version, string Origin, string Copyright, string Link, string Text);
