// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Tmds.DBus.Protocol;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>Answers one AT-SPI method: reads its arguments and sends exactly one reply.</summary>
/// <param name="call">The request and the helpers that reply to it.</param>
/// <param name="reader">The arguments, read in order.</param>
internal delegate void AtSpiMethod(in AtSpiCall call, ref Reader reader);

/// <summary>Writes the value of one AT-SPI property as the content of a variant.</summary>
/// <param name="call">The request.</param>
/// <param name="writer">The writer, positioned where a variant starts.</param>
internal delegate void AtSpiProperty(in AtSpiCall call, ref MessageWriter writer);
