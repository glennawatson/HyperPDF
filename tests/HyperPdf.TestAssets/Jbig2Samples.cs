// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// JBIG2 streams for decoder tests and benchmarks: real pages from scanned court reports, PDFium's JBIG2 test
/// vectors (BSD-3-Clause) and synthetic streams that cover each region type and coding. Each hash is the SHA-256 of the
/// decoded rows (1 for white), checked against PDFium's render of the same image, or against the encoder's own image
/// where PDFium departs from T.88.
/// </summary>
public static class Jbig2Samples
{
    /// <summary>The width of <see cref="RealLibraryOfCongress"/>.</summary>
    private const int RealLibraryOfCongressWidth = 1824;

    /// <summary>The height of <see cref="RealLibraryOfCongress"/>.</summary>
    private const int RealLibraryOfCongressHeight = 2720;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RealLibraryOfCongress"/>.</summary>
    private const string RealLibraryOfCongressHash = "e27f5bc44af88c36b6fb15f4fef2843683ca0d3eeb351d8237bf24b59d3c0b6e";

    /// <summary>The page segments of <see cref="RealLibraryOfCongress"/>, in base64.</summary>
    private const string RealLibraryOfCongressData =
          "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"
        + "CCIClXko43//f/9/7eukPUorjgxo/3//rA==";

    /// <summary>The width of <see cref="RealGoogleBooks"/>.</summary>
    private const int RealGoogleBooksWidth = 3358;

    /// <summary>The height of <see cref="RealGoogleBooks"/>.</summary>
    private const int RealGoogleBooksHeight = 5266;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RealGoogleBooks"/>.</summary>
    private const string RealGoogleBooksHash = "61a0c71bf97153af3a6fc297c3f63c02b6b5ac313b8e09ee6edead8ccfa95965";

    /// <summary>The page segments of <see cref="RealGoogleBooks"/>, in base64.</summary>
    private const string RealGoogleBooksData =
          "AAAAFjAAAQAAABMAAA0eAAAUkgAAASwAAAEsAAAAAAAAFwAAAQAAAEsAAAP//f8C/v7+AAAABgAAAAZmDRM5/wpVS020hjw3nR9l8nVOO/UUjt6hacBW"
        + "RtKp/Z413Jih0a1427hHogxEziZNP1V7973V/6wAAAAYBkIAFwEAAABNAAANHgAAFJIAAAAAAAAAAAAAAAAAAAudT0vyt5zQS2NUP+ngD0IRyFMx4aga"
        + "Y/GmxiyJIvSKsEGjzuJbq7U2rPUbNSZxtI7s4EqP/6w=";

    /// <summary>The global segments of <see cref="RealGoogleBooks"/>, in base64.</summary>
    private const string RealGoogleBooksGlobals =
          "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";

    /// <summary>The width of <see cref="RealInternetArchiveText"/>.</summary>
    private const int RealInternetArchiveTextWidth = 1758;

    /// <summary>The height of <see cref="RealInternetArchiveText"/>.</summary>
    private const int RealInternetArchiveTextHeight = 3039;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RealInternetArchiveText"/>.</summary>
    private const string RealInternetArchiveTextHash = "d398e4653e2539d9f4daf03f276f7d2102b076440ec69fa08c6177b42480578f";

    /// <summary>The page segments of <see cref="RealInternetArchiveText"/>, in base64.</summary>
    private const string RealInternetArchiveTextData =
          "AAAAADAAAQAAABMAAAbeAAAL3wAAAAAAAAAAUAAAAAAAAQABAQAAAB4AAAP//f8C/v7+AAAAAwAAAANmC8G8hCdf5y8t/6wAAAACACEBAQAAABkAAgP/"
        + "/f8C/v7+/////wAAAAMAAAAAoP+sAAAAAwYgAgEAAAAuAAAG3gAAC98AAAAAAAAAAAIBEv////8AAAAD/xxmli/O7Y9BX2vYn+x92Pf/rA==";

    /// <summary>The width of <see cref="RealInternetArchiveGeneric"/>.</summary>
    private const int RealInternetArchiveGenericWidth = 3550;

    /// <summary>The height of <see cref="RealInternetArchiveGeneric"/>.</summary>
    private const int RealInternetArchiveGenericHeight = 5092;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RealInternetArchiveGeneric"/>.</summary>
    private const string RealInternetArchiveGenericHash = "a757f72cbed5741e122fcb09ef28f57fb38ab7daab015f61d19cdda6c7e18695";

    /// <summary>The page segments of <see cref="RealInternetArchiveGeneric"/>, in base64.</summary>
    private const string RealInternetArchiveGenericData =
          "AAAAADAAAQAAABMAAA3eAAAT5AAAAAAAAAAAAQAAAAAAASYAAQAAAJoAAA3eAAAT5AAAAAAAAAAAAAAD//3/Av7+/v9/hh0PSskHz/9//3//f/9//3//"
        + "f/9//3//f/9//3//f/9//3//f/9//3//f/9//3//f/9//3//f/9//3//f/9//3//f/99JT/TVucIRApDfp4ZCyVoNfIVVjXAH/9//3//f/iFee2tCnh6"
        + "08f/BXVSxvWrzeEh9E2D/3//f/+s";

    /// <summary>The width of <see cref="PdfiumComposeOrXorReplace"/>.</summary>
    private const int PdfiumComposeOrXorReplaceWidth = 399;

    /// <summary>The height of <see cref="PdfiumComposeOrXorReplace"/>.</summary>
    private const int PdfiumComposeOrXorReplaceHeight = 400;

    /// <summary>The SHA-256 of the decoded rows of <see cref="PdfiumComposeOrXorReplace"/>.</summary>
    private const string PdfiumComposeOrXorReplaceHash = "f195624b2fc5505fdbbd3473c58eaf6b22b4f376b57ced779a4357d8bd12bc4f";

    /// <summary>The page segments of <see cref="PdfiumComposeOrXorReplace"/>, in base64.</summary>
    private const string PdfiumComposeOrXorReplaceData =
          "AAAAADAAAQAAABMAAAGPAAABkAAAAAAAAAAAQAAAAAAAAScAAQAAADYAAAAXAAAAFwAAADcAAABwAAAD//3/Av7+/nLGp27ufdFudh6GKMazwDz4b75j"
        + "L/jivvd//6wAAAACJwABAAAANgAAABcAAAAXAAAANwAAAHAAAAP//f8C/v7+csanbu590W52HoYoxrPAPPhvvmMv+OK+93//rAAAAAMnAAEAAAA2AAAA"
        + "FwAAABcAAABAAAABDQIAA//9/wL+/v5yxqdu7n3RbnYehijGs8A8+G++Yy/44r73f/+sAAAABCcAAQAAADYAAAAXAAAAFwAAADwAAAC+AgAD//3/Av7+"
        + "/nLGp27ufdFudh6GKMazwDz4b75jL/jivvd//6wAAAAFJwABAAAANgAAABcAAAAXAAAAPAAAAL4CAAP//f8C/v7+csanbu590W52HoYoxrPAPPhvvmMv"
        + "+OK+93//rAAAAAYnAAEAAAD+AAABAgAAAZAAAACNAAAAAAAAA//9/wL+/v7/eMdCy56M/3/83R567jhCjq8HFFHX19wU0IbAwAGNtuzG6kOE3hISVCgF"
        + "11RIbdsPvcg8A75zHO//eEsOkDDYGag1w1uLfWriP1Jx+9yfPmz3myFAeqT5BU4gOHnr8Y83lKXDqqlMzliPFzAK8jxdDYGnSeo5tAl9zjqPQBK8droe"
        + "Bv4+28zMhNvRKCZfcJRyJWmYlW3Wfl3S5Md7+M6p5z4UG/u9+QyJ/mFj+mcMQq/5EwqcMbfuk7XeaQN216pf9sdBycZyTsMO3ooe6hvoOiXIoBGsS91r"
        + "SzcoleAy+1Wf/6wAAAAHJwABAAAA3QAAAQIAAAGQAAAAjQAAAAAEAAP//f8C/v7+q/Oi/17c/fQREcCLVEbVyTZ+1q6knLbJCF4gzVeLSU2t7ZuOpzQu"
        + "LB6VHZ//caCN9QnR/2Opgc+SmUIVL0RqrJ3OqgSU0S9h+6D/eT2kMPXvffAf9n/lrn9NGuUFUBI2bv3F9nvekEFc1e5teKSn35OOlC7AuE5lyFb4q5d8"
        + "0F/SQGP/Pi78Oz2lMf4dKJZq/yoHdHuLtV5oTP2+9KdF6ZQB/h3pfcz63hQyVgw0m7Znv0JDuj+xpqlajYdQPQPGf/+s";

    /// <summary>The width of <see cref="PdfiumComposeAndXnor"/>.</summary>
    private const int PdfiumComposeAndXnorWidth = 399;

    /// <summary>The height of <see cref="PdfiumComposeAndXnor"/>.</summary>
    private const int PdfiumComposeAndXnorHeight = 400;

    /// <summary>The SHA-256 of the decoded rows of <see cref="PdfiumComposeAndXnor"/>.</summary>
    private const string PdfiumComposeAndXnorHash = "f195624b2fc5505fdbbd3473c58eaf6b22b4f376b57ced779a4357d8bd12bc4f";

    /// <summary>The page segments of <see cref="PdfiumComposeAndXnor"/>, in base64.</summary>
    private const string PdfiumComposeAndXnorData =
          "AAAAADAAAQAAABMAAAGPAAABkAAAAAAAAAAARAAAAAAAAScAAQAAAFEAAACNAAABkAAAAAAAAAAAAQAD//3/Av7+/qvykM+9qaIverCIxjqbc0mtqFFF"
        + "L/Du0cGp1jMXyyJWuiedOzsoRwY3tgc2rSrTdzYq6P2i/6wAAAACJwABAAAAUQAAAI0AAAGQAAAAAAAAAAABAAP//f8C/v7+q/KQz72poi96sIjGOptz"
        + "Sa2oUUUv8O7RwanWMxfLIla6J507OyhHBje2BzatKtN3Niro/aL/rAAAAAMnAAEAAADdAAABAgAAAZAAAACNAAAAAAMAA//9/wL+/v6r86L/Xtz99BER"
        + "wItURtXJNn7WrqSctskIXiDNV4tJTa3tm46nNC4sHpUdn/9xoI31CdH/Y6mBz5KZQhUvRGqsnc6qBJTRL2H7oP95PaQw9e998B/2f+Wuf00a5QVQEjZu"
        + "/cX2e96QQVzV7m14pKffk46ULsC4TmXIVvirl3zQX9JAY/8+Lvw7PaUx/h0olmr/Kgd0e4u1XmhM/b70p0XplAH+Hel9zPreFDJWDDSbtme/QkO6P7Gm"
        + "qVqNh1A9A8Z//6wAAAAEJwABAAAANgAAABcAAAAXAAAAPAAAAL4DAAP//f8C/v7+csanbu590W52HoYoxrPAPPhvvmMv+OK+93//rAAAAAUnAAEAAAA2"
        + "AAAAFwAAABcAAAA8AAAAvgMAA//9/wL+/v5yxqdu7n3RbnYehijGs8A8+G++Yy/44r73f/+s";

    /// <summary>The width of <see cref="GenericTemplate0Typical"/>.</summary>
    private const int GenericTemplate0TypicalWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate0Typical"/>.</summary>
    private const int GenericTemplate0TypicalHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate0Typical"/>.</summary>
    private const string GenericTemplate0TypicalHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate0Typical"/>, in base64.</summary>
    private const string GenericTemplate0TypicalData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAHUAAAA9AAAAJQAAAAAAAAAAAAgD//3/Av7+/r5MX8QIE7dl8jDsgA7SXdSM2xJ9"
        + "2s4i/0KKdKhqCXFLAenMzxHf6/1SvVZoqA+/XSEKAhHtc6odBIsWHJW6Z/rol4ff0yZW5HvNriVbDLtemE83ICezB9a//6wAAAACMQABAAAAAA==";

    /// <summary>The width of <see cref="GenericTemplate0Adaptive"/>.</summary>
    private const int GenericTemplate0AdaptiveWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate0Adaptive"/>.</summary>
    private const int GenericTemplate0AdaptiveHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate0Adaptive"/>.</summary>
    private const string GenericTemplate0AdaptiveHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate0Adaptive"/>, in base64.</summary>
    private const string GenericTemplate0AdaptiveData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAHgAAAA9AAAAJQAAAAAAAAAAAAD7AP7/A/7//qT6/m7JHXHRLXdWJ5CCUA1J7Pxd"
        + "dVPa/t//f8y7Ph+p6ajlFNU2b4siZuL6ZHAcRdF6iqZS88Qfasx6AOkGyF2YP8ra6KeJOltpg3IMD0LCJERkdO5NDvEsWIDO/6wAAAACMQABAAAAAA==";

    /// <summary>The width of <see cref="GenericTemplate1Typical"/>.</summary>
    private const int GenericTemplate1TypicalWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate1Typical"/>.</summary>
    private const int GenericTemplate1TypicalHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate1Typical"/>.</summary>
    private const string GenericTemplate1TypicalHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate1Typical"/>, in base64.</summary>
    private const string GenericTemplate1TypicalData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGkAAAA9AAAAJQAAAAAAAAAAAAoD/75MAvTKD1m74Zn42QsIYw7N9kV6hS0R8jiO"
        + "ForC5uU/crj3i0yFJ1KUOAbdiWUdNq/teG9K9jc/vAsGUPEW2DCRiJcf/B3UBGdqjpfOFpkuNgR//6wAAAACMQABAAAAAA==";

    /// <summary>The width of <see cref="GenericTemplate1Adaptive"/>.</summary>
    private const int GenericTemplate1AdaptiveWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate1Adaptive"/>.</summary>
    private const int GenericTemplate1AdaptiveHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate1Adaptive"/>.</summary>
    private const string GenericTemplate1AdaptiveHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate1Adaptive"/>, in base64.</summary>
    private const string GenericTemplate1AdaptiveData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGoAAAA9AAAAJQAAAAAAAAAAAAL+/qT6hbpWKb442yXi2mQevL2fpKzlsMb/edpP"
        + "1Y+5NQ1nuurInxYGwVgInZhipAB5Hba2zEcyp7G/8n9evPBIEatWPAYWfP95b16wvWXEM4fY3Pzlff+sAAAAAjEAAQAAAAA=";

    /// <summary>The width of <see cref="GenericTemplate2"/>.</summary>
    private const int GenericTemplate2Width = 61;

    /// <summary>The height of <see cref="GenericTemplate2"/>.</summary>
    private const int GenericTemplate2Height = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate2"/>.</summary>
    private const string GenericTemplate2Hash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate2"/>, in base64.</summary>
    private const string GenericTemplate2Data =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGIAAAA9AAAAJQAAAAAAAAAAAAQC/6T6k45GCPJbTVgYZ2QSHYGg0BCSFrbmNJ3i"
        + "auRl3/MZRkmMT0qA2xdiqX2FNgzB3J04+Fb/CfS7SKjHGyBII/INqRjZJDn/CCFpxp//rAAAAAIxAAEAAAAA";

    /// <summary>The width of <see cref="GenericTemplate2Adaptive"/>.</summary>
    private const int GenericTemplate2AdaptiveWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate2Adaptive"/>.</summary>
    private const int GenericTemplate2AdaptiveHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate2Adaptive"/>.</summary>
    private const string GenericTemplate2AdaptiveHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate2Adaptive"/>, in base64.</summary>
    private const string GenericTemplate2AdaptiveData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGEAAAA9AAAAJQAAAAAAAAAAAAz9AL5MAvTKD2WaJjFqTuRhuRlisu/TUrOvLL+C"
        + "yiuxbB0cBTV4TrVcMGcyylRLjwMtkO+HnZ4smc7+TNbwqIppjM5cY9WpK1AwcNuhz/+sAAAAAjEAAQAAAAA=";

    /// <summary>The width of <see cref="GenericTemplate3Typical"/>.</summary>
    private const int GenericTemplate3TypicalWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate3Typical"/>.</summary>
    private const int GenericTemplate3TypicalHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate3Typical"/>.</summary>
    private const string GenericTemplate3TypicalHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate3Typical"/>, in base64.</summary>
    private const string GenericTemplate3TypicalData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGAAAAA9AAAAJQAAAAAAAAAAAA4C/75MX8QIFCaOXy6rfxWf/1Q2I7wfp3o6VPuI"
        + "6Kkz0XYNJONP62L4EeocuIlIeGABAU49w16UJPoN8JJYywWfHNYpv7qU83mYB4/f/6wAAAACMQABAAAAAA==";

    /// <summary>The width of <see cref="GenericTemplate3Adaptive"/>.</summary>
    private const int GenericTemplate3AdaptiveWidth = 61;

    /// <summary>The height of <see cref="GenericTemplate3Adaptive"/>.</summary>
    private const int GenericTemplate3AdaptiveHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericTemplate3Adaptive"/>.</summary>
    private const string GenericTemplate3AdaptiveHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericTemplate3Adaptive"/>, in base64.</summary>
    private const string GenericTemplate3AdaptiveData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGMAAAA9AAAAJQAAAAAAAAAAAAYB/qT61dulw2RHvuCMIqz9I/LYUbepPMy7RqEl"
        + "U6yPHw1fqNEJr0zp0LzqbJ6Z1p62Mj1jaqIX75GxpYxZN0hJmLHOcJ+Yja3GkLEKHyur/6wAAAACMQABAAAAAA==";

    /// <summary>The width of <see cref="GenericMmr"/>.</summary>
    private const int GenericMmrWidth = 61;

    /// <summary>The height of <see cref="GenericMmr"/>.</summary>
    private const int GenericMmrHeight = 37;

    /// <summary>The SHA-256 of the decoded rows of <see cref="GenericMmr"/>.</summary>
    private const string GenericMmrHash = "5b029d3f81900dcc285d1f05f19769ed2f0bedd369d01c92d88ef4f5a9c4db73";

    /// <summary>The page segments of <see cref="GenericMmr"/>, in base64.</summary>
    private const string GenericMmrData =
          "AAAAADAAAQAAABMAAAA9AAAAJQAAAAAAAAAAAAAAAAAAASYAAQAAAGMAAAA9AAAAJQAAAAAAAAAAAAHN5+M4jxUL4P8X4lxR////////EiIebcwv45s/"
        + "BA1EkGJ5yhL/BHH/xiEEEDwioXyI/j4ecyowoicua1/2VD+I4jPP/L/kfOr//xOog8AEAEAAAAACMQABAAAAAA==";

    /// <summary>The width of <see cref="ComposeOperators"/>.</summary>
    private const int ComposeOperatorsWidth = 50;

    /// <summary>The height of <see cref="ComposeOperators"/>.</summary>
    private const int ComposeOperatorsHeight = 40;

    /// <summary>The SHA-256 of the decoded rows of <see cref="ComposeOperators"/>.</summary>
    private const string ComposeOperatorsHash = "488ec5840b1fa7774d1ce7f8cc6329cbab12a0ad0c8e1d4364b1c011345522cb";

    /// <summary>The page segments of <see cref="ComposeOperators"/>, in base64.</summary>
    private const string ComposeOperatorsData =
          "AAAAADAAAQAAABMAAAAyAAAAKAAAAAAAAAAABAAAAAAAASYAAQAAAEMAAAAeAAAAFAAAAAMAAAACAAAD//3/Av7+/o8xj28nyqi/eiTba8PgoCYhiojN"
        + "FzkVOImInmSM6wzWTUBTowiAJf+sAAAAAiYAAQAAADMAAAAZAAAAFgAAABQAAAAPAQID/3WD25sXNZOPdkZgYhn8DvciLTd2vuU2L3HnQXkD/6wAAAAD"
        + "JgABAAAAMgAAABkAAAAW////+wAAABkCBAL/da/PNKrdmbmYzEOLcusJOfjupM+v4nRTLPnbz/+sAAAABDIAAQAAAAQAAAAnAAAABSYAAQAAADcAAAAe"
        + "AAAAFAAAACH////8AwYC/48xj2/2llK6iK/g07XvTxQcv4nfW2QGuszsfcOMfszBe/+sAAAABiYAAQAAAC4AAAAZAAAAFgAAAAoAAAAKBAE0OMoOXWbo"
        + "j/4ZVhvynXmiEuuXY/4xKKaPjzo+AAAABzEAAQAAAAA=";

    /// <summary>The width of <see cref="RefinePage"/>.</summary>
    private const int RefinePageWidth = 48;

    /// <summary>The height of <see cref="RefinePage"/>.</summary>
    private const int RefinePageHeight = 32;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RefinePage"/>.</summary>
    private const string RefinePageHash = "434842a24cbb81c5ac5af6bc917b7acd923b096a56954ff96d8bcb2e94079241";

    /// <summary>The page segments of <see cref="RefinePage"/>, in base64.</summary>
    private const string RefinePageData =
          "AAAAADAAAQAAABMAAAAwAAAAIAAAAAAAAAAAAAAAAAAAASYAAQAAAG4AAAAwAAAAIAAAAAAAAAAAAAAD//3/Av7+/pvcfYIvhJ72PV2EO5L4R8igYB0o"
        + "HUo8aJ0bvk7Qid0GBcERv36RcamfdMj9qu0Ub/UnwXYVwPSXrQ3dyqjguH5MtITTi81dgfkBSvq4k7lPEuv/rAAAAAIqAAEAAABZAAAAMAAAACAAAAAA"
        + "AAAAAAQC/////6Xko9fGxIKlCbvqFNv/L2+tPh28V1m3brqkwj/0mjtSVb01BZjOqCtyzbm5fkjv2GE18zmHm9giHAc43Eh0U5/r/6wAAAADMQABAAAA"
        + "AA==";

    /// <summary>The width of <see cref="RefineIntermediate"/>.</summary>
    private const int RefineIntermediateWidth = 60;

    /// <summary>The height of <see cref="RefineIntermediate"/>.</summary>
    private const int RefineIntermediateHeight = 44;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RefineIntermediate"/>.</summary>
    private const string RefineIntermediateHash = "35c06edeea2dce4c1e1391bd8a7653f9cd37b5c412adab0550f9c4801baa12de";

    /// <summary>The page segments of <see cref="RefineIntermediate"/>, in base64.</summary>
    private const string RefineIntermediateData =
          "AAAAADAAAQAAABMAAAA8AAAALAAAAAAAAAAAAAAAAAAAASQAAQAAAFkAAAAwAAAAIAAAAAAAAAAAAAwC/6PvP4ky4erYDjJcrsAEBUnI+Q81FWgtpHgd"
        + "7HMEZoUnlvUKrFn1fdTFLGHm8MnMJzJD/DPD1XQ+lNVMcSMnfUSRHv3/rAAAAAIqIAEBAAAAUgAAADAAAAAgAAAABwAAAAYAAaffz18dlYbCIhLXlVfu"
        + "ZvRsEGsLND8wwK7bFXOtwDAdvZxa+5YBhM18amEqQngf64eWVAmGkQnmIsELDUd//6wAAAADMQABAAAAAA==";

    /// <summary>The width of <see cref="RefineChain"/>.</summary>
    private const int RefineChainWidth = 60;

    /// <summary>The height of <see cref="RefineChain"/>.</summary>
    private const int RefineChainHeight = 44;

    /// <summary>The SHA-256 of the decoded rows of <see cref="RefineChain"/>.</summary>
    private const string RefineChainHash = "dcc0994c29c1caa3188c53105aa16b50c39df83b2c7c9cb04418ed2ca3566f82";

    /// <summary>The page segments of <see cref="RefineChain"/>, in base64.</summary>
    private const string RefineChainData =
          "AAAAADAAAQAAABMAAAA8AAAALAAAAAAAAAAAAAAAAAAAASQAAQAAAG4AAAAwAAAAIAAAAAAAAAAAAAAD//3/Av7+/pvcfYIvhJ72PV2EO5L4R8igYB0o"
        + "HUo8aJ0bvk7Qid0GBcERv36RcamfdMj9qu0Ub/UnwXYVwPSXrQ3dyqjguH5MtITTi81dgfkBSvq4k7lPEuv/rAAAAAIoIAEBAAAAWgAAADAAAAAgAAAA"
        + "AAAAAAAAAP4AAQGfy4oZvTmXFKJhp6N9aviVs5M+C9cmXOuyNdTECmEKJ4syYlLSr9ehg86eriONj8haxSfKoPJPq0atpir6iRtQqS//rAAAAAMqIAIB"
        + "AAAATwAAADAAAAAgAAAACQAAAAQCA9FPS9tp8T85em4NRj2fiYoN7UaIkBxx68p+nfW5rCuRGNDa5/Bs8SdMp7LsZxF1mXSKglMhTRI851xt/6wAAAAE"
        + "MQABAAAAAA==";

    /// <summary>The width of <see cref="TextHuffman"/>.</summary>
    private const int TextHuffmanWidth = 90;

    /// <summary>The height of <see cref="TextHuffman"/>.</summary>
    private const int TextHuffmanHeight = 60;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextHuffman"/>.</summary>
    private const string TextHuffmanHash = "9a07d62d92f102ae0e43022b389f49c52e95aceb5d302e21fb0ad74112c2d489";

    /// <summary>The page segments of <see cref="TextHuffman"/>, in base64.</summary>
    private const string TextHuffmanData =
          "AAAAADAAAQAAABMAAABaAAAAPAAAAAAAAAAAAAAAAAAAAQAAAQAAAF0AAQAAAAkAAAAJ49OD8ACZMFMA54FIANGQRACFwVhA////4Ljt+AC6MEDtcQDr"
        + "NsCOZoDIZAC/ZkD//8C4cr8AnBcgtFxAvBLA1DQQuxNAmv4gvLUAlFAQ///wAkAAAAACBiABAQAAAHwAAABaAAAAPAAAAAAAAAAAAAARAAAAAAA6ZmZm"
        + "ZmZmZmZmZmZmZmZmZmZhBBBBBBBBAFvrEulgH0nTaeAXQwGgE+ABGA8AIDAOi6IJ0PUPhf3IwDgFqAojQBiwHpXwAsaIkDCQD0wTTQB6MvpHwAC6boiw"
        + "Do4CEdB0xcQgAAAAAzEAAQAAAAA=";

    /// <summary>The width of <see cref="TextHuffmanTransposed"/>.</summary>
    private const int TextHuffmanTransposedWidth = 90;

    /// <summary>The height of <see cref="TextHuffmanTransposed"/>.</summary>
    private const int TextHuffmanTransposedHeight = 60;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextHuffmanTransposed"/>.</summary>
    private const string TextHuffmanTransposedHash = "4dfedffd154b30d1b6f964e6e3df643e1f0861079205f1b9267dc52df77c33c6";

    /// <summary>The page segments of <see cref="TextHuffmanTransposed"/>, in base64.</summary>
    private const string TextHuffmanTransposedData =
          "AAAAADAAAQAAABMAAABaAAAAPAAAAAAAAAAAAAAAAAAAAQAAAQAAAF0AAQAAAAkAAAAJ49OD8ACZMFMA54FIANGQRACFwVhA////4Ljt+AC6MEDtcQDr"
        + "NsCOZoDIZAC/ZkD//8C4cr8AnBcgtFxAvBLA1DQQuxNAmv4gvLUAlFAQ///wAkAAAAACBiABAQAAAIkAAABaAAAAPAAAAAAAAAAAAAF1AAAAAAA7ZmZm"
        + "ZmZmZmZmZmZmZmZmZmZhBBBBBBBBAEvoug6r1gVgF1UldgKfeRjDt1BAc8AMoru9F6h1DoBAKdUIjDVXd+vHrqEevV9XQJRMQ7u5oCNDgF6AECzsKx6x"
        + "qXUBOBhW0QJ0R3foaEAwLdE0gkMBhAAAAAMxAAEAAAAA";

    /// <summary>The width of <see cref="TextHuffmanBottomRight"/>.</summary>
    private const int TextHuffmanBottomRightWidth = 90;

    /// <summary>The height of <see cref="TextHuffmanBottomRight"/>.</summary>
    private const int TextHuffmanBottomRightHeight = 60;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextHuffmanBottomRight"/>.</summary>
    private const string TextHuffmanBottomRightHash = "5437a46675d94838cd5a9254bf42b2d5539f414992c15f3958df9d3d1ac9e567";

    /// <summary>The page segments of <see cref="TextHuffmanBottomRight"/>, in base64.</summary>
    private const string TextHuffmanBottomRightData =
          "AAAAADAAAQAAABMAAABaAAAAPAAAAAAAAAAAAAAAAAAAAQAAAQAAAF0AAQAAAAkAAAAJ49OD8ACZMFMA54FIANGQRACFwVhA////4Ljt+AC6MEDtcQDr"
        + "NsCOZoDIZAC/ZkD//8C4cr8AnBcgtFxAvBLA1DQQuxNAmv4gvLUAlFAQ///wAkAAAAACBiABAQAAAI8AAABaAAAAPAAAAAAAAAAAAHwpAAAAAAA4ZmZm"
        + "ZmZmZmZmZmZmZmZmZmZhBBBBBBBBAAPwWEcDbqiPKrCh0MQdIxkegewrAXlLwg59uJGquxExGbJ0rKRbfx+h4APS+kyKcjLBQKMFVoHigymjqWSxqKBv"
        + "1wr0HhwGAjjvrLz1hF2gbq6sTB9ISFtJR09DIAAAAAMxAAEAAAAA";

    /// <summary>The width of <see cref="TextHuffmanCustomTable"/>.</summary>
    private const int TextHuffmanCustomTableWidth = 90;

    /// <summary>The height of <see cref="TextHuffmanCustomTable"/>.</summary>
    private const int TextHuffmanCustomTableHeight = 60;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextHuffmanCustomTable"/>.</summary>
    private const string TextHuffmanCustomTableHash = "6c0606022838be2df8495b168a18e3cf575be11a636cc8a2574f57df8084f47e";

    /// <summary>The page segments of <see cref="TextHuffmanCustomTable"/>, in base64.</summary>
    private const string TextHuffmanCustomTableData =
          "AAAAADAAAQAAABMAAABaAAAAPAAAAAAAAAAAAAAAAAAAATUAAQAAAA4l////+AAAAChtNRyRgAAAAAIAAAEAAABdAAEAAAAJAAAACePTg/AAmTBTAOeB"
        + "SADRkEQAhcFYQP///+C47fgAujBA7XEA6zbAjmaAyGQAv2ZA///AuHK/AJwXILRcQLwSwNQ0ELsTQJr+ILy1AJRQEP//8AJAAAAAAwZAAQIBAAAAeQAA"
        + "AFoAAAA8AAAAAAAAAAAAABEADAAAADdmZmZmZmZmZmZmZmZmZmZmZmEEEEEEEEEAUBmSBAMBIAhiMAgF94X8hARBgRBKExCExwAt4XwiOERQAxIJgAFg"
        + "KNyDvABwQRsEhERgEMAqQN4X0yOOBKGAsAAARxqNQhgAAAAEMQABAAAAAA==";

    /// <summary>The width of <see cref="TextHuffmanMmrEndOfBlock"/>.</summary>
    private const int TextHuffmanMmrEndOfBlockWidth = 90;

    /// <summary>The height of <see cref="TextHuffmanMmrEndOfBlock"/>.</summary>
    private const int TextHuffmanMmrEndOfBlockHeight = 60;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextHuffmanMmrEndOfBlock"/>.</summary>
    private const string TextHuffmanMmrEndOfBlockHash = "6c0606022838be2df8495b168a18e3cf575be11a636cc8a2574f57df8084f47e";

    /// <summary>The page segments of <see cref="TextHuffmanMmrEndOfBlock"/>, in base64.</summary>
    private const string TextHuffmanMmrEndOfBlockData =
          "AAAAADAAAQAAABMAAABaAAAAPAAAAAAAAAAAAAAAAAAAATUAAQAAAA4l////+AAAAChtNRyRgAAAAAIAAAEAAACTAAEAAAAJAAAACePTg/hQJqi+XRfO"
        + "Ijov4YTEILYIdBFRLodlZQhArEILCQSxEREWACACuO38ICaojxHRjWIQTBFDwgn0rhILCikutwuh0ED7WIiMAEAEuHK/iUAmqLxxEeCXQRHQQUQkvGMI"
        + "ECXZHRHSpCtJkfdnHCe9AhTXTlDgiOgQIewgVhBRFYiIjABABAJAAAAAAwZAAQIBAAAAeAAAAFoAAAA8AAAAAAAAAAAAABEAAAAAADdmZmZmZmZmZmZm"
        + "ZmZmZmZmZmEEEEEEEEEAUBnAjQdBpAXQ9AQLvhfy0EBOggJpSehJ9QCfC+FAcjqAek0wAsF6eAL4AOIA9MS0UBgXQFYAPhfTgP1GlMFgACA/S9KQQAAA"
        + "AAQxAAEAAAAA";

    /// <summary>The width of <see cref="TextHuffmanRefineCustom"/>.</summary>
    private const int TextHuffmanRefineCustomWidth = 90;

    /// <summary>The height of <see cref="TextHuffmanRefineCustom"/>.</summary>
    private const int TextHuffmanRefineCustomHeight = 60;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextHuffmanRefineCustom"/>.</summary>
    private const string TextHuffmanRefineCustomHash = "efcda43e7eea47342f81f06912390406cdc0dd6138df96fa2d81b931bb9d843d";

    /// <summary>The page segments of <see cref="TextHuffmanRefineCustom"/>, in base64.</summary>
    private const string TextHuffmanRefineCustomData =
          "AAAAADAAAQAAABMAAABaAAAAPAAAAAAAAAAAAAAAAAAAATUAAQAAAA4l////+AAAAChtNRyRgAAAAAIAAAEAAACTAAEAAAAJAAAACePTg/hQJqi+XRfO"
        + "Ijov4YTEILYIdBFRLodlZQhArEILCQSxEREWACACuO38ICaojxHRjWIQTBFDwgn0rhILCikutwuh0ED7WIiMAEAEuHK/iUAmqLxxEeCXQRHQQUQkvGMI"
        + "ECXZHRHSpCtJkfdnHCe9AhTXTlDgiOgQIewgVhBRFYiIjABABAJAAAAAAwZAAQIBAAABJAAAAFoAAAA8AAAAAAAAAAAAgBMADAAAADdmZmZmZmZmZmZm"
        + "ZmZmZmZmZmEEEEEEEEEAUBnbUwALY0Yo/6wkBAGG7Y6pGBE63/+sGgBBjMQAvke1F1NX/6wYAgC8tSBIBDac4wjG/6zeF/IIBE1RgMXhvDX/rCAIgSyj"
        + "gMUC0ZZg/6wJhCCfa4yty7uv/6wUAE3hfC3bGOsV6wv/rCuCIUS1AOiv5sFWmf+sAYSBNwKAiy8A/6ycAWAWMYBsZUuP/6wbiC3gA45iAObhnkV3a/+s"
        + "GIbAlbCgCdei/6wRCMAWsKCQRUD/rCACogRSgOnYZ/+s3hfTEcOO2pyNJfI+cf+sCoMAtutI4j//rAgAAj7lOPyBSBZ//6wihqCFtSiClff/rMAAAAAE"
        + "MQABAAAAAA==";

    /// <summary>The width of <see cref="TextArith"/>.</summary>
    private const int TextArithWidth = 96;

    /// <summary>The height of <see cref="TextArith"/>.</summary>
    private const int TextArithHeight = 64;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextArith"/>.</summary>
    private const string TextArithHash = "94d9fd742fb41af15ca56b60fa09846f2bbfb540a82bcefdb2b46121f896d126";

    /// <summary>The page segments of <see cref="TextArith"/>, in base64.</summary>
    private const string TextArithData =
          "AAAAADAAAQAAABMAAABgAAAAQAAAAAAAAAAAAAAAAAAAAQAAAQAAAE8IAAL/AAAACQAAAAlSkbzNJInzAc0Ie6nQinFCyqdfMHUlbn9WotPZ2PDUOI/K"
        + "qlHzCW4KxiW+sPG7ow8o4d5Awfdn1kkbaAb3/0zN7/+sAAAAAgYgAQEAAABSAAAAYAAAAEAAAAAAAAAAAAB4EAAAADuiaudyLN30VfUtm/4SYT4RP7M3"
        + "12c7XMHheJPyC21YawIEyiygz/6FDEjdXai+sXhonqdO4TfAHuL/rAAAAAMxAAEAAAAA";

    /// <summary>The width of <see cref="TextArithRefine"/>.</summary>
    private const int TextArithRefineWidth = 96;

    /// <summary>The height of <see cref="TextArithRefine"/>.</summary>
    private const int TextArithRefineHeight = 64;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextArithRefine"/>.</summary>
    private const string TextArithRefineHash = "a8cdaf1885c3cc709ae68e8cf6880a232bc9972ece02b4f3273462111d67df38";

    /// <summary>The page segments of <see cref="TextArithRefine"/>, in base64.</summary>
    private const string TextArithRefineData =
          "AAAAADAAAQAAABMAAABgAAAAQAAAAAAAAAAAAAAAAAAAAQAAAQAAAE8IAAL/AAAACQAAAAlSkbzNJInzAc0Ie6nQinFCyqdfMHUlbn9WotPZ2PDUOI/K"
        + "qlHzCW4KxiW+sPG7ow8o4d5Awfdn1kkbaAb3/0zN7/+sAAAAAgYgAQEAAADwAAAAYAAAAEAAAAAAAAAAAAB4Bv//AQEAAAA+pitHevS0qT830YmpIZfx"
        + "l4WT5g2ste8bRJQSgc/h6CuIIVdnzEpE0OW1lDgdXYNGiYBxmop1tcbov3Mw7YhxRybC3Vf3K7ptl6IWG6yrBTUCgRg9CuiWiHd6Gcowc0ihcP8BUPI/"
        + "R+aRkcuKyA0HfXvfXBdbKsSucZRqo7wlwUP3kjbQHhcoNcal+6U5eFIUNhot97kc5V17NYsPa51lZPXc7eabKt17rpQIW43gJQGIPhN9tmuUpDVm8vHs"
        + "9xOFmJqd8QhXpnnZiQC1bQZWJ5vAYf+sAAAAAzEAAQAAAAA=";

    /// <summary>The width of <see cref="TextArithTransposed"/>.</summary>
    private const int TextArithTransposedWidth = 96;

    /// <summary>The height of <see cref="TextArithTransposed"/>.</summary>
    private const int TextArithTransposedHeight = 64;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextArithTransposed"/>.</summary>
    private const string TextArithTransposedHash = "b53d4fca00260800bcd9854be7c12761ede2d1aa205b6c2d54bd8250195829d2";

    /// <summary>The page segments of <see cref="TextArithTransposed"/>, in base64.</summary>
    private const string TextArithTransposedData =
          "AAAAADAAAQAAABMAAABgAAAAQAAAAAAAAAAAAAAAAAAAAQAAAQAAAE8IAAL/AAAACQAAAAlSkbzNJInzAc0Ie6nQinFCyqdfMHUlbn9WotPZ2PDUOI/K"
        + "qlHzCW4KxiW+sPG7ow8o4d5Awfdn1kkbaAb3/0zN7/+sAAAAAgYgAQEAAADsAAAAYAAAAEAAAAAAAAAAAAD7bgAAADqoDHIZW0hpdQN1u11BzCJj+qYh"
        + "wOhlgYy+P1aEbCJFFGizZypEDGyuEK8wq86fBKSkRsrGXEpJp0ypDhs+rIPl1jIbXAdPXRx02QUmiTFu39PoMAMmZEhtnfzIa1p2G/gXuiD6CtDFzU4K"
        + "QOyyTINgedWDJP8cP8O5asRaLaLYxjrw/w94lslXJEo9rZrqI/FQ7o18sZf6/k4kCCkPA57168oHUQVsq+dZOAtIHkxKESv9zvqSsZeCfIFRbZqwh45i"
        + "QiJ8lyUVUA3mBjGyz5FpfhG//6wAAAADMQABAAAAAA==";

    /// <summary>The width of <see cref="TextArithTopRight"/>.</summary>
    private const int TextArithTopRightWidth = 96;

    /// <summary>The height of <see cref="TextArithTopRight"/>.</summary>
    private const int TextArithTopRightHeight = 64;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextArithTopRight"/>.</summary>
    private const string TextArithTopRightHash = "ab8350b75d47a14ca7c834b0ad2b82fd690873116a3d7f96afe2e9eb31ed0f47";

    /// <summary>The page segments of <see cref="TextArithTopRight"/>, in base64.</summary>
    private const string TextArithTopRightData =
          "AAAAADAAAQAAABMAAABgAAAAQAAAAAAAAAAAAAAAAAAAAQAAAQAAAE8IAAL/AAAACQAAAAlSkbzNJInzAc0Ie6nQinFCyqdfMHUlbn9WotPZ2PDUOI/K"
        + "qlHzCW4KxiW+sPG7ow8o4d5Awfdn1kkbaAb3/0zN7/+sAAAAAgYgAQEAAADhAAAAYAAAAEAAAAAAAAAAAAD5ugAAADuqEv3H0neRce6KPEcXNkWON5mM"
        + "rkn7Ue8uV4ur1De0yO3aKtI21XGS1C8/7S18Dhk9MgYFljm4N74zM3uyb6a3pd3QU8nsudj4CnMbOakqQoBir528wULJCFyJAuZ17Hu4K3ABgQtS2pbw"
        + "zungnrwsYH528Lj+H+wODU0iAfShprbgAH1Mbjs5cNqvIVlO2XB3j9yafwRuMhHwr+cpCqcLquVqlSoDBjVCbseGoZHsIAC5lHHJkY10cyEj3/PKtUmD"
        + "f/YSXtK1Zv+sAAAAAzEAAQAAAAA=";

    /// <summary>The width of <see cref="TextArithRefineClipped"/>.</summary>
    private const int TextArithRefineClippedWidth = 40;

    /// <summary>The height of <see cref="TextArithRefineClipped"/>.</summary>
    private const int TextArithRefineClippedHeight = 30;

    /// <summary>The SHA-256 of the decoded rows of <see cref="TextArithRefineClipped"/>.</summary>
    private const string TextArithRefineClippedHash = "b830cf3e51b5ae1205b3beaf25d8f934fec8eec9614e5e0526a274ef3665044d";

    /// <summary>The page segments of <see cref="TextArithRefineClipped"/>, in base64.</summary>
    private const string TextArithRefineClippedData =
          "AAAAADAAAQAAABMAAAAoAAAAHgAAAAAAAAAAAAAAAAAAAQAAAQAAAE8IAAL/AAAACQAAAAlSkbzNJInzAc0Ie6nQinFCyqdfMHUlbn9WotPZ2PDUOI/K"
        + "qlHzCW4KxiW+sPG7ow8o4d5Awfdn1kkbaAb3/0zN7/+sAAAAAgYgAQEAAABLAAAAKAAAAB4AAAAAAAAAAAD4EgAAAAalKUw//ZZzcKCY05TQGZDStjgo"
        + "QTXNkrKNNPAXOJP6lFIHenDey7aVnaL4Pxb5lkysv/+sAAAAAzEAAQAAAAA=";

    /// <summary>The width of <see cref="SymbolsGlobalsRefinementAggregate"/>.</summary>
    private const int SymbolsGlobalsRefinementAggregateWidth = 80;

    /// <summary>The height of <see cref="SymbolsGlobalsRefinementAggregate"/>.</summary>
    private const int SymbolsGlobalsRefinementAggregateHeight = 50;

    /// <summary>The SHA-256 of the decoded rows of <see cref="SymbolsGlobalsRefinementAggregate"/>.</summary>
    private const string SymbolsGlobalsRefinementAggregateHash = "c280c50c12ba29693f5ef76c1b3c71d4999ef931590c45b2cfecf2ad25e97084";

    /// <summary>The page segments of <see cref="SymbolsGlobalsRefinementAggregate"/>, in base64.</summary>
    private const string SymbolsGlobalsRefinementAggregateData =
          "AAAAATAAAQAAABMAAABQAAAAMgAAAAAAAAAAAAAAAAAAAgAgAAEAAABKCAIC//////8AAAAJAAAAB1KEWjsR8luXft4TN/67Kev4wLQXZdLqHUnBZqXt"
        + "prRL4+5YZw2tq8+9y5UwbpDtFUkr0eHbQ2f3/6wAAAADBkAAAgEAAAA+AAAAUAAAADIAAAAAAAAAAAB4EAAAAB6iKunULr/dRybM5HuiJWVcf4725mNz"
        + "0UqQhjC4x3b2rgOsc5Z9/6wAAAAEMQABAAAAAA==";

    /// <summary>The global segments of <see cref="SymbolsGlobalsRefinementAggregate"/>, in base64.</summary>
    private const string SymbolsGlobalsRefinementAggregateGlobals =
          "AAAAAAAAAAAAAFMAAAP//f8C/v7+AAAACQAAAAlSkZsMVIn2EAAdzXqPexzDIjOkWpfQUDWZemHl1Bhmsnm/fRzXV1pjgl/Tj1T+Foq5LH+zEs0G8gA/"
        + "Pr1nXg//rA==";

    /// <summary>The width of <see cref="SymbolsHuffmanAggregate"/>.</summary>
    private const int SymbolsHuffmanAggregateWidth = 80;

    /// <summary>The height of <see cref="SymbolsHuffmanAggregate"/>.</summary>
    private const int SymbolsHuffmanAggregateHeight = 50;

    /// <summary>The SHA-256 of the decoded rows of <see cref="SymbolsHuffmanAggregate"/>.</summary>
    private const string SymbolsHuffmanAggregateHash = "7dd05d73c471a3b8dc21e7711ab40a3f9d8323b30712d8713d00cea769ccdfec";

    /// <summary>The page segments of <see cref="SymbolsHuffmanAggregate"/>, in base64.</summary>
    private const string SymbolsHuffmanAggregateData =
          "AAAAADAAAQAAABMAAABQAAAAMgAAAAAAAAAAAAAAAAAAAQAAAQAAAF0AAQAAAAkAAAAJ49OD8ACZMFMA54FIANGQRACFwVhA////4Ljt+AC6MEDtcQDr"
        + "NsCOZoDIZAC/ZkD//8C4cr8AnBcgtFxAvBLA1DQQuxNAmv4gvLUAlFAQ///wAkAAAAACACABAQAAACMQAwAAAAUAAAAD6+CEABzASZwgDuZeUPYYB4Mv"
        + "KGBED+ARxgAAAAMEQAECAQAAADwAAABQAAAAMgAAAAAAAAAAAHgQAAAAHaJ4VoLd84dDjNRbCE91YiugHxbWh/TzKAYhGWLeo8GKAxUT/6wAAAAEKiAD"
        + "AQAAAMIAAABQAAAAMgAAAAAAAAAAAAFyspR5W7sudKWFnYL50R8IeuaHTU20hTZMjCD/QE8RHVd8AjyDBl0QVZM3I+4Q5osheZ1VSNfQCELv5ILXekqf"
        + "fw8f12GuInqaEBPNqBcvr/oH94fCs4EsPZ1wLR6WFc9sN/9v7xMsEhDNl4IiQwEWvVxgWHHU1zsecidOPXZb04MVvGKU82GhujVSGd+pujI4gUfF62Xr"
        + "7+5U6qPUmax8/wZxtAnalq7e79p0YX//rAAAAAUxAAEAAAAA";

    /// <summary>The width of <see cref="SymbolsHuffmanRefinementAggregate"/>.</summary>
    private const int SymbolsHuffmanRefinementAggregateWidth = 80;

    /// <summary>The height of <see cref="SymbolsHuffmanRefinementAggregate"/>.</summary>
    private const int SymbolsHuffmanRefinementAggregateHeight = 50;

    /// <summary>The SHA-256 of the decoded rows of <see cref="SymbolsHuffmanRefinementAggregate"/>.</summary>
    private const string SymbolsHuffmanRefinementAggregateHash = "aa622860503a2735a3fc170f525b1262c11450ad2dac4d8a0a8160cade2da721";

    /// <summary>The page segments of <see cref="SymbolsHuffmanRefinementAggregate"/>, in base64.</summary>
    private const string SymbolsHuffmanRefinementAggregateData =
          "AAAAADAAAQAAABMAAABQAAAAMgAAAAAAAAAAAAAAAAAAAQAAAQAAAF0AAQAAAAkAAAAJ49OD8ACZMFMA54FIANGQRACFwVhA////4Ljt+AC6MEDtcQDr"
        + "NsCOZoDIZAC/ZkD//8C4cr8AnBcgtFxAvBLA1DQQuxNAmv4gvLUAlFAQ///wAkAAAAACACABAQAAAFEQAwAAAAkAAAAH49wiILEwRBXwL/+s/uoVEPx2"
        + "FgHZZ/+sBWjgp5NphD//rP7qF4QAT+igYsM//6zkIADmAkzhAHcy8oewwDwZeUMCIH8AjnAAAAADBEABAgEAAABBAAAAUAAAADIAAAAAAAAAAAB4EAAA"
        + "ACCihMa0P0xLAtWJwhG8UR0rSpE2nKlD/397/SLq8B+Am0eH+gbNZHS//6wAAAAEKiADAQAAAMMAAABQAAAAMgAAAAAAAAAAAAFyspR522QXTpySRk3L"
        + "ahN6peNYzU4NXdIbloXIm50xLO4gkk3mgMIDcZsVsjRzHmpB/1ZX41CKT67JCO1PycjXMNzD1CQIUvEI0uaqnO07Le8U+PBREBCgJAejWIFqwTezI4wS"
        + "JmbG2Oz/WQBHBP61TGMJb5SWM9jcZd7ydgX7qDa1VlibrArbLuh9oF9fb/I/KO+2O+Nh2gGT/xJSkFlNLvEc3gdJH0virWcNVnB//6wAAAAFMQABAAAA"
        + "AA==";

    /// <summary>The width of <see cref="HalftoneArith"/>.</summary>
    private const int HalftoneArithWidth = 64;

    /// <summary>The height of <see cref="HalftoneArith"/>.</summary>
    private const int HalftoneArithHeight = 48;

    /// <summary>The SHA-256 of the decoded rows of <see cref="HalftoneArith"/>.</summary>
    private const string HalftoneArithHash = "1ab78550d4cfe393d34e7dd9b3710930efc0b52452201ed8554d97fcbb7aaaa2";

    /// <summary>The page segments of <see cref="HalftoneArith"/>, in base64.</summary>
    private const string HalftoneArithData =
          "AAAAADAAAQAAABMAAABAAAAAMAAAAAAAAAAAAAAAAAAAARAAAQAAABQABAQAAAAFV/95+rRaFy5G/rX/rAAAAAIWIAEBAAAAiQAAAEAAAAAwAAAAAAAA"
        + "AAAAAgAAABIAAAAO///+AP///wAEAAAADu5VOt4WFQECXV4Ey53TiMnLNdKmW+TCxHrWzDf/WoKjZrvhU0Q3xWGLrtUeDbyQa5rs/KcWULUyad8nUj19"
        + "jSIkeQN7kgceoAY1JAZci7Os2Fk6ZFCIjsfiQJ+y5yDZn/+sAAAAAzEAAQAAAAA=";

    /// <summary>The width of <see cref="HalftoneArithSkip"/>.</summary>
    private const int HalftoneArithSkipWidth = 64;

    /// <summary>The height of <see cref="HalftoneArithSkip"/>.</summary>
    private const int HalftoneArithSkipHeight = 48;

    /// <summary>The SHA-256 of the decoded rows of <see cref="HalftoneArithSkip"/>.</summary>
    private const string HalftoneArithSkipHash = "d0a1218a5c55c0e688fd9904fdd660aa70dca4b48dca7a1c7123c59c650ebaf6";

    /// <summary>The page segments of <see cref="HalftoneArithSkip"/>, in base64.</summary>
    private const string HalftoneArithSkipData =
          "AAAAADAAAQAAABMAAABAAAAAMAAAAAAAAAAAAAAAAAAAARAAAQAAABQABAQAAAAFV/95+rRaFy5G/rX/rAAAAAIWIAEBAAAAeQAAAEAAAAAwAAAAAAAA"
        + "AAAAKgAAABIAAAAO///+AP///wAEAABADtp+rP8KOVxoRQ1G+ABexkFDFAwsXB3Sxd+MIzADiX4RaoObs4Zuzyz7UR7fsHsDiB6sMIue/tQfQJihmhrS"
        + "AuCTCj5MvQcQOGYAv+T4Y7yf/6wAAAADMQABAAAAAA==";

    /// <summary>The width of <see cref="HalftoneMmr"/>.</summary>
    private const int HalftoneMmrWidth = 64;

    /// <summary>The height of <see cref="HalftoneMmr"/>.</summary>
    private const int HalftoneMmrHeight = 48;

    /// <summary>The SHA-256 of the decoded rows of <see cref="HalftoneMmr"/>.</summary>
    private const string HalftoneMmrHash = "1ab78550d4cfe393d34e7dd9b3710930efc0b52452201ed8554d97fcbb7aaaa2";

    /// <summary>The page segments of <see cref="HalftoneMmr"/>, in base64.</summary>
    private const string HalftoneMmrData =
          "AAAAADAAAQAAABMAAABAAAAAMAAAAAAAAAAAAAAAAAAAARAAAQAAACEBBAQAAAAFNowi8R4jmKQRQ4QWEEMRaSRQ5Q88iOggviMAAAACFiABAQAAAP8A"
        + "AABAAAAAMAAAAAAAAAAAAEEAAAASAAAADv///gD///8ABAAAACPkdF0XgQSoJJIIEkuhYIEtbI6LsaekIQS7I+R6sMJAiPQ9wcJUiOgjwLCBBNUUOCsM"
        + "IU1QQVwQIQQSscIIaGgRHXDgglABABAmuXRHRHiOrqxkdBKtBFDsbizOwrMOmEEKhAgynSBLRdAgSi/aYRxwrEECdXRQ5Vgo3jhBYpQgoaRdEfQeG0kw"
        + "sGCFl2oAIAImrI+R0X1ggSEW0owyOggQYXC2kCahxGElCQTdhEeCKHKHCBP2OKXGEEIQQrBFDhBCyOglHDBAkPBBXiR6lsE2twrlOhUAEAEAAAADMQAB"
        + "AAAAAA==";

    /// <summary>Gets a Library of Congress page: a symbol dictionary, a refinement/aggregate dictionary, a refined text region and an XOR generic region.</summary>
    public static Jbig2Sample RealLibraryOfCongress { get; } = new(
        nameof(RealLibraryOfCongress),
        RealLibraryOfCongressWidth,
        RealLibraryOfCongressHeight,
        Convert.FromBase64String(RealLibraryOfCongressData),
        [],
        RealLibraryOfCongressHash);

    /// <summary>Gets a Google Books page: a global symbol dictionary, a page dictionary and a text region.</summary>
    public static Jbig2Sample RealGoogleBooks { get; } = new(
        nameof(RealGoogleBooks),
        RealGoogleBooksWidth,
        RealGoogleBooksHeight,
        Convert.FromBase64String(RealGoogleBooksData),
        Convert.FromBase64String(RealGoogleBooksGlobals),
        RealGoogleBooksHash);

    /// <summary>Gets an Internet Archive page with two dictionaries and a refined text region.</summary>
    public static Jbig2Sample RealInternetArchiveText { get; } = new(
        nameof(RealInternetArchiveText),
        RealInternetArchiveTextWidth,
        RealInternetArchiveTextHeight,
        Convert.FromBase64String(RealInternetArchiveTextData),
        [],
        RealInternetArchiveTextHash);

    /// <summary>Gets an Internet Archive cover page: one large template 0 generic region.</summary>
    public static Jbig2Sample RealInternetArchiveGeneric { get; } = new(
        nameof(RealInternetArchiveGeneric),
        RealInternetArchiveGenericWidth,
        RealInternetArchiveGenericHeight,
        Convert.FromBase64String(RealInternetArchiveGenericData),
        [],
        RealInternetArchiveGenericHash);

    /// <summary>Gets PDFium's composite-or-xor-replace test: lossless generic regions combined with OR, XOR and REPLACE.</summary>
    public static Jbig2Sample PdfiumComposeOrXorReplace { get; } = new(
        nameof(PdfiumComposeOrXorReplace),
        PdfiumComposeOrXorReplaceWidth,
        PdfiumComposeOrXorReplaceHeight,
        Convert.FromBase64String(PdfiumComposeOrXorReplaceData),
        [],
        PdfiumComposeOrXorReplaceHash);

    /// <summary>Gets PDFium's composite-and-xnor test: lossless generic regions combined with AND, then XNOR.</summary>
    public static Jbig2Sample PdfiumComposeAndXnor { get; } = new(
        nameof(PdfiumComposeAndXnor),
        PdfiumComposeAndXnorWidth,
        PdfiumComposeAndXnorHeight,
        Convert.FromBase64String(PdfiumComposeAndXnorData),
        [],
        PdfiumComposeAndXnorHash);

    /// <summary>Gets a template 0 generic region with typical prediction.</summary>
    public static Jbig2Sample GenericTemplate0Typical { get; } = new(
        nameof(GenericTemplate0Typical),
        GenericTemplate0TypicalWidth,
        GenericTemplate0TypicalHeight,
        Convert.FromBase64String(GenericTemplate0TypicalData),
        [],
        GenericTemplate0TypicalHash);

    /// <summary>Gets a template 0 generic region whose adaptive pixels are moved, one onto the current row.</summary>
    public static Jbig2Sample GenericTemplate0Adaptive { get; } = new(
        nameof(GenericTemplate0Adaptive),
        GenericTemplate0AdaptiveWidth,
        GenericTemplate0AdaptiveHeight,
        Convert.FromBase64String(GenericTemplate0AdaptiveData),
        [],
        GenericTemplate0AdaptiveHash);

    /// <summary>Gets a template 1 generic region with typical prediction.</summary>
    public static Jbig2Sample GenericTemplate1Typical { get; } = new(
        nameof(GenericTemplate1Typical),
        GenericTemplate1TypicalWidth,
        GenericTemplate1TypicalHeight,
        Convert.FromBase64String(GenericTemplate1TypicalData),
        [],
        GenericTemplate1TypicalHash);

    /// <summary>Gets a template 1 generic region with a moved adaptive pixel.</summary>
    public static Jbig2Sample GenericTemplate1Adaptive { get; } = new(
        nameof(GenericTemplate1Adaptive),
        GenericTemplate1AdaptiveWidth,
        GenericTemplate1AdaptiveHeight,
        Convert.FromBase64String(GenericTemplate1AdaptiveData),
        [],
        GenericTemplate1AdaptiveHash);

    /// <summary>Gets a template 2 generic region.</summary>
    public static Jbig2Sample GenericTemplate2 { get; } = new(
        nameof(GenericTemplate2),
        GenericTemplate2Width,
        GenericTemplate2Height,
        Convert.FromBase64String(GenericTemplate2Data),
        [],
        GenericTemplate2Hash);

    /// <summary>Gets a template 2 generic region with typical prediction and an adaptive pixel on the current row.</summary>
    public static Jbig2Sample GenericTemplate2Adaptive { get; } = new(
        nameof(GenericTemplate2Adaptive),
        GenericTemplate2AdaptiveWidth,
        GenericTemplate2AdaptiveHeight,
        Convert.FromBase64String(GenericTemplate2AdaptiveData),
        [],
        GenericTemplate2AdaptiveHash);

    /// <summary>Gets a template 3 generic region with typical prediction.</summary>
    public static Jbig2Sample GenericTemplate3Typical { get; } = new(
        nameof(GenericTemplate3Typical),
        GenericTemplate3TypicalWidth,
        GenericTemplate3TypicalHeight,
        Convert.FromBase64String(GenericTemplate3TypicalData),
        [],
        GenericTemplate3TypicalHash);

    /// <summary>Gets a template 3 generic region with a moved adaptive pixel.</summary>
    public static Jbig2Sample GenericTemplate3Adaptive { get; } = new(
        nameof(GenericTemplate3Adaptive),
        GenericTemplate3AdaptiveWidth,
        GenericTemplate3AdaptiveHeight,
        Convert.FromBase64String(GenericTemplate3AdaptiveData),
        [],
        GenericTemplate3AdaptiveHash);

    /// <summary>Gets an MMR generic region ending with an end-of-block code.</summary>
    public static Jbig2Sample GenericMmr { get; } = new(
        nameof(GenericMmr),
        GenericMmrWidth,
        GenericMmrHeight,
        Convert.FromBase64String(GenericMmrData),
        [],
        GenericMmrHash);

    /// <summary>Gets regions at offsets, partly off a black page, with every combination operator and an end-of-stripe segment.</summary>
    public static Jbig2Sample ComposeOperators { get; } = new(
        nameof(ComposeOperators),
        ComposeOperatorsWidth,
        ComposeOperatorsHeight,
        Convert.FromBase64String(ComposeOperatorsData),
        [],
        ComposeOperatorsHash);

    /// <summary>Gets a refinement of the page with template 0, typical prediction and moved adaptive pixels.</summary>
    public static Jbig2Sample RefinePage { get; } = new(
        nameof(RefinePage),
        RefinePageWidth,
        RefinePageHeight,
        Convert.FromBase64String(RefinePageData),
        [],
        RefinePageHash);

    /// <summary>Gets a template 1 refinement of an intermediate generic region, placed at an offset.</summary>
    public static Jbig2Sample RefineIntermediate { get; } = new(
        nameof(RefineIntermediate),
        RefineIntermediateWidth,
        RefineIntermediateHeight,
        Convert.FromBase64String(RefineIntermediateData),
        [],
        RefineIntermediateHash);

    /// <summary>Gets an intermediate refinement refined again with XOR.</summary>
    public static Jbig2Sample RefineChain { get; } = new(
        nameof(RefineChain),
        RefineChainWidth,
        RefineChainHeight,
        Convert.FromBase64String(RefineChainData),
        [],
        RefineChainHash);

    /// <summary>Gets a Huffman symbol dictionary with uncompressed collective bitmaps and a Huffman text region.</summary>
    public static Jbig2Sample TextHuffman { get; } = new(
        nameof(TextHuffman),
        TextHuffmanWidth,
        TextHuffmanHeight,
        Convert.FromBase64String(TextHuffmanData),
        [],
        TextHuffmanHash);

    /// <summary>Gets a transposed Huffman text region with two-row strips, top-right corners and XOR.</summary>
    public static Jbig2Sample TextHuffmanTransposed { get; } = new(
        nameof(TextHuffmanTransposed),
        TextHuffmanTransposedWidth,
        TextHuffmanTransposedHeight,
        Convert.FromBase64String(TextHuffmanTransposedData),
        [],
        TextHuffmanTransposedHash);

    /// <summary>Gets a Huffman text region with four-row strips, bottom-right corners and a negative S offset.</summary>
    public static Jbig2Sample TextHuffmanBottomRight { get; } = new(
        nameof(TextHuffmanBottomRight),
        TextHuffmanBottomRightWidth,
        TextHuffmanBottomRightHeight,
        Convert.FromBase64String(TextHuffmanBottomRightData),
        [],
        TextHuffmanBottomRightHash);

    /// <summary>Gets a Huffman text region whose S deltas use a custom table segment.</summary>
    public static Jbig2Sample TextHuffmanCustomTable { get; } = new(
        nameof(TextHuffmanCustomTable),
        TextHuffmanCustomTableWidth,
        TextHuffmanCustomTableHeight,
        Convert.FromBase64String(TextHuffmanCustomTableData),
        [],
        TextHuffmanCustomTableHash);

    /// <summary>Gets a Huffman dictionary whose MMR collective bitmaps end with end-of-block codes; PDFium ignores the bitmap size and fails here.</summary>
    public static Jbig2Sample TextHuffmanMmrEndOfBlock { get; } = new(
        nameof(TextHuffmanMmrEndOfBlock),
        TextHuffmanMmrEndOfBlockWidth,
        TextHuffmanMmrEndOfBlockHeight,
        Convert.FromBase64String(TextHuffmanMmrEndOfBlockData),
        [],
        TextHuffmanMmrEndOfBlockHash);

    /// <summary>
    /// Gets refined instances in a Huffman text region with a custom table and MMR collective bitmaps. PDFium reads
    /// neighbouring symbols' pixels when refining and fails on the end-of-block codes.
    /// </summary>
    public static Jbig2Sample TextHuffmanRefineCustom { get; } = new(
        nameof(TextHuffmanRefineCustom),
        TextHuffmanRefineCustomWidth,
        TextHuffmanRefineCustomHeight,
        Convert.FromBase64String(TextHuffmanRefineCustomData),
        [],
        TextHuffmanRefineCustomHash);

    /// <summary>Gets an arithmetic symbol dictionary with template 2 and an arithmetic text region.</summary>
    public static Jbig2Sample TextArith { get; } = new(
        nameof(TextArith),
        TextArithWidth,
        TextArithHeight,
        Convert.FromBase64String(TextArithData),
        [],
        TextArithHash);

    /// <summary>Gets refined instances with template 0, two-row strips and bottom-left corners.</summary>
    public static Jbig2Sample TextArithRefine { get; } = new(
        nameof(TextArithRefine),
        TextArithRefineWidth,
        TextArithRefineHeight,
        Convert.FromBase64String(TextArithRefineData),
        [],
        TextArithRefineHash);

    /// <summary>Gets a transposed text region with eight-row strips, a black default pixel, XOR and template 1 refinement.</summary>
    public static Jbig2Sample TextArithTransposed { get; } = new(
        nameof(TextArithTransposed),
        TextArithTransposedWidth,
        TextArithTransposedHeight,
        Convert.FromBase64String(TextArithTransposedData),
        [],
        TextArithTransposedHash);

    /// <summary>Gets a text region with four-row strips, top-right corners, XNOR and refinement.</summary>
    public static Jbig2Sample TextArithTopRight { get; } = new(
        nameof(TextArithTopRight),
        TextArithTopRightWidth,
        TextArithTopRightHeight,
        Convert.FromBase64String(TextArithTopRightData),
        [],
        TextArithTopRightHash);

    /// <summary>Gets refined instances that the region's right edge clips.</summary>
    public static Jbig2Sample TextArithRefineClipped { get; } = new(
        nameof(TextArithRefineClipped),
        TextArithRefineClippedWidth,
        TextArithRefineClippedHeight,
        Convert.FromBase64String(TextArithRefineClippedData),
        [],
        TextArithRefineClippedHash);

    /// <summary>Gets a global dictionary refined and aggregated by a page dictionary that also exports two of its symbols.</summary>
    public static Jbig2Sample SymbolsGlobalsRefinementAggregate { get; } = new(
        nameof(SymbolsGlobalsRefinementAggregate),
        SymbolsGlobalsRefinementAggregateWidth,
        SymbolsGlobalsRefinementAggregateHeight,
        Convert.FromBase64String(SymbolsGlobalsRefinementAggregateData),
        Convert.FromBase64String(SymbolsGlobalsRefinementAggregateGlobals),
        SymbolsGlobalsRefinementAggregateHash);

    /// <summary>Gets a Huffman refinement/aggregate dictionary of aggregated symbols, an intermediate text region and its refinement.</summary>
    public static Jbig2Sample SymbolsHuffmanAggregate { get; } = new(
        nameof(SymbolsHuffmanAggregate),
        SymbolsHuffmanAggregateWidth,
        SymbolsHuffmanAggregateHeight,
        Convert.FromBase64String(SymbolsHuffmanAggregateData),
        [],
        SymbolsHuffmanAggregateHash);

    /// <summary>Gets a Huffman refinement/aggregate dictionary with refined and aggregated symbols; PDFium reads neighbouring symbols' pixels when refining.</summary>
    public static Jbig2Sample SymbolsHuffmanRefinementAggregate { get; } = new(
        nameof(SymbolsHuffmanRefinementAggregate),
        SymbolsHuffmanRefinementAggregateWidth,
        SymbolsHuffmanRefinementAggregateHeight,
        Convert.FromBase64String(SymbolsHuffmanRefinementAggregateData),
        [],
        SymbolsHuffmanRefinementAggregateHash);

    /// <summary>Gets an arithmetic pattern dictionary and halftone region with a grid partly off the page.</summary>
    public static Jbig2Sample HalftoneArith { get; } = new(
        nameof(HalftoneArith),
        HalftoneArithWidth,
        HalftoneArithHeight,
        Convert.FromBase64String(HalftoneArithData),
        [],
        HalftoneArithHash);

    /// <summary>Gets a halftone region with skipped cells, a slanted grid vector and XOR.</summary>
    public static Jbig2Sample HalftoneArithSkip { get; } = new(
        nameof(HalftoneArithSkip),
        HalftoneArithSkipWidth,
        HalftoneArithSkipHeight,
        Convert.FromBase64String(HalftoneArithSkipData),
        [],
        HalftoneArithSkipHash);

    /// <summary>Gets an MMR pattern dictionary and MMR halftone planes combined with REPLACE.</summary>
    public static Jbig2Sample HalftoneMmr { get; } = new(
        nameof(HalftoneMmr),
        HalftoneMmrWidth,
        HalftoneMmrHeight,
        Convert.FromBase64String(HalftoneMmrData),
        [],
        HalftoneMmrHash);

    /// <summary>Gets every sample.</summary>
    public static IReadOnlyList<Jbig2Sample> All { get; } =
    [
        RealLibraryOfCongress,
        RealGoogleBooks,
        RealInternetArchiveText,
        RealInternetArchiveGeneric,
        PdfiumComposeOrXorReplace,
        PdfiumComposeAndXnor,
        GenericTemplate0Typical,
        GenericTemplate0Adaptive,
        GenericTemplate1Typical,
        GenericTemplate1Adaptive,
        GenericTemplate2,
        GenericTemplate2Adaptive,
        GenericTemplate3Typical,
        GenericTemplate3Adaptive,
        GenericMmr,
        ComposeOperators,
        RefinePage,
        RefineIntermediate,
        RefineChain,
        TextHuffman,
        TextHuffmanTransposed,
        TextHuffmanBottomRight,
        TextHuffmanCustomTable,
        TextHuffmanMmrEndOfBlock,
        TextHuffmanRefineCustom,
        TextArith,
        TextArithRefine,
        TextArithTransposed,
        TextArithTopRight,
        TextArithRefineClipped,
        SymbolsGlobalsRefinementAggregate,
        SymbolsHuffmanAggregate,
        SymbolsHuffmanRefinementAggregate,
        HalftoneArith,
        HalftoneArithSkip,
        HalftoneMmr,
    ];

    /// <summary>Gets a sample by name.</summary>
    /// <param name="name">The sample's name.</param>
    /// <returns>The sample.</returns>
    /// <exception cref="KeyNotFoundException">No sample has the name.</exception>
    public static Jbig2Sample Get(string name)
    {
        foreach (var sample in All)
        {
            if (sample.Name == name)
            {
                return sample;
            }
        }

        throw new KeyNotFoundException(name);
    }
}
