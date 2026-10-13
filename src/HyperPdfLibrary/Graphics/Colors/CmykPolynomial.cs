// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The polynomial that converts DeviceCMYK to sRGB, unrolled for any lane width. The coefficients come from our own derivation, not from any
/// published colour table: a least-squares fit of a degree-3 polynomial in cyan, magenta, yellow and black (each from 0 to 1) to a modified
/// spectral Neugebauer model with 16 Neugebauer primaries, a Yule-Nielsen n-factor of 2, no mechanical dot gain and process-ink primaries on
/// coated stock written as sRGB (paper 255,255,255; cyan about 0,174,239; magenta about 236,0,140; yellow about 255,242,0; black about
/// 35,31,32; overprints multiply the transmittances of their inks in linear light, with a floor of 0.012 per channel). The model and the fit are
/// in the test project (<c>CmykNeugebauerModel</c> and <c>CmykPolynomialFit</c>), and a test checks that this polynomial matches a regeneration.
/// Each output channel is <c>1 + sum(coefficient * term)</c> over the products of up to three inks, clamped to 0..1 afterwards.
/// </summary>
internal static class CmykPolynomial
{
    /// <summary>The red coefficient of the C term.</summary>
    private const float RedC = -0.7584509F;

    /// <summary>The red coefficient of the M term.</summary>
    private const float RedM = -0.08421534F;

    /// <summary>The red coefficient of the Y term.</summary>
    private const float RedY = -0.010026584F;

    /// <summary>The red coefficient of the K term.</summary>
    private const float RedK = -0.7430594F;

    /// <summary>The red coefficient of the CC term.</summary>
    private const float RedCC = -0.08431969F;

    /// <summary>The red coefficient of the CM term.</summary>
    private const float RedCM = 0.06392772F;

    /// <summary>The red coefficient of the CY term.</summary>
    private const float RedCY = 0.008975666F;

    /// <summary>The red coefficient of the CK term.</summary>
    private const float RedCK = 0.45125532F;

    /// <summary>The red coefficient of the MM term.</summary>
    private const float RedMM = 0.0065086624F;

    /// <summary>The red coefficient of the MY term.</summary>
    private const float RedMY = 0.008975666F;

    /// <summary>The red coefficient of the MK term.</summary>
    private const float RedMK = 0.06309177F;

    /// <summary>The red coefficient of the YY term.</summary>
    private const float RedYY = 0.0070119053F;

    /// <summary>The red coefficient of the YK term.</summary>
    private const float RedYK = 0.008975666F;

    /// <summary>The red coefficient of the KK term.</summary>
    private const float RedKK = -0.07909196F;

    /// <summary>The red coefficient of the CCC term.</summary>
    private const float RedCCC = -0.03960212F;

    /// <summary>The red coefficient of the CCM term.</summary>
    private const float RedCCM = 0.0047121653F;

    /// <summary>The red coefficient of the CCY term.</summary>
    private const float RedCCY = -0.0025614265F;

    /// <summary>The red coefficient of the CCK term.</summary>
    private const float RedCCK = 0.14192054F;

    /// <summary>The red coefficient of the CMM term.</summary>
    private const float RedCMM = -0.0022284924F;

    /// <summary>The red coefficient of the CMY term.</summary>
    private const float RedCMY = -0.0032444736F;

    /// <summary>The red coefficient of the CMK term.</summary>
    private const float RedCMK = -0.05163922F;

    /// <summary>The red coefficient of the CYY term.</summary>
    private const float RedCYY = -0.0025614267F;

    /// <summary>The red coefficient of the CYK term.</summary>
    private const float RedCYK = -0.0032444736F;

    /// <summary>The red coefficient of the CKK term.</summary>
    private const float RedCKK = 0.1331494F;

    /// <summary>The red coefficient of the MMM term.</summary>
    private const float RedMMM = -0.0017977802F;

    /// <summary>The red coefficient of the MMY term.</summary>
    private const float RedMMY = -0.0025614265F;

    /// <summary>The red coefficient of the MMK term.</summary>
    private const float RedMMK = -0.0022469675F;

    /// <summary>The red coefficient of the MYY term.</summary>
    private const float RedMYY = -0.0025614267F;

    /// <summary>The red coefficient of the MYK term.</summary>
    private const float RedMYK = -0.0032444736F;

    /// <summary>The red coefficient of the MKK term.</summary>
    private const float RedMKK = 0.0038989715F;

    /// <summary>The red coefficient of the YYY term.</summary>
    private const float RedYYY = -0.0017929986F;

    /// <summary>The red coefficient of the YYK term.</summary>
    private const float RedYYK = -0.0025614267F;

    /// <summary>The red coefficient of the YKK term.</summary>
    private const float RedYKK = -0.0025614265F;

    /// <summary>The red coefficient of the KKK term.</summary>
    private const float RedKKK = -0.03536628F;

    /// <summary>The green coefficient of the C term.</summary>
    private const float GreenC = -0.3186696F;

    /// <summary>The green coefficient of the M term.</summary>
    private const float GreenM = -0.75216126F;

    /// <summary>The green coefficient of the Y term.</summary>
    private const float GreenY = -0.06231629F;

    /// <summary>The green coefficient of the K term.</summary>
    private const float GreenK = -0.7476789F;

    /// <summary>The green coefficient of the CC term.</summary>
    private const float GreenCC = -0.0012403965F;

    /// <summary>The green coefficient of the CM term.</summary>
    private const float GreenCM = 0.23553713F;

    /// <summary>The green coefficient of the CY term.</summary>
    private const float GreenCY = 0.024090126F;

    /// <summary>The green coefficient of the CK term.</summary>
    private const float GreenCK = 0.23467058F;

    /// <summary>The green coefficient of the MM term.</summary>
    private const float GreenMM = -0.105015956F;

    /// <summary>The green coefficient of the MY term.</summary>
    private const float GreenMY = 0.046803534F;

    /// <summary>The green coefficient of the MK term.</summary>
    private const float GreenMK = 0.4598028F;

    /// <summary>The green coefficient of the YY term.</summary>
    private const float GreenYY = 0.008608629F;

    /// <summary>The green coefficient of the YK term.</summary>
    private const float GreenYK = 0.04661212F;

    /// <summary>The green coefficient of the KK term.</summary>
    private const float GreenKK = -0.100123234F;

    /// <summary>The green coefficient of the CCC term.</summary>
    private const float GreenCCC = -0.002696323F;

    /// <summary>The green coefficient of the CCM term.</summary>
    private const float GreenCCM = 0.003923196F;

    /// <summary>The green coefficient of the CCY term.</summary>
    private const float GreenCCY = -0.0029863224F;

    /// <summary>The green coefficient of the CCK term.</summary>
    private const float GreenCCK = 0.003744059F;

    /// <summary>The green coefficient of the CMM term.</summary>
    private const float GreenCMM = 0.028868435F;

    /// <summary>The green coefficient of the CMY term.</summary>
    private const float GreenCMY = -0.011554593F;

    /// <summary>The green coefficient of the CMK term.</summary>
    private const float GreenCMK = -0.2052804F;

    /// <summary>The green coefficient of the CYY term.</summary>
    private const float GreenCYY = -0.003192198F;

    /// <summary>The green coefficient of the CYK term.</summary>
    private const float GreenCYK = -0.01144545F;

    /// <summary>The green coefficient of the CKK term.</summary>
    private const float GreenCKK = 0.027187325F;

    /// <summary>The green coefficient of the MMM term.</summary>
    private const float GreenMMM = -0.024283076F;

    /// <summary>The green coefficient of the MMY term.</summary>
    private const float GreenMMY = 0.0012538693F;

    /// <summary>The green coefficient of the MMK term.</summary>
    private const float GreenMMK = 0.1416303F;

    /// <summary>The green coefficient of the MYY term.</summary>
    private const float GreenMYY = -0.0030867406F;

    /// <summary>The green coefficient of the MYK term.</summary>
    private const float GreenMYK = -0.032291513F;

    /// <summary>The green coefficient of the MKK term.</summary>
    private const float GreenMKK = 0.13757896F;

    /// <summary>The green coefficient of the YYY term.</summary>
    private const float GreenYYY = -0.0022590496F;

    /// <summary>The green coefficient of the YYK term.</summary>
    private const float GreenYYK = -0.0030902696F;

    /// <summary>The green coefficient of the YKK term.</summary>
    private const float GreenYKK = 0.0010199775F;

    /// <summary>The green coefficient of the KKK term.</summary>
    private const float GreenKKK = -0.02435294F;

    /// <summary>The blue coefficient of the C term.</summary>
    private const float BlueC = -0.07433115F;

    /// <summary>The blue coefficient of the M term.</summary>
    private const float BlueM = -0.43899113F;

    /// <summary>The blue coefficient of the Y term.</summary>
    private const float BlueY = -0.7495216F;

    /// <summary>The blue coefficient of the K term.</summary>
    private const float BlueK = -0.7438656F;

    /// <summary>The blue coefficient of the CC term.</summary>
    private const float BlueCC = 0.009326393F;

    /// <summary>The blue coefficient of the CM term.</summary>
    private const float BlueCM = 0.034402702F;

    /// <summary>The blue coefficient of the CY term.</summary>
    private const float BlueCY = 0.054880362F;

    /// <summary>The blue coefficient of the CK term.</summary>
    private const float BlueCK = 0.054555252F;

    /// <summary>The blue coefficient of the MM term.</summary>
    private const float BlueMM = -0.012304459F;

    /// <summary>The blue coefficient of the MY term.</summary>
    private const float BlueMY = 0.3212752F;

    /// <summary>The blue coefficient of the MK term.</summary>
    private const float BlueMK = 0.32009548F;

    /// <summary>The blue coefficient of the YY term.</summary>
    private const float BlueYY = -0.11491646F;

    /// <summary>The blue coefficient of the YK term.</summary>
    private const float BlueYK = 0.46551874F;

    /// <summary>The blue coefficient of the KK term.</summary>
    private const float BlueKK = -0.10649492F;

    /// <summary>The blue coefficient of the CCC term.</summary>
    private const float BlueCCC = -0.002471203F;

    /// <summary>The blue coefficient of the CCM term.</summary>
    private const float BlueCCM = -0.003452082F;

    /// <summary>The blue coefficient of the CCY term.</summary>
    private const float BlueCCY = -0.0033225187F;

    /// <summary>The blue coefficient of the CCK term.</summary>
    private const float BlueCCK = -0.0033313546F;

    /// <summary>The blue coefficient of the CMM term.</summary>
    private const float BlueCMM = -0.0028507712F;

    /// <summary>The blue coefficient of the CMY term.</summary>
    private const float BlueCMY = -0.017462514F;

    /// <summary>The blue coefficient of the CMK term.</summary>
    private const float BlueCMK = -0.017213749F;

    /// <summary>The blue coefficient of the CYY term.</summary>
    private const float BlueCYY = 0.0018242672F;

    /// <summary>The blue coefficient of the CYK term.</summary>
    private const float BlueCYK = -0.036344845F;

    /// <summary>The blue coefficient of the CKK term.</summary>
    private const float BlueCKK = 0.0014169033F;

    /// <summary>The blue coefficient of the MMM term.</summary>
    private const float BlueMMM = -0.0037101074F;

    /// <summary>The blue coefficient of the MMY term.</summary>
    private const float BlueMMY = 0.012865883F;

    /// <summary>The blue coefficient of the MMK term.</summary>
    private const float BlueMMK = 0.012185957F;

    /// <summary>The blue coefficient of the MYY term.</summary>
    private const float BlueMYY = 0.044071633F;

    /// <summary>The blue coefficient of the MYK term.</summary>
    private const float BlueMYK = -0.28468338F;

    /// <summary>The blue coefficient of the MKK term.</summary>
    private const float BlueMKK = 0.04036382F;

    /// <summary>The blue coefficient of the YYY term.</summary>
    private const float BlueYYY = -0.016733423F;

    /// <summary>The blue coefficient of the YYK term.</summary>
    private const float BlueYYK = 0.13744259F;

    /// <summary>The blue coefficient of the YKK term.</summary>
    private const float BlueYKK = 0.13163991F;

    /// <summary>The blue coefficient of the KKK term.</summary>
    private const float BlueKKK = -0.01772238F;

    /// <summary>The value of every channel when all inks are zero, which makes paper white exact.</summary>
    private const float Constant = 1F;

    /// <summary>Evaluates the polynomial for each lane.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="c">The cyan lanes from 0 to 1.</param>
    /// <param name="m">The magenta lanes from 0 to 1.</param>
    /// <param name="y">The yellow lanes from 0 to 1.</param>
    /// <param name="k">The black lanes from 0 to 1.</param>
    /// <param name="red">Receives the red lanes, unclamped.</param>
    /// <param name="green">Receives the green lanes, unclamped.</param>
    /// <param name="blue">Receives the blue lanes, unclamped.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Evaluate<TLanes, TFloat, TWord>(TFloat c, TFloat m, TFloat y, TFloat k, out TFloat red, out TFloat green, out TFloat blue)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        var paper = TLanes.Splat(Constant);
        var sum = new CmykChannels<TFloat>(paper, paper, paper);
        AddInks<TLanes, TFloat, TWord>(c, m, y, k, ref sum);
        AddPairs<TLanes, TFloat, TWord>(c, m, y, k, ref sum, out var pairs);
        AddTriplesWithCyan<TLanes, TFloat, TWord>(c, m, y, k, in pairs, ref sum);
        AddTriplesWithoutCyan<TLanes, TFloat, TWord>(c, m, y, k, in pairs, ref sum);
        red = sum.Red;
        green = sum.Green;
        blue = sum.Blue;
    }

    /// <summary>Adds the terms of one ink.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="c">The cyan lanes from 0 to 1.</param>
    /// <param name="m">The magenta lanes from 0 to 1.</param>
    /// <param name="y">The yellow lanes from 0 to 1.</param>
    /// <param name="k">The black lanes from 0 to 1.</param>
    /// <param name="sum">The channel sums to add to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddInks<TLanes, TFloat, TWord>(TFloat c, TFloat m, TFloat y, TFloat k, ref CmykChannels<TFloat> sum)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        var (r, g, b) = sum;
        r = TLanes.MultiplyAdd(TLanes.Splat(RedC), c, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenC), c, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueC), c, b);

        r = TLanes.MultiplyAdd(TLanes.Splat(RedM), m, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenM), m, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueM), m, b);

        r = TLanes.MultiplyAdd(TLanes.Splat(RedY), y, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenY), y, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueY), y, b);

        r = TLanes.MultiplyAdd(TLanes.Splat(RedK), k, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenK), k, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueK), k, b);

        sum = new(r, g, b);
    }

    /// <summary>Adds the products of two inks.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="c">The cyan lanes from 0 to 1.</param>
    /// <param name="m">The magenta lanes from 0 to 1.</param>
    /// <param name="y">The yellow lanes from 0 to 1.</param>
    /// <param name="k">The black lanes from 0 to 1.</param>
    /// <param name="sum">The channel sums to add to.</param>
    /// <param name="pairs">Receives the products of two inks.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddPairs<TLanes, TFloat, TWord>(TFloat c, TFloat m, TFloat y, TFloat k, ref CmykChannels<TFloat> sum, out CmykPairs<TFloat> pairs)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        var (r, g, b) = sum;
        var cc = TLanes.Multiply(c, c);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCC), cc, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCC), cc, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCC), cc, b);

        var cm = TLanes.Multiply(c, m);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCM), cm, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCM), cm, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCM), cm, b);

        var cy = TLanes.Multiply(c, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCY), cy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCY), cy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCY), cy, b);

        var ck = TLanes.Multiply(c, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCK), ck, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCK), ck, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCK), ck, b);

        var mm = TLanes.Multiply(m, m);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMM), mm, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMM), mm, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMM), mm, b);

        var my = TLanes.Multiply(m, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMY), my, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMY), my, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMY), my, b);

        var mk = TLanes.Multiply(m, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMK), mk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMK), mk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMK), mk, b);

        var yy = TLanes.Multiply(y, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedYY), yy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenYY), yy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueYY), yy, b);

        var yk = TLanes.Multiply(y, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedYK), yk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenYK), yk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueYK), yk, b);

        var kk = TLanes.Multiply(k, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedKK), kk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenKK), kk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueKK), kk, b);

        sum = new(r, g, b);
        pairs = new(cc, cm, cy, ck, mm, my, mk, yy, yk, kk);
    }

    /// <summary>Adds the products of three inks that include cyan.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="c">The cyan lanes from 0 to 1.</param>
    /// <param name="m">The magenta lanes from 0 to 1.</param>
    /// <param name="y">The yellow lanes from 0 to 1.</param>
    /// <param name="k">The black lanes from 0 to 1.</param>
    /// <param name="pairs">The products of two inks.</param>
    /// <param name="sum">The channel sums to add to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTriplesWithCyan<TLanes, TFloat, TWord>(TFloat c, TFloat m, TFloat y, TFloat k, in CmykPairs<TFloat> pairs, ref CmykChannels<TFloat> sum)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        var (r, g, b) = sum;
        var ccc = TLanes.Multiply(pairs.CC, c);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCCC), ccc, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCCC), ccc, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCCC), ccc, b);

        var ccm = TLanes.Multiply(pairs.CC, m);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCCM), ccm, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCCM), ccm, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCCM), ccm, b);

        var ccy = TLanes.Multiply(pairs.CC, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCCY), ccy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCCY), ccy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCCY), ccy, b);

        var cck = TLanes.Multiply(pairs.CC, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCCK), cck, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCCK), cck, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCCK), cck, b);

        var cmm = TLanes.Multiply(pairs.CM, m);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCMM), cmm, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCMM), cmm, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCMM), cmm, b);

        var cmy = TLanes.Multiply(pairs.CM, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCMY), cmy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCMY), cmy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCMY), cmy, b);

        var cmk = TLanes.Multiply(pairs.CM, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCMK), cmk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCMK), cmk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCMK), cmk, b);

        var cyy = TLanes.Multiply(pairs.CY, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCYY), cyy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCYY), cyy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCYY), cyy, b);

        var cyk = TLanes.Multiply(pairs.CY, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCYK), cyk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCYK), cyk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCYK), cyk, b);

        var ckk = TLanes.Multiply(pairs.CK, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedCKK), ckk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenCKK), ckk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueCKK), ckk, b);

        sum = new(r, g, b);
    }

    /// <summary>Adds the products of three inks that leave out cyan.</summary>
    /// <typeparam name="TLanes">The lane operations.</typeparam>
    /// <typeparam name="TFloat">The float lanes.</typeparam>
    /// <typeparam name="TWord">The pixel lanes.</typeparam>
    /// <param name="c">The cyan lanes from 0 to 1.</param>
    /// <param name="m">The magenta lanes from 0 to 1.</param>
    /// <param name="y">The yellow lanes from 0 to 1.</param>
    /// <param name="k">The black lanes from 0 to 1.</param>
    /// <param name="pairs">The products of two inks.</param>
    /// <param name="sum">The channel sums to add to.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddTriplesWithoutCyan<TLanes, TFloat, TWord>(TFloat c, TFloat m, TFloat y, TFloat k, in CmykPairs<TFloat> pairs, ref CmykChannels<TFloat> sum)
        where TLanes : ICmykLanes<TFloat, TWord>
        where TFloat : unmanaged
        where TWord : unmanaged
    {
        var (r, g, b) = sum;
        var mmm = TLanes.Multiply(pairs.MM, m);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMMM), mmm, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMMM), mmm, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMMM), mmm, b);

        var mmy = TLanes.Multiply(pairs.MM, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMMY), mmy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMMY), mmy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMMY), mmy, b);

        var mmk = TLanes.Multiply(pairs.MM, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMMK), mmk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMMK), mmk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMMK), mmk, b);

        var myy = TLanes.Multiply(pairs.MY, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMYY), myy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMYY), myy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMYY), myy, b);

        var myk = TLanes.Multiply(pairs.MY, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMYK), myk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMYK), myk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMYK), myk, b);

        var mkk = TLanes.Multiply(pairs.MK, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedMKK), mkk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenMKK), mkk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueMKK), mkk, b);

        var yyy = TLanes.Multiply(pairs.YY, y);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedYYY), yyy, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenYYY), yyy, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueYYY), yyy, b);

        var yyk = TLanes.Multiply(pairs.YY, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedYYK), yyk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenYYK), yyk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueYYK), yyk, b);

        var ykk = TLanes.Multiply(pairs.YK, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedYKK), ykk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenYKK), ykk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueYKK), ykk, b);

        var kkk = TLanes.Multiply(pairs.KK, k);
        r = TLanes.MultiplyAdd(TLanes.Splat(RedKKK), kkk, r);
        g = TLanes.MultiplyAdd(TLanes.Splat(GreenKKK), kkk, g);
        b = TLanes.MultiplyAdd(TLanes.Splat(BlueKKK), kkk, b);

        sum = new(r, g, b);
    }
}
