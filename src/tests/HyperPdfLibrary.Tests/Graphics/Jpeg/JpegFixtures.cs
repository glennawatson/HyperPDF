// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpeg;

/// <summary>
/// Small JPEGs written for these tests with ImageMagick (libjpeg): a progressive RGB gradient with 2x2 chroma sampling,
/// a 16x16 solid cyan YCCK file with an Adobe marker (transform 2), and a 32x24 progressive YCCK gradient.
/// CMYK data in JPEG files is stored inverted, so cyan is stored as 0, 255, 255, 255.
/// </summary>
internal static class JpegFixtures
{
    /// <summary>The width of <see cref="ProgressiveRgb"/>.</summary>
    internal const int ProgressiveRgbWidth = 24;

    /// <summary>The height of <see cref="ProgressiveRgb"/>.</summary>
    internal const int ProgressiveRgbHeight = 16;

    /// <summary>The side of <see cref="CmykCyan"/>.</summary>
    internal const int CmykCyanSide = 16;

    /// <summary>The width of <see cref="CmykProgressive"/>.</summary>
    internal const int CmykProgressiveWidth = 32;

    /// <summary>The height of <see cref="CmykProgressive"/>.</summary>
    internal const int CmykProgressiveHeight = 24;

    /// <summary>Gets the progressive RGB gradient from red on the left to blue on the right, JFIF, no Adobe marker.</summary>
    internal static byte[] ProgressiveRgb => Convert.FromBase64String(
        """
        /9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMU
        FRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQU
        FBQUFBQUFBT/wgARCAAQABgDASIAAhEBAxEB/8QAFgABAQEAAAAAAAAAAAAAAAAAAAYH/8QAFgEBAQEAAAAAAAAAAAAAAAAAAAYH
        /9oADAMBAAIQAxAAAAHK08uNHoU8P//EABUQAQEAAAAAAAAAAAAAAAAAAAAT/9oACAEBAAEFArrrrrrv/8QAFhEAAwAAAAAAAAAA
        AAAAAAAAABZi/9oACAEDAQE/AW2hto//xAAWEQADAAAAAAAAAAAAAAAAAAAAFmL/2gAIAQIBAT8BS5EuT//EABQQAQAAAAAAAAAA
        AAAAAAAAACD/2gAIAQEABj8CX//EABUQAQEAAAAAAAAAAAAAAAAAAABx/9oACAEBAAE/IaUpSlP/2gAMAwEAAgADAAAAEMsv/8QA
        FhEAAwAAAAAAAAAAAAAAAAAAABHw/9oACAEDAQE/EKZTP//EABYRAAMAAAAAAAAAAAAAAAAAAAAR8P/aAAgBAgEBPxCUSj//xAAW
        EAEBAQAAAAAAAAAAAAAAAAARACD/2gAIAQEAAT8QMMOFX//Z
        """);

    /// <summary>Gets the solid cyan YCCK file: Adobe marker with transform 2, 1x1 sampling.</summary>
    internal static byte[] CmykCyan => Convert.FromBase64String(
        """
        /9j/7gAOQWRvYmUAZAAAAAAC/9sAQwACAQEBAQECAQEBAgICAgIEAwICAgIFBAQDBAYFBgYGBQYGBgcJCAYHCQcGBggLCAkKCgoK
        CgYICwwLCgwJCgoK/9sAQwECAgICAgIFAwMFCgcGBwoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoK
        CgoKCgoK/8AAFAgAEAAQBAERAAIRAQMRAQQRAP/EABYAAQEBAAAAAAAAAAAAAAAAAAAICf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/
        xAAWAQEBAQAAAAAAAAAAAAAAAAAACAn/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oADgQBAAIRAxEEAAA/AIvSm38b+AAAAP/Z
        """);

    /// <summary>Gets the progressive YCCK gradient from cyan to magenta.</summary>
    internal static byte[] CmykProgressive => Convert.FromBase64String(
        """
        /9j/7gAOQWRvYmUAZAAAAAAC/9sAQwADAgIDAgIDAwMDBAMDBAUIBQUEBAUKBwcGCAwKDAwLCgsLDQ4SEA0OEQ4LCxAWEBETFBUV
        FQwPFxgWFBgSFBUU/9sAQwEDBAQFBAUJBQUJFA0LDRQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQU
        FBQUFBQU/8IAFAgAGAAgBAERAAIRAQMRAQQRAP/EABYAAQEBAAAAAAAAAAAAAAAAAAAGCP/EABYBAQEBAAAAAAAAAAAAAAAAAAAH
        Bf/aAA4EAQACEAMQBAAAAAGJnt61SABRZkTAAUWXEgAH/8QAFRABAQAAAAAAAAAAAAAAAAAAABP/2gAIAQEAAQUCgggggggggggg
        /8QAFREBAQAAAAAAAAAAAAAAAAAAABL/2gAIAQIBAQUCpSlKUpSlKU//xAAVEQEBAAAAAAAAAAAAAAAAAAAAFP/aAAgBAwEBBQKt
        WrVq1atWrVq1b//EABQQAQAAAAAAAAAAAAAAAAAAADD/2gAIAQQAAQUCT//EABQQAQAAAAAAAAAAAAAAAAAAADD/2gAIAQEABj8C
        T//EABQRAQAAAAAAAAAAAAAAAAAAADD/2gAIAQIBBj8CT//EABQRAQAAAAAAAAAAAAAAAAAAADD/2gAIAQMBBj8CT//EABQQAQAA
        AAAAAAAAAAAAAAAAADD/2gAIAQQABj8CT//EABYQAQEBAAAAAAAAAAAAAAAAAHEAIP/aAAgBAQABPyExjHIGMY3/xAAUEQEAAAAA
        AAAAAAAAAAAAAAAw/9oACAECAQE/IUAA/8QAFREBAQAAAAAAAAAAAAAAAAAAAHH/2gAIAQMBAT8hta1rWta1rW//xAAUEAEAAAAA
        AAAAAAAAAAAAAAAw/9oACAEEAAE/IU//2gAOBAEAAgADAAQAAAAQMzMREVVV/8QAFxAAAwEAAAAAAAAAAAAAAAAAAHGhIP/aAAgB
        AQABPxDAAsFgsFmQD//EABQRAQAAAAAAAAAAAAAAAAAAADD/2gAIAQIBAT8QQAD/xAAUEQEAAAAAAAAAAAAAAAAAAAAw/9oACAED
        AQE/EEqqqq//xAAUEAEAAAAAAAAAAAAAAAAAAAAw/9oACAEEAAE/EE//2Q==
        """);
}
