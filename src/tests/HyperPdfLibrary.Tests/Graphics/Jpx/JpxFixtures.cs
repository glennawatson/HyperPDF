// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Small JPXDecode streams taken from the test corpus, with the SHA-256 of the BGRA pixels PDFium renders for
/// each when drawn at 1:1 with <c>/ColorSpace /DeviceRGB</c>.
/// </summary>
internal static class JpxFixtures
{
    /// <summary>The width of <see cref="Reversible"/>.</summary>
    internal const int ReversibleWidth = 1816;

    /// <summary>The height of <see cref="Reversible"/>.</summary>
    internal const int ReversibleHeight = 2983;

    /// <summary>The SHA-256 of PDFium's BGRA rendering of <see cref="Reversible"/>.</summary>
    internal const string ReversiblePixels = "2464A348B9C1326555B8E72BE8B5B115DB02CEE2DB04B78A6CE720877C6C7B32";

    /// <summary>The JP2 file ia-us-reports-228-p2-1.jpx from the corpus, base64.</summary>
    internal const string Reversible =
        "AAAADGpQICANCocKAAAAFGZ0eXBqcDIgAAAAAGpwMiAAAABHanAyaAAAABZpaGRyAAALpwAABxgAAwcHAQAAAAAPY29scgEAAAAAABAAAAAacmVzIAAAABJy"
        + "ZXNjJxAA/icQAP4AAAAAAABqcDJj/0//UQAvAAAAAAcYAAALpwAAAAAAAAAAAAAHGAAAC6cAAAAAAAAAAAADBwEBBwEBBwEB/1IADAAAABQBBQQEAAH/XAAT"
        + "IFBYWGBYWGBYWGBYWFhQUFj/ZAARAAFLYWthZHUtdjguMC4z/2QBnwABS2R1LUxheWVyLUluZm86IGxvZ18ye0RlbHRhLUQoc3F1YXJlZC1lcnJvcikvRGVs"
        + "dGEtTChieXRlcyl9LCBMKGJ5dGVzKQogIDE2LjUsICA1LjVlKzAyCiAgMTUuNSwgIDUuN2UrMDIKICAxNC41LCAgNS45ZSswMgogIDEzLjUsICA2LjFlKzAy"
        + "CiAgMTIuNSwgIDcuNWUrMDIKICAxMS41LCAgOC41ZSswMgogIDEwLjUsICA4LjllKzAyCiAgIDkuNSwgIDkuM2UrMDIKICAgOC41LCAgOS41ZSswMgogICA3"
        + "LjUsICA5LjZlKzAyCiAgIDYuNSwgIDEuMWUrMDMKICAgNS41LCAgMS4yZSswMwogICA0LjUsICAxLjJlKzAzCiAgIDMuNSwgIDEuNWUrMDMKICAgMi41LCAg"
        + "MS42ZSswMwogICAxLjUsICAxLjllKzAzCiAgIDAuNSwgIDIuMmUrMDMKICAtMC41LCAgMi44ZSswMwogIC0xLjUsICAzLjVlKzAzCiAgLTIuNSwgIDQuM2Ur"
        + "MDMK/5AACgAAAAAOtgAB/5OAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIDn"
        + "neARUVO0qx0Prd//Vir0a6wUBLUcGkNmaumyISXhQz4kRIXYRid68HkTkN1E/hip/m3DEFQ+PqgZeKdFcbFpt5WXNqnb3jh0yelUsWA30NFze4NRSAaz9d2k"
        + "Xv3tiHM/LgI2/gOcRwb6DyMlEQy4qspVCOuws1iEl/eAgICAgICAgICAgICAgICAgLzqwBFQVK2ecTGnETbtxjp/gf8z6L7PcLg4DzbvJJfadW3MFsriVsdn"
        + "TGON8M+f+gJVKBx1Dsbt+SPw+UDwTYVfAkIfuQJfcSiyIwKDC3C287OsjWKTwPVSgICAgICAgICAgICAgICAgID4KOAxkVAHWGVrnJsKB1NXOqaKqLNJgICA"
        + "gICAgICAgICAgICAgIC8ESOEunDO4WfuK5uxSWiilVG4gICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgICAgIDWMNMf"
        + "ACIkQaRL87C81R1VLgsoZT9Qk60c1y3EXSDQuhnwRclYCs83TFk2m4Hq3iisKQWgoS9vFy1JKY+0i7aRJl8Y28WjJG0SCO9o/M+qfXJzHiFBfWDoGKIjiI9V"
        + "vYYXgoT3mICAgICAgICAgICAgICAgICAqiDvHerRkTeR0rzC/kJocS8y0SC5CW7hH8cdN9p6+SF/EzX0PIDeYvkDFJo8r32H+CThZlMnSdJpYxSowUJIkYWv"
        + "hsdYCoCAgICAgICAgICAgICAgICAgICA4bQYa4ArNVadlhQJADyobL0SQ4VN75HXm5k40giAUqLL9d6AgICAgICAgICAgICAgPDpwlicyhXoMyRYfLUDVUuC"
        + "vvNYEQkoFgSZ6eW5OVIDy0OZl8CLxwB1PjHxkP0SkMzo6fYREqA57ATM8XMJQT4RLlKHv9bNYdGBwXIIsBaMkbQ4ARiYvgGMin9jos5I/raqhH8AJ14Y9d8B"
        + "aNThVw0eYJDmKg77BU2ooLvYEp9XNZKFhN4F5Q0Ez7WPh+XMZ6880hUDJW5wE4MMvXXQw5i9/E+UXAxG0sWFmAdOl4SRHnAmHZhRW6ErPpoo2V5t7Vn1CICA"
        + "s7XAnyW+6TsTuJ935mkzHlUTN5EPy0UsgICAgICAgICAgICAgICAgIDwzytx3iuDaIClkQQF+NEUz89owTW6LYx0kcgdkiRgDrANASGCwueyhTfgiEClPvNd"
        + "nGbnYSSLTZc3eJWXuW1eCwbLOkoBtIK0JBvdTaiiLz37SdHl2F3vIEImaVp2opSLRhWk0i6xonG3xyyVhAKvFN3x4FDgUjQdnfiAgICAgICAgICAgICAgPDp"
        + "wlCxjvOYJ1RIPHbvVUuCERA+3evfgjhAgk2waZgGc1aAGbzr3HZGSUwlO8H303PUwdZltjOJbBhqQWffuRO2fIrgOxrbBvGNpmAe+BXLJMsvXtNWGm4G0lDE"
        + "1EixoS6/uSEeZeyndEHd2cWjU4VcNFFggM5rUMrlQai1Yi3QJc3RTaD+CA5xVCPvCQSE95/UH3oTQKtAE2+qpT8hZBGLHeQQP6CKrqjCKyfEdZjhfD/tRtuo"
        + "9Pt2wFjYE/H33LevgIDFESF8eHgcqGvAP7nwUwZN972iUy8IlrXZRleXhvsH1svvunfByfqAgOQ+IADVxSvHhgzvPICAgICAgICAgICAgICA4hxLjGIxq0xR"
        + "ACKUWpn92Nypb3kVwyAFgJ2aBbG8gPQ5PhdCB6JU9CiYfd1eAiGzOE8okI0EpHTh5RsdVhbDi63a/EsYdxyxPMyVUnAY7ZuiF7oNhN9vKoh79aoVlptkcAOO"
        + "LuTx5MNcrU1QksVkhJqzkEjXX2GAgNVem1/rfW16xw+oVV6x6xtr7WAoD8C+KzQv95gKzdccj+W6hJEwo3ipTsb8ODFFpSkgAhzYskIWSrf39ki/dzoaskXl"
        + "xf0SwkuA2DCqciGN1ZGYd9NHReItRhhNLzzkcvZ4YOZzoGnJzQmxswyUkAVfOEK8W+n9SMFlJ6FM6G3yAPrEAkKduNziBgd8lTTAVCcg0oFbHc4zy78d8OlW"
        + "NbHFBErueH0j+pvVAFl+5Oe7MyYruDr3kUfsiNx4H6r8+kMizoBzkbOr4nU43+olfBLCVEBBXpaZyj2S2mglAvuA5tiyNwfPK5ET1YKABPiXpimgim1u7b2f"
        + "v3+NSWGAgICAgICAgICAgPDxwnjGV3xAs0MH/KYNy8pCR1657xa0SgFB3cYpsADmimCA2AX1UaA+4yWken0TowVQByz4RXCf5R95bv8BYxapctBTXxBxxank"
        + "6lOaDlYuq8nBiGujBTUtJNjSAE6sPTBImFjEisqbG35pGPcgCmCT0BSb47TPCgq1ykHUZiZ9JpvioUSBwcAiyKaH6MZB+cYOE3NnuGgDnHlk9ny/CIk8aPUf"
        + "+kW9Wvce3YSO7iIltW5yXBzAyzHF7a0pEQu3j3bQ+cGASr9Fabw94PKOpCBHVg8Hir1MkIq1Y2Iag3tSnyWe+DiuWt8F0GfgSalXgJ6jzFRrNTksSAKbCj0W"
        + "/gnUNccH0XW9zz01/sP4xTYpa7W7AtynO9Xcss/jEOMprZIb9G1488QXL1ZuJfHfGCFeFbCPu0EJYbQ5rn6B28j3DUW25trYqRsl5WhdKnUbzgDqbZK1vOHK"
        + "dR1SBEAU7iXI/cbkVfYwbhFd95s0UoCSPaWAgMbEk1MdynPEzKhwpoPpTUDlYj4J2y2TVDNrjONNLuVSKU0ti/WKKW9QJO4EnzdPNOWjNthBw9/my3B6lnae"
        + "fJC/hLOtY6YF3efznUwnmvd3ixVQIPWhn5PnEIqef56/uWW2gH4FHN6Wka8VPmkKN7PJmlNV1f1HcPwm8TmYAInizWccCD2bYGikIuwSrmkdN3eggICAgICA"
        + "gICAgIDw0o/ziQY0VPA8wIle9T2uCFd0Qol1uF/YCxGeQgA0wl1lkDjyUk9b3Wc+oVulGizGQsrcHMQrkSY14WFHjvwbAQ8QdgEhHbUQQ0RrlkOUSgb48Az/"
        + "NmdDpBerQr8Sgx0HjghwIqfQxUOYrrhoCPrkLaoZDiH8y2h1MfjwIKXqQRW1fDix4J1EtwqnxCHhsLhg4Yoj/0UzdQeReDhZYimAcqDevJV6HGkwXpdfLkAk"
        + "2UALyh7bGIKAgJhYcGhMNwiqx6nxD9Z9bK62YMWIjBXec8gPH9MfW45nwd3cltt1/0yF2CIBj8NwK5wMKS2ebwYaoqpeGEfH8HG5StOkMkWER8tz/OfU2y/9"
        + "e4dz4+FG937neJUqElZqo9323p/iF1Lb6YHwDi2Htj1KNd+nZXIJK4w5xpV+SClnxsxXSRivZOtXLah6pgPaEL6OIvoFgID4baC+mB2p+n7Uc1S7Qe1TrQp9"
        + "qaz0mqfDdXVqzI6q9R6oejvTapzdbn7R1TWAtxwQqWLgfjhU2RS0JaAmcNl2HVir0Tvx9RyeLu6tebRLycmwkM7VwTqFCYY20GfrJ4e32jBeBiFp83VifQrZ"
        + "w+S6+08Ac82AYZ7dmquBfbUgBhPq7VPmdcMP5dCXm99if/GvX4eo0dPms6+M7xiPgN9+5H9z+XHgfjWRjE5e3+gwfpX+zhFVfunDs1PLf4KjVtrBygsBlv8O"
        + "kk/BhvR9fGRMr152MceCMvSLz3evulGAJrmu7Wk/fEcsl1iclmIuqWSvxrBYPGfaAP8kOstTklLW0QnImhvA/OLQA0P2Z31A7sx+4gg+lE3cAOP9Xdr9guyi"
        + "7G8SqjG6qMCVyQSKSQWNd/KhvVDgSyW6w7WcMi9v+h/koytcPoGuH1iHGwikOPXoI/wo2+/IAFloVrE4eqQHICv7xBbCDJ8L6Q7TQg5mDsv/LEXzZUyUEyBF"
        + "3SialzhEdp94kyJo7pxVgICAgICAgIC4S9xvnUZMdg2FDEjVhgeMOtRxSXFjIUtG9pJJ3Q3ciIk55zt4JnA1h1823fRHJ1hhEtqyinmV28QVZzAVzMGiDD6x"
        + "xVIINZCIpdwNlMwASeBOUI9HOjajkwkh2hx4J2qMJHRr+yUqzqn/M+PdDlvQsymt268M4JLKus6l9eK8MYxiuMD7KU6Wbl/LXzqySjY5Xj1clNDPTV/B7fce"
        + "PV9d2vJS7mh4RigUVVV0V5VimKCAhuKKHsVqZN9g2EJypHIszdv94PGQMC8+Vy18MVtu3PIhlEh/DE56Xf2ljo61F7FbrvyiYPkDZDl86P0vop8dDadaawyo"
        + "PBjgRw2oHFCkPpw6V/D8WVCw2XlcF5UlxUVHxeWcpaHLIOva4tj35IdSAufm1bD9u5WRo2sxmgZH25r0G19pA4oZ+CHSkvMfDqm3GwDXoyuux2Z9mMdfsMk1"
        + "Xp/npPCzkVydCHOGE12l8nYovnJk1keaQiOYY8xzvBHyEjxADmUo0IwM3ZlXatbxCbAwg5p07Bqz0bBICFv+roACkvyld7mZRBzAEUP3s+pXK99uFqB3jC+H"
        + "ITW9M5L3QGvGfDoNhd95nLaGWB5CnchQT+U0ExBETjvTEzREOYy89SPA0AuHNfeXWagP29Lse0DPwuEG3zvPNgjCDedueicP3DKAgMOSgYRcXhxXi+FciJlj"
        + "sHKEUw0aph3T5KOow/jmIkjIfp0c5guTPupvwh6GUGP0ruScOZ8lL60Fb9MnhTMuRZJbdobD3/0u8KkHdN/rT7Kg95N+X3EIx45qUvES/TYcQfdtVs/nBiSR"
        + "WX9su9tSo+mK/UxfVdtbERWufEh2Bj0oP3xSoiwdOGbGvbT5g2wqgHvsfSbrHJWFi2t9eUieq6jtAtl/FlrNo90pY2lTETUG4X0SrKyzJgjT5wC5/w7vH3Hw"
        + "glr4JuSyeLUm4rd6N6ZnjzRfWPXqpBiiGUUuZP0nXPvvfPHkWGJzOAb9PRKWmnYNkLby/OT4/E7WM4nl2ijZUiRjwSpREQmAgICAgICAgP/Z";

    /// <summary>The width of <see cref="Irreversible"/>.</summary>
    internal const int IrreversibleWidth = 610;

    /// <summary>The height of <see cref="Irreversible"/>.</summary>
    internal const int IrreversibleHeight = 999;

    /// <summary>The JP2 file ia-us-reports-341-p1-0.jpx from the corpus, base64.</summary>
    internal const string Irreversible =
        "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";
}
