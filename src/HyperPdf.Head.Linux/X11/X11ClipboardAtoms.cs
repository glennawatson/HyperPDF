// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.X11;

/// <summary>The atoms the X11 clipboard uses.</summary>
/// <param name="Clipboard">The selection that copy and paste use.</param>
/// <param name="Targets">The type an application asks for to learn what it can have.</param>
/// <param name="Utf8String">The type of UTF-8 text.</param>
/// <param name="Paste">The property on the window that another application writes pasted text into.</param>
internal readonly record struct X11ClipboardAtoms(long Clipboard, long Targets, long Utf8String, long Paste);
