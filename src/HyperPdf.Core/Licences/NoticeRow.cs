// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Licences;

/// <summary>Where the parts of one component sit in the notices text.</summary>
/// <param name="Licence">The licence heading the component is under; components of one heading share its start.</param>
/// <param name="Name">The component's name.</param>
/// <param name="Version">The version field.</param>
/// <param name="Origin">The origin field.</param>
/// <param name="Copyright">The copyright field.</param>
/// <param name="Link">The link field.</param>
/// <param name="Text">The lines of the licence text, with the line breaks and any trailing blank lines.</param>
internal readonly record struct NoticeRow(TextRange Licence, TextRange Name, TextRange Version, TextRange Origin, TextRange Copyright, TextRange Link, TextRange Text);
