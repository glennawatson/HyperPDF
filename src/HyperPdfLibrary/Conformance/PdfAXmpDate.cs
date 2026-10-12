// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>An XMP date as read from its ISO 8601 text; unread parts are zero.</summary>
/// <param name="Year">The year.</param>
/// <param name="Month">The month, 1 to 12, or 0 when only a year was given.</param>
/// <param name="Day">The day, 1 to 31, or 0 when no day was given.</param>
/// <param name="Hour">The hour.</param>
/// <param name="Minute">The minute.</param>
/// <param name="Second">The second.</param>
/// <param name="Nanosecond">The fraction of a second in nanoseconds.</param>
/// <param name="Offset">The time zone offset, or <see langword="null"/> for local time.</param>
internal readonly record struct PdfAXmpDate(int Year, int Month, int Day, int Hour, int Minute, int Second, int Nanosecond, TimeSpan? Offset);
