// Reference values: the tables below, printed by
//   python -I tests/reference/special.py normal
//   python -I tests/reference/special.py lognormal
//   python -I tests/reference/special.py weibull
//   python -I tests/reference/special.py dist-gamma
//   python -I tests/reference/special.py dist-chi-squared
//   python -I tests/reference/special.py dist-beta
//   python -I tests/reference/special.py dist-student-t
//   python -I tests/reference/special.py dist-fisher-f
//   python -I tests/reference/special.py dist-regimes
//   python -I tests/reference/special.py dist-edges
// and the closed forms and identities written in each test.
module Ranvier.Science.Tests.ContinuousTests

open System
open Expecto
open Ranvier.Numerics
open Ranvier.Statistics

// python -I tests/reference/special.py normal
// x, N(0, 1) cdf, N(0, 1) survival
let private normalCases: struct (float * float * float)[] =
    [|
        struct (-37.0, 5.725571222524577e-300, 1.0)
        struct (-30.0, 4.906713927148187e-198, 1.0)
        struct (-10.0, 7.619853024160525e-24, 1.0)
        struct (-5.0, 2.866515718791939e-07, 0.9999997133484281)
        struct (-1.96, 0.024997895148220435, 0.9750021048517795)
        struct (-1.0, 0.15865525393145705, 0.8413447460685429)
        struct (-0.5, 0.3085375387259869, 0.6914624612740131)
        struct (0.0, 0.5, 0.5)
        struct (0.3, 0.6179114221889527, 0.3820885778110474)
        struct (1.0, 0.8413447460685429, 0.15865525393145705)
        struct (1.96, 0.9750021048517795, 0.024997895148220435)
        struct (3.0, 0.9986501019683699, 0.0013498980316300946)
        struct (5.0, 0.9999997133484281, 2.866515718791939e-07)
        struct (8.0, 0.9999999999999993, 6.220960574271784e-16)
        struct (10.0, 1.0, 7.619853024160525e-24)
        struct (30.0, 1.0, 4.906713927148187e-198)
        struct (37.0, 1.0, 5.725571222524577e-300)
    |]

// x, N(3, 2) cdf
let private normalShiftedCases: struct (float * float)[] =
    [|
        struct (-5.0, 3.1671241833119924e-05)
        struct (1.0, 0.15865525393145705)
        struct (3.0, 0.5)
        struct (7.5, 0.9877755273449553)
    |]

// p, N(0, 1) quantile
let private normalQuantileCases: struct (float * float)[] =
    [|
        struct (1e-300, -37.0470962993612)
        struct (1e-100, -21.273453560965326)
        struct (1e-10, -6.361340902404057)
        struct (0.025, -1.9599639845400543)
        struct (0.3, -0.5244005127080408)
        struct (0.975, 1.9599639845400538)
        struct (0.9999999999, 6.361340889697422)
    |]

// python -I tests/reference/special.py lognormal
// x, LN(0.5, 0.8) cdf, LN(0.5, 0.8) survival
let private logNormalCases: struct (float * float * float)[] =
    [|
        struct (1e-10, 2.2139633536948147e-190, 1.0)
        struct (0.1, 0.00022982501310415206, 0.9997701749868958)
        struct (1.0, 0.26598552904870054, 0.7340144709512995)
        struct (2.0, 0.595390608679215, 0.40460939132078505)
        struct (10.0, 0.9878777176649812, 0.012122282335018802)
        struct (1000.0, 0.9999999999999994, 5.749705686405583e-16)
    |]

// python -I tests/reference/special.py weibull
// k, scale, variance
let private weibullVarianceCases: struct (float * float * float)[] =
    [|
        struct (10.0, 1.0, 0.01310045507346831)
        struct (10.0, 1.7, 0.03786031516232341)
        struct (100.0, 1.0, 0.00016030491620026111)
        struct (100.0, 1.7, 0.0004632812078187546)
        struct (10000.0, 1.0, 1.6445038762822376e-08)
        struct (10000.0, 1.7, 4.7526162024556667e-08)
        struct (100000000.0, 1.0, 1.6449340238174553e-16)
        struct (100000000.0, 1.7, 4.753859328832446e-16)
    |]

// k, scale, x, survival
let private weibullSurvivalCases: struct (float * float * float * float)[] =
    [|
        struct (20.0, 1.7, 1.2, 0.9990571520175738)
        struct (20.0, 1.7, 1.6, 0.7427060286258715)
        struct (20.0, 1.7, 1.7, 0.36787944117144233)
        struct (20.0, 1.7, 1.8, 0.0434262777646477)
        struct (20.0, 1.7, 2.0, 6.239595687369753e-12)
        struct (20.0, 1.7, 2.2, 4.163087892779429e-76)
        struct (1000.0, 1.7, 1.69, 0.9972635447961798)
        struct (1000.0, 1.7, 1.7, 0.36787944117144233)
        struct (1000.0, 1.7, 1.7017, 0.06607770161458344)
        struct (1000.0, 1.7, 1.705, 6.467103253477042e-09)
    |]

let private weibullVarianceBelowTenCases: struct (float * float * float)[] =
    [|
        struct (5.0, 1.0, 0.044229977983117334)
        struct (5.0, 1.7, 0.12782463637120908)
        struct (9.9, 1.0, 0.013339995684942823)
        struct (9.9, 1.7, 0.038552587529484754)
    |]

// python -I tests/reference/special.py dist-gamma
// shape, scale, x, cdf, survival, density
let private gammaCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (2.5, 2.0, 1e-05, 1.6820823405667358e-14, 0.9999999999999831, 4.2051998439818155e-09)
        struct (2.5, 2.0, 0.1, 0.00016231661192261503, 0.9998376833880774, 0.0040001298281004535)
        struct (2.5, 2.0, 1.0, 0.03743422675270363, 0.9625657732472964, 0.08065690817304778)
        struct (2.5, 2.0, 5.0, 0.5841198130044921, 0.41588018699550794, 0.1220415213493874)
        struct (2.5, 2.0, 20.0, 0.9987502694369687, 0.0012497305630313753, 0.0005399940637392745)
        struct (2.5, 2.0, 100.0, 1.0, 5.28514836094324e-20, 2.56486620890214e-20)
        struct (0.5, 1.0, 1e-10, 1.1283791670579e-05, 0.9999887162083294, 56418.95834913373)
        struct (0.5, 1.0, 0.01, 0.1124629160182849, 0.8875370839817152, 5.585758033944685)
        struct (0.5, 1.0, 0.5, 0.6826894921370859, 0.3173105078629141, 0.4839414490382867)
        struct (0.5, 1.0, 3.0, 0.9856941215645704, 0.01430587843542964, 0.016217391109880488)
        struct (0.5, 1.0, 50.0, 1.0, 1.523970604832105e-23, 1.538919725341284e-23)
        struct (100.0, 1.0, 60.0, 1.4815276326460468e-06, 0.9999985184723673, 1.0216478351295635e-06)
        struct (100.0, 1.0, 90.0, 0.15822098918643016, 0.8417790108135699, 0.025912028250157558)
        struct (100.0, 1.0, 100.0, 0.5132987982791487, 0.48670120172085135, 0.039860996809147134)
        struct (100.0, 1.0, 110.0, 0.8417213299399129, 0.15827867006008708, 0.022671443180423363)
        struct (100.0, 1.0, 150.0, 0.9999940754596646, 5.924540335483916e-06, 2.0838128235826387e-06)
    |]

// shape, scale, p, quantile
let private gammaQuantileCases: struct (float * float * float * float)[] =
    [|
        struct (2.5, 2.0, 0.05, 1.1454762260617692)
        struct (2.5, 2.0, 0.975, 12.832501994030025)
        struct (0.5, 1.0, 0.05, 0.0019660700000097616)
        struct (0.5, 1.0, 0.975, 2.5119430936574436)
        struct (100.0, 1.0, 0.05, 84.13927721831419)
        struct (100.0, 1.0, 0.975, 120.52894775315546)
    |]

// python -I tests/reference/special.py dist-chi-squared
// dof, x, cdf, survival, density
let private chiSquaredCases: struct (float * float * float * float * float)[] =
    [|
        struct (3.0, 0.01, 0.0002651650586556098, 0.9997348349413444, 0.03969525474770118)
        struct (3.0, 1.0, 0.1987480430987992, 0.8012519569012008, 0.24197072451914334)
        struct (3.0, 3.0, 0.608374823728911, 0.39162517627108895, 0.15418032980376928)
        struct (3.0, 10.0, 0.9814338645369568, 0.018566135463043233, 0.008500366602520341)
        struct (3.0, 50.0, 0.9999999999201082, 7.989179244951472e-11, 3.917716632754334e-11)
        struct (10.0, 1.0, 0.00017211562995584078, 0.9998278843700441, 0.0007897534631674915)
        struct (10.0, 10.0, 0.5595067149347875, 0.4404932850652124, 0.08773368488392536)
        struct (10.0, 30.0, 0.9991433587892247, 0.0008566412107753004, 0.00032263135365426937)
        struct (10.0, 100.0, 1.0, 5.4497019829205295e-17, 2.511393031203018e-17)
    |]

// dof, p, quantile
let private chiSquaredQuantileCases: struct (float * float * float)[] =
    [|
        struct (3.0, 0.05, 0.3518463177492714)
        struct (3.0, 0.975, 9.348403604496147)
        struct (10.0, 0.05, 3.94029913611906)
        struct (10.0, 0.975, 20.483177350807395)
    |]

// python -I tests/reference/special.py dist-beta
// a, b, x, cdf, survival, density
let private betaCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (2.5, 3.5, 1e-06, 1.0864958046672595e-14, 0.9999999999999891, 2.716237571495868e-08)
        struct (2.5, 3.5, 0.1, 0.028575668042355413, 0.9714243319576445, 0.6600473799907084)
        struct (2.5, 3.5, 0.4, 0.4869041915261174, 0.5130958084738826, 1.9161828587118823)
        struct (2.5, 3.5, 0.8, 0.9784400592936358, 0.021559940706364265, 0.3476792783490149)
        struct (2.5, 3.5, 0.999, 0.9999999997548714, 2.451285660157063e-10, 8.576637809140345e-07)
    |]

// a, b, p, quantile
let private betaQuantileCases: struct (float * float * float * float)[] =
    [|
        struct (2.5, 3.5, 0.05, 0.12777559948359626)
        struct (2.5, 3.5, 0.975, 0.7905833359239951)
    |]

// python -I tests/reference/special.py dist-student-t
// dof, t, cdf, survival, density
let private studentTCases: struct (float * float * float * float * float)[] =
    [|
        struct (2.5, -1000.0, 2.2747463948307453e-08, 0.9999999772525361, 5.686854929307642e-11)
        struct (2.5, -10.0, 0.002220747883653712, 0.9977792521163463, 0.0005446371797667946)
        struct (2.5, -2.5, 0.05228498153907769, 0.9477150184609223, 0.04039800855886638)
        struct (2.5, -1.0, 0.20203051363913674, 0.7979694863608633, 0.20079596067092012)
        struct (2.5, 0.4, 0.6395903655346385, 0.3604096344653615, 0.3245870475278445)
        struct (2.5, 1.96, 0.918526217763593, 0.081473782236407, 0.07096203649988234)
        struct (2.5, 4.0, 0.9804935120793409, 0.019506487920659125, 0.010897415871881472)
        struct (2.5, 100.0, 0.9999928083507883, 7.191649211666188e-06, 1.797562790260566e-07)
        struct (5.0, -1000.0, 9.490065565989857e-15, 0.9999999999999906, 4.745012447238595e-17)
        struct (5.0, -10.0, 8.54737878714818e-05, 0.9999145262121285, 4.098981641534331e-05)
        struct (5.0, -2.5, 0.02724504967118812, 0.9727549503288119, 0.03332623888702283)
        struct (5.0, -1.0, 0.1816087338245613, 0.8183912661754387, 0.21967979735098056)
        struct (5.0, 0.4, 0.6471634425834427, 0.35283655741655723, 0.3453780757527334)
        struct (5.0, 1.96, 0.946356023747353, 0.05364397625264709, 0.06865169758273229)
        struct (5.0, 4.0, 0.9948382922595843, 0.005161707740415727, 0.005123727051917914)
        struct (5.0, 100.0, 0.9999999990519993, 9.480007112311813e-10, 4.7379731090455336e-11)
        struct (30.0, -1000.0, 1.0360017415558665e-69, 1.0, 3.1079149009704875e-71)
        struct (30.0, -10.0, 2.2876257041148064e-11, 0.9999999999771237, 5.327814578274168e-11)
        struct (30.0, -2.5, 0.009057824534033348, 0.9909421754659666, 0.021057019220621632)
        struct (30.0, -1.0, 0.16265430771301495, 0.8373456922869851, 0.23799334232287983)
        struct (30.0, 0.4, 0.654004741742904, 0.345995258257096, 0.3643219485288568)
        struct (30.0, 1.96, 0.9703288435519748, 0.02967115644802524, 0.061119852876620834)
        struct (30.0, 4.0, 0.9998090771819581, 0.00019092281804187843, 0.0005247164401974079)
        struct (30.0, 100.0, 1.0, 9.923058983635125e-40, 2.968291857858848e-40)
    |]

// dof, p, quantile
let private studentTQuantileCases: struct (float * float * float)[] =
    [|
        struct (2.5, 0.05, -2.5582186141359364)
        struct (2.5, 0.975, 3.5746548420036817)
        struct (5.0, 0.05, -2.0150483733330242)
        struct (5.0, 0.975, 2.5705818356363146)
        struct (30.0, 0.05, -1.6972608865939578)
        struct (30.0, 0.975, 2.0422724563012378)
    |]

// python -I tests/reference/special.py dist-fisher-f
// d1, d2, x, cdf, survival, density
let private fisherFCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (5.0, 10.0, 0.01, 4.038069889266104e-05, 0.9999596193011073, 0.009987686888975316)
        struct (5.0, 10.0, 0.5, 0.22997511934989837, 0.7700248806501017, 0.6876070027706234)
        struct (5.0, 10.0, 1.0, 0.5348805734621996, 0.4651194265378004, 0.49547978348663874)
        struct (5.0, 10.0, 3.0, 0.9344424379061559, 0.06555756209384411, 0.05582673114557219)
        struct (5.0, 10.0, 20.0, 0.9999352765038925, 6.472349610750509e-05, 1.4348623393615609e-05)
        struct (5.0, 10.0, 1000.0, 0.9999999999996293, 3.707168098708697e-13, 1.8489596689062653e-15)
        struct (2.0, 30.0, 0.1, 0.09486225349366048, 0.9051377465063395, 0.899143456794377)
        struct (2.0, 30.0, 1.0, 0.6201875941847543, 0.37981240581524567, 0.3560741304517928)
        struct (2.0, 30.0, 5.0, 0.9866365389898419, 0.013363461010158062, 0.010022595757618546)
        struct (2.0, 30.0, 50.0, 0.9999999997196707, 2.803293281617642e-10, 6.469138342194558e-11)
    |]

// d1, d2, p, quantile
let private fisherFQuantileCases: struct (float * float * float * float)[] =
    [|
        struct (5.0, 10.0, 0.05, 0.21119042878234492)
        struct (5.0, 10.0, 0.975, 4.2360856681886325)
        struct (2.0, 30.0, 0.05, 0.05138109450638156)
        struct (2.0, 30.0, 0.975, 4.182060590996112)
    |]

// python -I tests/reference/special.py dist-regimes
// dof, t, cdf, survival, density: t(100), where 1/2 - I_y(1/2, 50)/2 would cancel, and either side of |t| = 1
let private studentTRegimeCases: struct (float * float * float * float * float)[] =
    [|
        struct (100.0, -9.0, 7.680385257375208e-15, 0.9999999999999923, 3.8641708215471804e-14)
        struct (100.0, -5.0, 1.2250867067519001e-06, 0.9999987749132933, 5.08005823472728e-06)
        struct (100.0, -1.01, 0.1574664562228952, 0.8425335437771048, 0.23835855634544006)
        struct (100.0, -0.99, 0.1622817757498058, 0.8377182242501943, 0.243173713838713)
        struct (100.0, 0.99, 0.8377182242501943, 0.1622817757498058, 0.243173713838713)
        struct (100.0, 1.01, 0.8425335437771048, 0.1574664562228952, 0.23835855634544006)
    |]

// a, b, x, cdf, survival, density: a shape below 1 at small x, where 1 - x rounds near 1
let private betaRegimeCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (0.5, 100.0, 1e-20, 1.1269695801851285e-09, 0.9999999988730304, 56348479009.256424)
        struct (0.5, 100.0, 1e-10, 0.00011269695764661288, 0.9998873030423534, 563484.7845140648)
        struct (0.5, 100.0, 1e-06, 0.01126932391282343, 0.9887306760871766, 5634.2900783172145)
    |]

// d1, d2, x, cdf, survival, density: far above d2/d1 at d2 = 0.5 and far below it at d1 = 0.5, where w or 1 - w
// rounds near 1
let private fisherFRegimeCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (100.0, 0.5, 1000.0, 0.8615393241649868, 0.13846067583501323, 3.460821195063689e-05)
        struct (100.0, 0.5, 1e+20, 0.9999922133927572, 7.786607242745579e-06, 1.946651810686395e-26)
        struct (0.5, 100.0, 1e-20, 7.786607242745579e-06, 0.9999922133927572, 194665181068639.5)
        struct (0.5, 100.0, 0.001, 0.13846067583501323, 0.8615393241649868, 34.608211950636885)
    |]

// python -I tests/reference/special.py dist-edges
// shape, scale, x, cdf, survival, density: the ends of the gamma's measured domain
let private gammaEdgeCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (0.01, 1.0, 1e-100, 0.1005706528500385, 0.8994293471499615, 1.005706528500385e+97)
        struct (0.01, 1.0, 1e-10, 0.7988610914336052, 0.2011389085663948, 79886109.135451)
        struct (0.01, 1.0, 0.01, 0.9603474235215091, 0.039652576478490804, 0.9508857180865052)
        struct (0.01, 1.0, 1.0, 0.997783765376772, 0.00221623462322799, 0.0036997875568719287)
        struct (0.01, 1.0, 10.0, 0.9999999571829502, 4.281704977409939e-08, 4.672254062420182e-08)
        struct (0.01, 1.0, 100.0, 1.0, 3.8795920588023443e-48, 3.9176269537213123e-48)
        struct (1000.0, 1.0, 700.0, 1.0158583345333217e-26, 1.0, 4.386927123038404e-27)
        struct (1000.0, 1.0, 900.0, 0.0005499022657117829, 0.9994500977342882, 6.585503650465444e-05)
        struct (1000.0, 1.0, 1000.0, 0.5042052441802155, 0.4957947558197845, 0.012614611348721499)
        struct (1000.0, 1.0, 1100.0, 0.99894067674607, 0.0010593232539299773, 0.00010537028564234359)
        struct (1000.0, 1.0, 1400.0, 1.0, 8.04132540818483e-30, 2.3173346521091296e-30)
    |]

// dof, x, cdf, survival, density: dof 0.05 and 1e5, the ends of the measured domain, and 0.5 and 100
let private chiSquaredEdgeCases: struct (float * float * float * float * float)[] =
    [|
        struct (0.05, 1e-100, 0.0031515247196459762, 0.996848475280354, 7.878811799114941e+95)
        struct (0.05, 1e-10, 0.5604291519168295, 0.4395708480831705, 140107287.9723729)
        struct (0.05, 0.01, 0.8881121650944745, 0.11188783490552544, 2.2094758256625777)
        struct (0.05, 1.0, 0.9858272525307303, 0.014172747469269683, 0.015111705649574165)
        struct (0.05, 20.0, 0.9999998881372488, 1.118627511446815e-07, 6.095532649505505e-08)
        struct (0.05, 100.0, 1.0, 1.058123705841213e-25, 5.391835471009449e-26)
        struct (0.5, 1e-10, 0.0029337386158567917, 0.9970662613841432, 7334346.539348605)
        struct (0.5, 0.1, 0.5165553208304656, 0.4834446791695344, 1.2406426356271536)
        struct (0.5, 1.0, 0.8464864041916775, 0.15351359580832247, 0.14067411288159115)
        struct (0.5, 10.0, 0.9995079748755537, 0.00049202512444634, 0.00027790030058575597)
        struct (0.5, 100.0, 1.0, 2.788194848158256e-24, 1.4146119773390615e-24)
        struct (100.0, 50.0, 6.953305247616099e-06, 0.9999930466947524, 3.6021642652022015e-06)
        struct (100.0, 80.0, 0.07033506665939496, 0.9296649333406051, 0.011066885970397655)
        struct (100.0, 100.0, 0.5188083154720433, 0.48119168452795674, 0.02816250316259541)
        struct (100.0, 130.0, 0.9764876021901914, 0.023512397809808676, 0.0032997291091396677)
        struct (100.0, 200.0, 0.9999999882154993, 1.1784500720979422e-08, 3.0578554087971835e-09)
        struct (100000.0, 98000.0, 3.3847542280794912e-06, 0.9999966152457719, 3.6093981921096624e-08)
        struct (100000.0, 99500.0, 0.13169564297235348, 0.8683043570276465, 0.00047888256480812803)
        struct (100000.0, 100000.0, 0.5005947081047933, 0.4994052918952067, 0.0008920605713075277)
        struct (100000.0, 100500.0, 0.8681451883966163, 0.13185481160338378, 0.00047609720509648844)
        struct (100000.0, 102000.0, 0.9999955880607448, 4.4119392551203065e-06, 4.527936889649121e-08)
    |]

// a, b, x, cdf, survival, density: the corners of shapes 0.5 to 100 and of the measured 0.2 to 500
let private betaEdgeCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (0.5, 0.5, 1e-100, 6.366197723675814e-51, 1.0, 3.1830988618379065e+49)
        struct (0.5, 0.5, 1e-10, 6.366197723781917e-06, 0.9999936338022762, 31830.988619970616)
        struct (0.5, 0.5, 0.3, 0.36901011956554536, 0.6309898804344546, 0.6946091180428566)
        struct (0.5, 0.5, 0.9, 0.7951672353008665, 0.20483276469913342, 1.061032953945969)
        struct (0.5, 0.5, 0.9999999999, 0.9999936338020129, 6.366197987152692e-06, 31830.987303116795)
        struct (0.5, 100.0, 1e-100, 1.1269695801851285e-49, 1.0, 5.634847900925642e+50)
        struct (0.5, 100.0, 1e-10, 0.00011269695764661288, 0.9998873030423534, 563484.7845140648)
        struct (0.5, 100.0, 0.005, 0.6826910120299894, 0.31730898797001067, 48.515687440268124)
        struct (0.5, 100.0, 0.05, 0.9986206140626394, 0.001379385937360692, 0.1570486180735802)
        struct (0.5, 100.0, 0.3, 1.0, 3.290369956902061e-17, 4.753653605178042e-15)
        struct (100.0, 0.5, 0.7, 3.290369956902035e-17, 1.0, 4.7536536051780045e-15)
        struct (100.0, 0.5, 0.95, 0.0013793859373606855, 0.9986206140626394, 0.15704861807357945)
        struct (100.0, 0.5, 0.995, 0.31730898797001045, 0.6826910120299896, 48.51568744026808)
        struct (100.0, 0.5, 0.9999999999, 0.9998873030376911, 0.0001126969623089068, 563484.7612025958)
        struct (100.0, 100.0, 0.3, 1.8411141115800529e-09, 0.9999999981588858, 3.5938629673509754e-07)
        struct (100.0, 100.0, 0.45, 0.07838793271222053, 0.9216120672877794, 4.166740545241124)
        struct (100.0, 100.0, 0.5, 0.5, 0.5, 11.269695801851285)
        struct (100.0, 100.0, 0.55, 0.9216120672877797, 0.0783879327122203, 4.166740545241115)
        struct (100.0, 100.0, 0.7, 0.9999999981588858, 1.841114111580073e-09, 3.593862967351013e-07)
        struct (0.2, 0.2, 1e-100, 5.262326231224007e-21, 1.0, 1.0524652462448015e+79)
        struct (0.2, 0.2, 1e-10, 0.005262326231294184, 0.9947376737687058, 10524652.463290012)
        struct (0.2, 0.2, 0.5, 0.5, 0.5, 0.31904780188193904)
        struct (0.2, 0.2, 0.9999999999, 0.9947376736816245, 0.005262326318375546, 10524651.766639145)
        struct (0.2, 500.0, 1e-100, 3.7740008708825995e-20, 1.0, 7.5480017417652e+79)
        struct (0.2, 500.0, 1e-10, 0.03774000839495501, 0.9622599916050449, 75480013.65119939)
        struct (0.2, 500.0, 0.0001, 0.593231108341127, 0.40676889165887303, 1138.0454674212083)
        struct (0.2, 500.0, 0.01, 0.999652689958298, 0.0003473100417019499, 0.19943165618946582)
        struct (0.2, 500.0, 0.05, 0.9999999999998828, 1.172053330598706e-13, 6.349434206909282e-11)
        struct (500.0, 0.2, 0.95, 1.1720533305986797e-13, 0.9999999999998828, 6.349434206909139e-11)
        struct (500.0, 0.2, 0.99, 0.0003473100417019482, 0.999652689958298, 0.19943165618946482)
        struct (500.0, 0.2, 0.9999, 0.4067688916588856, 0.5932311083411145, 1138.0454674213147)
        struct (500.0, 0.2, 0.9999999999, 0.9622599909805206, 0.03774000901947943, 75480008.655004)
        struct (500.0, 500.0, 0.45, 0.0007636789395265001, 0.9992363210604736, 0.16741470119749582)
        struct (500.0, 500.0, 0.48, 0.10291752730699572, 0.8970824726930043, 11.345224253570203)
        struct (500.0, 500.0, 0.5, 0.5, 0.5, 25.225018178360802)
        struct (500.0, 500.0, 0.52, 0.8970824726930043, 0.10291752730699572, 11.345224253570203)
        struct (500.0, 500.0, 0.55, 0.9992363210604736, 0.0007636789395264908, 0.16741470119749396)
    |]

// dof, t, cdf, survival, density: dof 0.05 and 1000, the ends of the measured domain, and 0.5 and 100
let private studentTEdgeCases: struct (float * float * float * float * float)[] =
    [|
        struct (0.05, -1e+100, 4.485631048063485e-06, 0.999995514368952, 2.2428155240317428e-107)
        struct (0.05, -10000000000.0, 0.1418481085504884, 0.8581518914495115, 7.092405427524421e-13)
        struct (0.05, -10.0, 0.39977972888292707, 0.6002202711170729, 0.0019983869255644176)
        struct (0.05, -1.0, 0.4482813060155917, 0.5517186939844083, 0.021860957074032412)
        struct (0.05, 0.5, 0.53672496695746, 0.4632750330425401, 0.04219926577642753)
        struct (0.05, 100.0, 0.6436936836089822, 0.35630631639101784, 0.00017815270195134769)
        struct (0.5, -1e+100, 3.207009754142229e-51, 1.0, 1.6035048770711145e-151)
        struct (0.5, -10000000000.0, 3.207009754142229e-06, 0.9999967929902459, 1.6035048770711146e-16)
        struct (0.5, -2.0, 0.22275744509156561, 0.7772425549084344, 0.051899228247372614)
        struct (0.5, -0.5, 0.3786590364647183, 0.6213409635352817, 0.19896392402617255)
        struct (0.5, 1.5, 0.7460491046759458, 0.25395089532405424, 0.07508803940917724)
        struct (0.5, 100000.0, 0.998985854469861, 0.0010141455301389997, 5.0707276505428766e-09)
        struct (100.0, -30.0, 4.190166279344146e-52, 1.0, 1.2584163368965668e-51)
        struct (100.0, -3.0, 0.0017039576716647248, 0.9982960423283352, 0.005126089702320253)
        struct (100.0, -0.5, 0.3090867829154433, 0.6909132170845567, 0.3508028333923303)
        struct (100.0, 1.0, 0.8401379221079384, 0.1598620778920617, 0.24076589692854597)
        struct (100.0, 4.0, 0.9999392381778496, 6.076182215038084e-05, 0.00022115455654062906)
        struct (100.0, 20.0, 1.0, 4.997133930668478e-37, 2.0037296688112087e-36)
        struct (1000.0, -30.0, 7.687343722021741e-142, 1.0, 1.2151341190135477e-140)
        struct (1000.0, -5.0, 3.3836281823243153e-07, 0.9999996616371818, 1.7120122333122448e-06)
        struct (1000.0, -1.2, 0.11521177616223353, 0.8847882238377665, 0.194098384399826)
        struct (1000.0, 0.3, 0.617880247916378, 0.38211975208362203, 0.38127609547338126)
        struct (1000.0, 2.0, 0.9771148267533741, 0.02288517324662582, 0.05408517400975593)
        struct (1000.0, 10.0, 1.0, 8.335351479300033e-23, 7.651781684387526e-22)
    |]

// d1, d2, x, cdf, survival, density: the corners of d1, d2 from 0.5 to 100, and one of them at 1000 with the other
// at 0.5 or 100, the ends of the measured domain; a value below 1e-300 prints as 0.0 and is skipped
let private fisherFEdgeCases: struct (float * float * float * float * float * float)[] =
    [|
        struct (0.5, 0.5, 1e-10, 0.0017055826816746616, 0.9982944173183254, 4263956.704016095)
        struct (0.5, 0.5, 0.001, 0.09590237534219626, 0.9040976246578037, 23.966010625047605)
        struct (0.5, 0.5, 0.3, 0.38847117333342446, 0.6115288266665755, 0.29174281497296606)
        struct (0.5, 0.5, 3.0, 0.6022432216826442, 0.39775677831735584, 0.029576164260098153)
        struct (0.5, 0.5, 1000.0, 0.9040976246578037, 0.09590237534219626, 2.3966010625047605e-05)
        struct (0.5, 0.5, 10000000000.0, 0.9982944173183254, 0.0017055826816746616, 4.263956704016096e-14)
        struct (0.5, 100.0, 1e-10, 0.0024623414132115915, 0.9975376585867884, 6155853.532905246)
        struct (0.5, 100.0, 0.001, 0.13846067583501323, 0.8615393241649868, 34.608211950636885)
        struct (0.5, 100.0, 0.3, 0.5677682967149376, 0.4322317032850624, 0.4453863646608471)
        struct (0.5, 100.0, 3.0, 0.8981163587807167, 0.10188364121928323, 0.0404137093730966)
        struct (0.5, 100.0, 1000.0, 1.0, 2.0707539746844117e-41, 8.653423253027115e-43)
        struct (0.5, 100.0, 10000000000.0, 1.0, 0.0, 0.0)
        struct (100.0, 0.5, 1e-10, 0.0, 1.0, 0.0)
        struct (100.0, 0.5, 0.001, 2.0707539746844134e-41, 1.0, 8.653423253027123e-37)
        struct (100.0, 0.5, 0.3, 0.08945249228123675, 0.9105475077187632, 0.3820875019199355)
        struct (100.0, 0.5, 3.0, 0.41802551093829343, 0.5819744890617066, 0.045346609736022704)
        struct (100.0, 0.5, 1000.0, 0.8615393241649868, 0.13846067583501323, 3.460821195063689e-05)
        struct (100.0, 0.5, 10000000000.0, 0.9975376585867884, 0.0024623414132115915, 6.155853532905246e-14)
        struct (100.0, 100.0, 1e-10, 0.0, 1.0, 0.0)
        struct (100.0, 100.0, 0.001, 4.5737001791722887e-122, 1.0, 2.2823706321801054e-117)
        struct (100.0, 100.0, 0.3, 2.651678634111673e-09, 0.9999999973483213, 2.4344461311567296e-07)
        struct (100.0, 100.0, 3.0, 0.9999999561515296, 4.38484704593346e-08, 3.756092395905319e-07)
        struct (100.0, 100.0, 1000.0, 1.0, 4.573700179172284e-122, 2.282370632180103e-123)
        struct (100.0, 100.0, 10000000000.0, 1.0, 0.0, 0.0)
        struct (1000.0, 0.5, 1e-10, 0.0, 1.0, 0.0)
        struct (1000.0, 0.5, 0.001, 5.336151517825311e-91, 1.0, 8.920108577664774e-86)
        struct (1000.0, 0.5, 0.3, 0.08781854337710908, 0.912181456622891, 0.38179417665248916)
        struct (1000.0, 0.5, 3.0, 0.41700226858652406, 0.582997731413476, 0.04543752107361937)
        struct (1000.0, 0.5, 1000.0, 0.8613050175117827, 0.1386949824882173, 3.466680802449251e-05)
        struct (1000.0, 0.5, 10000000000.0, 0.9975334923052093, 0.002466507694790683, 6.16626923685332e-14)
        struct (1000.0, 100.0, 1e-10, 0.0, 1.0, 0.0)
        struct (1000.0, 100.0, 0.001, 0.0, 1.0, 0.0)
        struct (1000.0, 100.0, 0.3, 4.7301489748916295e-22, 1.0, 1.3978354248921894e-19)
        struct (1000.0, 100.0, 3.0, 0.999999999901986, 9.801403084967357e-11, 1.0703348444501304e-09)
        struct (1000.0, 100.0, 1000.0, 1.0, 2.968648066041796e-129, 1.4827234816184902e-130)
        struct (1000.0, 100.0, 10000000000.0, 1.0, 0.0, 0.0)
        struct (0.5, 1000.0, 1e-10, 0.002466507694790683, 0.9975334923052093, 6166269.23685332)
        struct (0.5, 1000.0, 0.001, 0.1386949824882173, 0.8613050175117827, 34.666808024492504)
        struct (0.5, 1000.0, 0.3, 0.5687632415787973, 0.4312367584212027, 0.44626786382444456)
        struct (0.5, 1000.0, 3.0, 0.8997546308549875, 0.1002453691450125, 0.04041494472006835)
        struct (0.5, 1000.0, 1000.0, 1.0, 5.336151517825292e-91, 8.920108577664743e-92)
        struct (0.5, 1000.0, 10000000000.0, 1.0, 0.0, 0.0)
        struct (100.0, 1000.0, 1e-10, 0.0, 1.0, 0.0)
        struct (100.0, 1000.0, 0.001, 2.968648066041799e-129, 1.0, 1.4827234816184917e-124)
        struct (100.0, 1000.0, 0.3, 2.8430243087805377e-12, 0.9999999999971569, 3.261467544634024e-10)
        struct (100.0, 1000.0, 3.0, 1.0, 3.084409024716647e-18, 8.036054625640496e-17)
        struct (100.0, 1000.0, 1000.0, 1.0, 0.0, 0.0)
        struct (100.0, 1000.0, 10000000000.0, 1.0, 0.0, 0.0)
    |]

let private rel (a: float) (b: float) : float = abs (a - b) / abs b

let private lowerGrid = [ 1e-100; 1e-10; 0.01; 0.3; 0.5 ]
let private upperGrid = [ 0.6; 0.975; 0.999; 1.0 - 1e-10 ]

/// A reference value below 1e-300 is outside the stated bound and is skipped.
let private matches (name: string) (actual: float) (reference: float) =
    if reference >= 1e-300 then
        Expect.isLessThanOrEqual
            (rel actual reference)
            1e-12
            (name + ": " + string actual + " against " + string reference)

/// matches with a stated bound in place of 1e-12.
let private matchesWithin (bound: float) (name: string) (actual: float) (reference: float) =
    if reference >= 1e-300 then
        Expect.isLessThanOrEqual (rel actual reference) bound (name + ": " + string actual + " against " + string reference)

/// actual within relative bound of expected, or equal to it, as at 0 and at the infinities.
let private closeTo (bound: float) (name: string) (actual: float) (expected: float) =
    if actual <> expected then
        Expect.isLessThanOrEqual (rel actual expected) bound (name + ": " + string actual + " against " + string expected)

let private roundTrip (d: Continuous) (p: float) =
    let q = Continuous.quantile d p

    if p <= 0.5 then
        Expect.isLessThanOrEqual
            (rel (Continuous.cdf d q) p)
            1e-12
            (Continuous.describe d + " cdf at the quantile of " + string p)
    else
        Expect.isLessThanOrEqual
            (rel (Continuous.survival d q) (1.0 - p))
            1e-12
            (Continuous.describe d + " survival at the quantile of " + string p)

/// The Kolmogorov-Smirnov statistic of the sample against a distribution function.
let private ksDistance (cdf: float -> float) (xs: float[]) : float =
    let sorted = Array.sort xs
    let n = float sorted.Length
    let mutable d = 0.0

    for i in 0 .. sorted.Length - 1 do
        let f = cdf sorted[i]
        d <- max d (max (float (i + 1) / n - f) (f - float i / n))

    d

let private families: Continuous list =
    [
        Continuous.normal 0.0 1.0
        Continuous.normal 3.0 2.0
        Continuous.logNormal 0.5 0.8
        Continuous.uniform 0.0 4.0
        Continuous.exponential 2.0
        Continuous.cauchy 0.0 1.0
        Continuous.weibull 1.5 2.0
        Continuous.logistic 1.0 2.0
    ]

/// The families built on the regularised incomplete gamma and beta functions.
let private incompleteFamilies: Continuous list =
    [
        Continuous.gamma 2.5 2.0
        Continuous.chiSquared 3.0
        Continuous.beta 2.5 3.5
        Continuous.studentT 5.0
        Continuous.fisherF 5.0 10.0
    ]

let private allFamilies = families @ incompleteFamilies

let private isUniform (d: Continuous) = d = Continuous.uniform 0.0 4.0

let private pi = Math.PI

[<Tests>]
let tests =
    testList
        "Continuous"
        [
            testCase "normal cdf and survival against the reference" (fun () ->
                let d = Continuous.normal 0.0 1.0

                for row in normalCases do
                    let struct (x, c, s) = row
                    matches ("cdf at " + string x) (Continuous.cdf d x) c
                    matches ("survival at " + string x) (Continuous.survival d x) s

                let shifted = Continuous.normal 3.0 2.0

                for row in normalShiftedCases do
                    let struct (x, c) = row
                    matches ("N(3, 2) cdf at " + string x) (Continuous.cdf shifted x) c)

            testCase "normal quantile against the reference" (fun () ->
                let d = Continuous.normal 0.0 1.0

                for row in normalQuantileCases do
                    let struct (p, q) = row
                    Expect.isLessThanOrEqual (rel (Continuous.quantile d p) q) 1e-12 ("quantile at " + string p)

                Expect.equal (Continuous.quantile d 0.5) 0.0 "the median")

            testCase "normal density" (fun () ->
                let d = Continuous.normal 0.0 1.0

                for x in [ 0.0; 1.0; 5.0 ] do
                    let expected = exp (-x * x / 2.0) / sqrt (2.0 * pi)
                    Expect.isLessThanOrEqual (abs (Continuous.density d x - expected)) 1e-13 ("density at " + string x)

                Expect.isLessThanOrEqual
                    (abs (Continuous.logDensity d 30.0 - (-450.0 - 0.5 * log (2.0 * pi))))
                    1e-13
                    "logDensity at 30")

            testCase "quantile round trips" (fun () ->
                for d in families do
                    for p in lowerGrid do
                        roundTrip d p

                    for p in upperGrid do
                        if not (isUniform d && p = 1.0 - 1e-10) then
                            roundTrip d p

                for d in
                    [
                        Continuous.normal 0.0 1.0
                        Continuous.exponential 2.0
                        Continuous.logistic 1.0 2.0
                        Continuous.cauchy 0.0 1.0
                    ] do
                    roundTrip d 1e-300)

            testCase "quantile at 0 and 1 is the support's bounds" (fun () ->
                for d in allFamilies do
                    let struct (lower, upper) = Continuous.support d
                    Expect.equal (Continuous.quantile d 0.0) lower (Continuous.describe d + " at 0")
                    Expect.equal (Continuous.quantile d 1.0) upper (Continuous.describe d + " at 1")
                    Expect.isTrue (Double.IsNaN(Continuous.quantile d nan)) (Continuous.describe d + " at NaN")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Continuous.quantile d -0.1 |> ignore)
                        (Continuous.describe d + " at -0.1")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Continuous.quantile d 1.1 |> ignore)
                        (Continuous.describe d + " at 1.1"))

            testCase "logNormal against the reference" (fun () ->
                let d = Continuous.logNormal 0.5 0.8

                for row in logNormalCases do
                    let struct (x, c, s) = row
                    matches ("cdf at " + string x) (Continuous.cdf d x) c
                    matches ("survival at " + string x) (Continuous.survival d x) s

                Expect.isLessThanOrEqual (rel (Continuous.mean d) (exp (0.5 + 0.32))) 1e-14 "mean"

                Expect.isLessThanOrEqual
                    (rel (Continuous.variance d) ((exp 0.64 - 1.0) * exp (1.0 + 0.64)))
                    1e-14
                    "variance")

            testCase "exponential closed forms" (fun () ->
                let d = Continuous.exponential 2.0
                // cdf(x) = 1 - e^(-2x) = 2x (1 - x + ...), so at 1e-20 it is 2e-20 (1 - 1e-20) to 1e-40.
                Expect.isLessThanOrEqual
                    (rel (Continuous.cdf d 1e-20) (2e-20 * (1.0 - 1e-20)))
                    1e-14
                    "cdf near 0"

                Expect.isLessThanOrEqual (rel (Continuous.survival d 300.0) (exp -600.0)) 1e-13 "survival at 300"
                Expect.equal (Continuous.mean d) 0.5 "mean"
                Expect.equal (Continuous.variance d) 0.25 "variance")

            testCase "uniform closed forms" (fun () ->
                let d = Continuous.uniform 0.0 4.0
                Expect.equal (Continuous.cdf d 1.0) 0.25 "cdf at 1"

                for x in [ 0.0; 1.0; 4.0 ] do
                    Expect.equal (Continuous.density d x) 0.25 ("density at " + string x)

                for x in [ -1.0; 4.5 ] do
                    Expect.equal (Continuous.density d x) 0.0 ("density at " + string x)

                for p in [ 0.0; 1e-300; 0.1; 0.3; 0.5; 0.77; 1.0 ] do
                    Expect.equal (Continuous.quantile d p) (4.0 * p) ("quantile at " + string p)

                Expect.equal (Continuous.mean d) 2.0 "mean"
                Expect.equal (Continuous.variance d) (16.0 / 12.0) "variance")

            testCase "cauchy tails" (fun () ->
                let d = Continuous.cauchy 0.0 1.0

                Expect.isLessThanOrEqual
                    (rel (Continuous.cdf d -1e100) (1e-100 / pi))
                    1e-14
                    "cdf far in the lower tail"

                Expect.equal (Continuous.cdf d 0.0) 0.5 "cdf at 0"
                Expect.isLessThanOrEqual (abs (Continuous.cdf d 1.0 - 0.75)) 1e-15 "cdf at 1"
                // atan(1/z) / pi = 1 / (z pi) (1 - 1 / (3 z^2) + ...), the next term 3e-21 of the first at z = 1e10.
                Expect.isLessThanOrEqual (rel (Continuous.survival d 1e10) (1e-10 / pi)) 1e-14 "survival at 1e10"
                Expect.isLessThanOrEqual (abs (Continuous.quantile d 0.75 - 1.0)) 1e-15 "quantile at 0.75"
                Expect.isTrue (Double.IsNaN(Continuous.mean d)) "mean"
                Expect.isTrue (Double.IsNaN(Continuous.variance d)) "variance")

            testCase "weibull closed forms" (fun () ->
                let d = Continuous.weibull 1.5 2.0
                // 1 - e^(-t) = t (1 - t / 2 + ...) with t = (x / scale)^shape = 1e-14^1.5 at x = 2e-14.
                let t = 1e-14 ** 1.5

                Expect.isLessThanOrEqual
                    (rel (Continuous.cdf d 2e-14) (t * (1.0 - t / 2.0)))
                    1e-13
                    "cdf near 0"

                Expect.isLessThanOrEqual
                    (rel (Continuous.survival d 20.0) (exp (-(10.0 ** 1.5))))
                    1e-13
                    "survival at 20"

                let d2 = Continuous.weibull 2.0 3.0
                // mean = scale Gamma(3/2) = 3 sqrt(pi) / 2; variance = scale^2 (Gamma(2) - Gamma(3/2)^2) = 9 (1 - pi / 4).
                Expect.isLessThanOrEqual (rel (Continuous.mean d2) (3.0 * sqrt pi / 2.0)) 1e-14 "mean"
                Expect.isLessThanOrEqual (rel (Continuous.variance d2) (9.0 * (1.0 - pi / 4.0))) 1e-14 "variance"
                Expect.equal (Continuous.density (Continuous.weibull 0.5 1.0) 0.0) infinity "density at 0, shape 1/2")

            testCase "weibull variance against the reference at large shape" (fun () ->
                for row in weibullVarianceCases do
                    let struct (k, scale, v) = row

                    Expect.isLessThanOrEqual
                        (rel (Continuous.variance (Continuous.weibull k scale)) v)
                        1e-13
                        ("variance at shape " + string k + ", scale " + string scale)

                // The direct difference of gamma values goes negative from a shape of about 1e8.
                Expect.isTrue (Continuous.variance (Continuous.weibull 1e8 1.0) > 0.0) "positive at shape 1e8"
                Expect.isTrue (Continuous.standardDeviation (Continuous.weibull 1e8 1.0) > 0.0) "sd at shape 1e8")

            testCase "the gamma reads its value at 0 where x / scale underflows" (fun () ->
                // The documented limit: at shape 0.5, scale 1e300 and x = 1e-300, x / scale is 1e-600, which is 0 in
                // the doubles, so the cdf reads 0 and the density +infinity, the values at 0. The true cdf is
                // P(1/2, 1e-600) = erf(1e-300), about 1.1e-300, and the density about 0.564 (closed forms).
                let d = Continuous.gamma 0.5 1e300
                Expect.equal (Continuous.cdf d 1e-300) 0.0 "cdf"
                Expect.equal (Continuous.survival d 1e-300) 1.0 "survival"
                Expect.equal (Continuous.density d 1e-300) infinity "density")

            testCase "weibull variance against the reference below shape 10" (fun () ->
                // Below 10 the variance is the direct difference Γ(1 + 2/k) − Γ(1 + 1/k)², which loses about log10(k²)
                // digits to cancellation.
                for row in weibullVarianceBelowTenCases do
                    let struct (k, scale, v) = row

                    Expect.isLessThanOrEqual
                        (rel (Continuous.variance (Continuous.weibull k scale)) v)
                        1e-12
                        ("variance at shape " + string k + ", scale " + string scale))

            testCase "weibull survival at a large shape and a scale that is not a power of two" (fun () ->
                for row in weibullSurvivalCases do
                    let struct (k, scale, x, sf) = row
                    let d = Continuous.weibull k scale
                    // x / scale rounds to half an ulp and is raised to the shape: the error grows as shape * (x / scale)^shape.
                    let bound = if k <= 20.0 then 1e-12 else 1e-9
                    matchesWithin bound ("survival at shape " + string k + ", x " + string x) (Continuous.survival d x) sf)

            testCase "log-normal quantile round trip at a large mu" (fun () ->
                // exp (mu + sigma * z) rounds its argument to half an ulp of mu: the error grows as |mu| * |z| / sigma.
                for p in lowerGrid @ upperGrid @ [ 1e-300 ] do
                    roundTrip (Continuous.logNormal 50.0 0.1) p

                let d = Continuous.logNormal 300.0 0.1

                for p in [ 1e-300; 1e-100; 1e-10; 0.3; 0.975 ] do
                    let q = Continuous.quantile d p

                    let err =
                        if p <= 0.5 then
                            rel (Continuous.cdf d q) p
                        else
                            rel (Continuous.survival d q) (1.0 - p)

                    Expect.isLessThanOrEqual err 2e-11 ("round trip at " + string p))

            testCase "logistic closed forms" (fun () ->
                let d = Continuous.logistic 1.0 2.0
                let expected = 1.0 / (1.0 + exp 700.0)
                Expect.isLessThanOrEqual (rel (Continuous.cdf d -1399.0) expected) 1e-13 "cdf in the lower tail"
                Expect.isLessThanOrEqual (rel (Continuous.survival d 1401.0) expected) 1e-13 "survival in the upper tail"
                Expect.equal (Continuous.mean d) 1.0 "mean"
                Expect.isLessThanOrEqual (rel (Continuous.variance d) (4.0 * pi * pi / 3.0)) 1e-14 "variance")

            testCase "the density integrates to the cdf" (fun () ->
                for d in allFamilies do
                    let a = Continuous.quantile d 0.1
                    let b = Continuous.quantile d 0.9
                    let integral = Integrate.adaptive 1e-14 1e-12 (Continuous.density d) a b
                    Expect.isTrue integral.Converged (Continuous.describe d + " converged")

                    Expect.isLessThanOrEqual
                        (abs (integral.Value - (Continuous.cdf d b - Continuous.cdf d a)))
                        1e-10
                        (Continuous.describe d + " integral"))

            testCase "support" (fun () ->
                let expected =
                    [
                        Continuous.normal 0.0 1.0, struct (-infinity, infinity)
                        Continuous.logNormal 0.5 0.8, struct (0.0, infinity)
                        Continuous.uniform 0.0 4.0, struct (0.0, 4.0)
                        Continuous.exponential 2.0, struct (0.0, infinity)
                        Continuous.cauchy 0.0 1.0, struct (-infinity, infinity)
                        Continuous.weibull 1.5 2.0, struct (0.0, infinity)
                        Continuous.logistic 1.0 2.0, struct (-infinity, infinity)
                        Continuous.gamma 2.5 2.0, struct (0.0, infinity)
                        Continuous.chiSquared 3.0, struct (0.0, infinity)
                        Continuous.beta 2.5 3.5, struct (0.0, 1.0)
                        Continuous.studentT 5.0, struct (-infinity, infinity)
                        Continuous.fisherF 5.0 10.0, struct (0.0, infinity)
                    ]

                for d, bounds in expected do
                    Expect.equal (Continuous.support d) bounds (Continuous.describe d)

                for d in allFamilies do
                    let sd = Continuous.standardDeviation d
                    let variance = Continuous.variance d

                    if Double.IsNaN variance then
                        Expect.isTrue (Double.IsNaN sd) (Continuous.describe d + " standard deviation")
                    else
                        Expect.equal sd (sqrt variance) (Continuous.describe d + " standard deviation"))

            testCase "NaN and infinite arguments" (fun () ->
                for d in allFamilies do
                    let name = Continuous.describe d
                    Expect.isTrue (Double.IsNaN(Continuous.cdf d nan)) (name + " cdf at NaN")
                    Expect.isTrue (Double.IsNaN(Continuous.survival d nan)) (name + " survival at NaN")
                    Expect.isTrue (Double.IsNaN(Continuous.density d nan)) (name + " density at NaN")
                    Expect.equal (Continuous.cdf d -infinity) 0.0 (name + " cdf at -infinity")
                    Expect.equal (Continuous.cdf d infinity) 1.0 (name + " cdf at infinity")
                    Expect.equal (Continuous.survival d -infinity) 1.0 (name + " survival at -infinity")
                    Expect.equal (Continuous.survival d infinity) 0.0 (name + " survival at infinity")
                    Expect.equal (Continuous.density d infinity) 0.0 (name + " density at infinity")
                    Expect.equal (Continuous.density d -infinity) 0.0 (name + " density at -infinity")
                    Expect.equal (Continuous.logDensity d infinity) -infinity (name + " logDensity at infinity")

                for d, outside in
                    [
                        Continuous.logNormal 0.5 0.8, [ -1.0; 0.0 ]
                        Continuous.uniform 0.0 4.0, [ -1.0; 4.5 ]
                        Continuous.exponential 2.0, [ -1.0 ]
                        Continuous.weibull 1.5 2.0, [ -1.0 ]
                        Continuous.gamma 2.5 2.0, [ -1.0 ]
                        Continuous.chiSquared 3.0, [ -1.0 ]
                        Continuous.beta 2.5 3.5, [ -0.5; 1.5 ]
                        Continuous.fisherF 5.0 10.0, [ -1.0 ]
                    ] do
                    for x in outside do
                        let name = Continuous.describe d + " at " + string x
                        Expect.equal (Continuous.density d x) 0.0 (name + " density")
                        Expect.equal (Continuous.logDensity d x) -infinity (name + " logDensity"))

            testCase "describe" (fun () ->
                Expect.equal (Continuous.describe (Continuous.normal 0.0 1.0)) "Normal(mean = 0, sd = 1)" "normal"

                Expect.equal
                    (Continuous.describe (Continuous.logNormal 0.5 0.8))
                    "LogNormal(mu = 0.5, sigma = 0.8)"
                    "logNormal"

                Expect.equal
                    (Continuous.describe (Continuous.uniform 0.0 4.0))
                    "Uniform(lower = 0, upper = 4)"
                    "uniform"

                Expect.equal (Continuous.describe (Continuous.exponential 2.0)) "Exponential(rate = 2)" "exponential"

                Expect.equal
                    (Continuous.describe (Continuous.cauchy 0.0 1.0))
                    "Cauchy(location = 0, scale = 1)"
                    "cauchy"

                Expect.equal
                    (Continuous.describe (Continuous.weibull 1.5 2.0))
                    "Weibull(shape = 1.5, scale = 2)"
                    "weibull"

                Expect.equal
                    (Continuous.describe (Continuous.logistic 1.0 2.0))
                    "Logistic(location = 1, scale = 2)"
                    "logistic"

                Expect.equal
                    (Continuous.describe (Continuous.gamma 2.5 2.0))
                    "Gamma(shape = 2.5, scale = 2)"
                    "gamma"

                Expect.equal (Continuous.describe (Continuous.chiSquared 3.0)) "ChiSquared(dof = 3)" "chiSquared"
                Expect.equal (Continuous.describe (Continuous.beta 2.5 3.5)) "Beta(a = 2.5, b = 3.5)" "beta"
                Expect.equal (Continuous.describe (Continuous.studentT 5.0)) "StudentT(dof = 5)" "studentT"

                Expect.equal
                    (Continuous.describe (Continuous.fisherF 5.0 10.0))
                    "FisherF(d1 = 5, d2 = 10)"
                    "fisherF")

            testCase "constructors reject parameters outside their domain" (fun () ->
                let rejects (name: string) (build: unit -> Continuous) =
                    Expect.throwsT<ArgumentOutOfRangeException> (fun () -> build () |> ignore) name

                rejects "normal 0 0" (fun () -> Continuous.normal 0.0 0.0)
                rejects "normal 0 -1" (fun () -> Continuous.normal 0.0 -1.0)
                rejects "normal 0 nan" (fun () -> Continuous.normal 0.0 nan)
                rejects "normal infinity 1" (fun () -> Continuous.normal infinity 1.0)
                rejects "logNormal 0 0" (fun () -> Continuous.logNormal 0.0 0.0)
                rejects "uniform 1 1" (fun () -> Continuous.uniform 1.0 1.0)
                rejects "uniform 2 1" (fun () -> Continuous.uniform 2.0 1.0)
                rejects "uniform -infinity 0" (fun () -> Continuous.uniform -infinity 0.0)
                rejects "exponential 0" (fun () -> Continuous.exponential 0.0)
                rejects "exponential infinity" (fun () -> Continuous.exponential infinity)
                rejects "cauchy 0 0" (fun () -> Continuous.cauchy 0.0 0.0)
                rejects "weibull 0 1" (fun () -> Continuous.weibull 0.0 1.0)
                rejects "weibull 1 0" (fun () -> Continuous.weibull 1.0 0.0)
                rejects "logistic 0 -1" (fun () -> Continuous.logistic 0.0 -1.0)
                rejects "gamma 0 1" (fun () -> Continuous.gamma 0.0 1.0)
                rejects "gamma 1 0" (fun () -> Continuous.gamma 1.0 0.0)
                rejects "chiSquared 0" (fun () -> Continuous.chiSquared 0.0)
                rejects "chiSquared infinity" (fun () -> Continuous.chiSquared infinity)
                rejects "beta 0 1" (fun () -> Continuous.beta 0.0 1.0)
                rejects "beta 1 nan" (fun () -> Continuous.beta 1.0 nan)
                rejects "studentT 0" (fun () -> Continuous.studentT 0.0)
                rejects "studentT infinity" (fun () -> Continuous.studentT infinity)
                rejects "fisherF 0 1" (fun () -> Continuous.fisherF 0.0 1.0)
                rejects "fisherF 1 -1" (fun () -> Continuous.fisherF 1.0 -1.0)

                // dof / 2 must be greater than 0: at the least double, 5e-324, it rounds to 0 and the cdf raised inside.
                rejects "chiSquared 5e-324" (fun () -> Continuous.chiSquared 5e-324)
                rejects "studentT 5e-324" (fun () -> Continuous.studentT 5e-324)
                rejects "fisherF 5e-324 1" (fun () -> Continuous.fisherF 5e-324 1.0)
                rejects "fisherF 1 5e-324" (fun () -> Continuous.fisherF 1.0 5e-324)
                Expect.isTrue (Continuous.cdf (Continuous.chiSquared 1e-323) 1.0 > 0.0) "chiSquared 1e-323 is accepted"

                // upper - lower overflows: the exception names the parameter upper.
                rejects "uniform -1e308 1e308" (fun () -> Continuous.uniform -1e308 1e308)
#if !FABLE_COMPILER
                let e = Expect.throwsC (fun () -> Continuous.uniform -1e308 1e308 |> ignore) id

                match e with
                | :? ArgumentOutOfRangeException as range -> Expect.equal range.ParamName "upper" "the parameter named"
                | other -> failtestf "expected ArgumentOutOfRangeException, got %s" (other.GetType().Name)
#endif
                )

            testCase "equal parameters give equal distributions" (fun () ->
                Expect.equal (Continuous.normal 0.0 1.0) (Continuous.normal 0.0 1.0) "same parameters"
                Expect.notEqual (Continuous.normal 0.0 1.0) (Continuous.normal 0.0 2.0) "different parameters")

            testCase "samples follow the distribution" (fun () ->
                let count = 20000
                // The asymptotic Kolmogorov critical value at alpha = 1e-4, sqrt(-ln(alpha / 2) / 2) = 2.226
                // (general knowledge), divided by sqrt n.
                let critical = 2.226 / sqrt (float count)

                for d in allFamilies do
                    let name = Continuous.describe d
                    let struct (xs, final) = Continuous.samples d count (Generator.create 2026)
                    let ks = ksDistance (Continuous.cdf d) xs
                    Expect.isLessThanOrEqual ks critical (name + " Kolmogorov-Smirnov distance " + string ks)

                    let sd = Continuous.standardDeviation d

                    if not (Double.IsNaN sd || Double.IsInfinity sd) then
                        let mean = Array.sum xs / float count

                        Expect.isLessThanOrEqual
                            (abs (mean - Continuous.mean d))
                            (5.0 * sd / sqrt (float count))
                            (name + " sample mean")

                    let mutable g = Generator.create 2026

                    for i in 0 .. count - 1 do
                        let struct (x, next) = Continuous.sample d g
                        g <- next

                        if x <> xs[i] then
                            Expect.equal x xs[i] (name + " sample " + string i)

                    Expect.equal g final (name + " final generator")

                    Expect.throwsT<ArgumentOutOfRangeException>
                        (fun () -> Continuous.samples d -1 (Generator.create 1) |> ignore)
                        (name + " with a negative count"))

            testCase "gamma against the reference" (fun () ->
                for row in gammaCases do
                    let struct (k, scale, x, c, s, f) = row
                    let d = Continuous.gamma k scale
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f

                for row in gammaQuantileCases do
                    let struct (k, scale, p, q) = row
                    let d = Continuous.gamma k scale
                    matches (Continuous.describe d + " quantile at " + string p) (Continuous.quantile d p) q)

            testCase "chi-squared against the reference" (fun () ->
                for row in chiSquaredCases do
                    let struct (dof, x, c, s, f) = row
                    let d = Continuous.chiSquared dof
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f

                for row in chiSquaredQuantileCases do
                    let struct (dof, p, q) = row
                    let d = Continuous.chiSquared dof
                    matches (Continuous.describe d + " quantile at " + string p) (Continuous.quantile d p) q)

            testCase "beta against the reference" (fun () ->
                for row in betaCases do
                    let struct (a, b, x, c, s, f) = row
                    let d = Continuous.beta a b
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f

                for row in betaQuantileCases do
                    let struct (a, b, p, q) = row
                    let d = Continuous.beta a b
                    matches (Continuous.describe d + " quantile at " + string p) (Continuous.quantile d p) q)

            testCase "Student t against the reference" (fun () ->
                for row in studentTCases do
                    let struct (dof, t, c, s, f) = row
                    let d = Continuous.studentT dof
                    let name = Continuous.describe d + " at " + string t
                    matches (name + " cdf") (Continuous.cdf d t) c
                    matches (name + " survival") (Continuous.survival d t) s
                    matches (name + " density") (Continuous.density d t) f

                for row in studentTQuantileCases do
                    let struct (dof, p, q) = row
                    let d = Continuous.studentT dof
                    // closeTo, since matches skips a reference below 1e-300 and so every negative quantile.
                    closeTo 1e-12 (Continuous.describe d + " quantile at " + string p) (Continuous.quantile d p) q)

            testCase "F against the reference" (fun () ->
                for row in fisherFCases do
                    let struct (d1, d2, x, c, s, f) = row
                    let d = Continuous.fisherF d1 d2
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f

                for row in fisherFQuantileCases do
                    let struct (d1, d2, p, q) = row
                    let d = Continuous.fisherF d1 d2
                    matches (Continuous.describe d + " quantile at " + string p) (Continuous.quantile d p) q)

            testCase "chi-squared with 2 degrees of freedom is the exponential with rate 1/2" (fun () ->
                // Identity: chi-squared(2) is gamma(1, 2), whose density is e^(-x/2) / 2.
                let d = Continuous.chiSquared 2.0
                let e = Continuous.exponential 0.5

                for x in [ 1e-8; 0.5; 2.0; 40.0; 1000.0 ] do
                    let name = "at " + string x
                    closeTo 1e-12 (name + " cdf") (Continuous.cdf d x) (Continuous.cdf e x)
                    closeTo 1e-12 (name + " survival") (Continuous.survival d x) (Continuous.survival e x)
                    closeTo 1e-12 (name + " density") (Continuous.density d x) (Continuous.density e x)

                for p in lowerGrid @ upperGrid do
                    closeTo 1e-12 ("quantile at " + string p) (Continuous.quantile d p) (Continuous.quantile e p))

            testCase "gamma with shape 1 is the exponential" (fun () ->
                // Identity: gamma(1, scale) has density e^(-x/scale) / scale.
                let d = Continuous.gamma 1.0 3.0
                let e = Continuous.exponential (1.0 / 3.0)

                for x in [ 1e-8; 0.5; 2.0; 40.0; 1000.0 ] do
                    let name = "at " + string x
                    closeTo 1e-12 (name + " cdf") (Continuous.cdf d x) (Continuous.cdf e x)
                    closeTo 1e-12 (name + " survival") (Continuous.survival d x) (Continuous.survival e x)
                    closeTo 1e-12 (name + " density") (Continuous.density d x) (Continuous.density e x)

                for p in lowerGrid @ upperGrid do
                    closeTo 1e-12 ("quantile at " + string p) (Continuous.quantile d p) (Continuous.quantile e p))

            testCase "beta(1, 1) is the uniform and beta(1/2, 1/2) the arcsine" (fun () ->
                // Closed forms: I_x(1, 1) = x and I_x(1/2, 1/2) = (2 / pi) asin(sqrt x).
                let uniform = Continuous.beta 1.0 1.0

                for x in [ 1e-12; 0.1; 0.3; 0.5; 0.9 ] do
                    closeTo 1e-14 ("beta(1, 1) cdf at " + string x) (Continuous.cdf uniform x) x

                let arcsine = Continuous.beta 0.5 0.5

                for x in [ 1e-12; 0.1; 0.5; 0.9 ] do
                    closeTo
                        1e-13
                        ("beta(1/2, 1/2) cdf at " + string x)
                        (Continuous.cdf arcsine x)
                        (2.0 / pi * asin (sqrt x)))

            testCase "Student t with 1 degree of freedom is the Cauchy" (fun () ->
                // Identity (spec section 5.2): t(1) is the standard Cauchy.
                let t = Continuous.studentT 1.0
                let c = Continuous.cauchy 0.0 1.0

                for x in [ -1e100; -1e10; -3.0; -1.0; 0.0; 0.5; 2.0; 1e10 ] do
                    let name = "at " + string x
                    closeTo 1e-12 (name + " cdf") (Continuous.cdf t x) (Continuous.cdf c x)
                    closeTo 1e-12 (name + " survival") (Continuous.survival t x) (Continuous.survival c x)
                    closeTo 1e-12 (name + " density") (Continuous.density t x) (Continuous.density c x)

                for p in lowerGrid @ upperGrid do
                    closeTo 1e-12 ("quantile at " + string p) (Continuous.quantile t p) (Continuous.quantile c p))

            testCase "the square of a t is an F with 1 numerator degree of freedom" (fun () ->
                // Identity: P(F(1, n) > t^2) = P(|T(n)| > t) = 2 P(T(n) > t).
                let f = Continuous.fisherF 1.0 5.0
                let t = Continuous.studentT 5.0

                for x in [ 0.3; 1.0; 2.5; 10.0; 300.0 ] do
                    closeTo
                        1e-12
                        ("at " + string x)
                        (Continuous.survival f (x * x))
                        (2.0 * Continuous.survival t x))

            testCase "quantile round trips for the incomplete-function families" (fun () ->
                for d in
                    [
                        Continuous.gamma 2.5 2.0
                        Continuous.gamma 0.5 1.0
                        Continuous.chiSquared 3.0
                        Continuous.chiSquared 10.0
                        Continuous.studentT 2.5
                        Continuous.studentT 30.0
                        Continuous.fisherF 5.0 10.0
                    ] do
                    for p in lowerGrid @ upperGrid do
                        roundTrip d p

                // At 1 - 1e-10 the quantile of beta(2.5, 3.5) is 1 - 7.7e-4, where adjacent doubles move the survival by
                // 5e-13 of itself, half the bound (measured here), so the round trip there rests on the rounding.
                let b = Continuous.beta 2.5 3.5

                for p in lowerGrid @ upperGrid do
                    if p <> 1.0 - 1e-10 then
                        roundTrip b p)

            testCase "densities at the lower bound" (fun () ->
                // The limits at 0 of x^(k - 1) e^(-x/scale) / (Gamma(k) scale^k), x^(a - 1) (1 - x)^(b - 1) / B(a, b) and
                // the F density (closed forms): +infinity below shape 1, the constant at shape 1, 0 above.
                for d, expected in
                    [
                        Continuous.gamma 0.5 1.0, infinity
                        Continuous.gamma 1.0 2.0, 0.5
                        Continuous.gamma 2.0 1.0, 0.0
                        Continuous.chiSquared 1.0, infinity
                        Continuous.chiSquared 2.0, 0.5
                        Continuous.beta 0.5 2.0, infinity
                        Continuous.beta 1.0 3.0, 3.0
                        Continuous.beta 2.0 2.0, 0.0
                        Continuous.fisherF 1.0 5.0, infinity
                        Continuous.fisherF 2.0 5.0, 1.0
                        Continuous.fisherF 5.0 5.0, 0.0
                    ] do
                    let name = Continuous.describe d
                    Expect.equal (Continuous.density d 0.0) expected (name + " density at 0")
                    closeTo 1e-15 (name + " logDensity at 0") (Continuous.logDensity d 0.0) (log expected))

            testCase "log densities at both bounds and at samples on the bounds" (fun () ->
                // At each end of the support the log density is the log of the density's limit there, never NaN.
                for d in
                    incompleteFamilies
                    @ [
                        Continuous.gamma 0.5 1.0
                        Continuous.gamma 1.0 2.0
                        Continuous.chiSquared 2.0
                        Continuous.beta 0.5 2.0
                        Continuous.beta 1.0 3.0
                        Continuous.beta 3.0 1.0
                        Continuous.beta 2.0 0.5
                        Continuous.studentT 1.0
                        Continuous.fisherF 1.0 5.0
                        Continuous.fisherF 2.0 5.0
                    ] do
                    let struct (lower, upper) = Continuous.support d

                    for x in [ lower; upper ] do
                        let name = Continuous.describe d + " at " + string x
                        let ld = Continuous.logDensity d x
                        Expect.isFalse (Double.IsNaN ld) (name + " logDensity is not NaN")
                        closeTo 1e-15 (name + " logDensity") ld (log (Continuous.density d x))

                // Generator.gamma gives exactly 0 for about half the draws at shape 1e-3, and Generator.beta exactly 0 or
                // 1 for most draws at (0.01, 0.01) (general knowledge: u^(1/shape) underflows where u < 2^(-1075 shape)).
                for d in [ Continuous.gamma 1e-3 1.0; Continuous.beta 0.01 0.01 ] do
                    let name = Continuous.describe d
                    let struct (xs, _) = Continuous.samples d 200 (Generator.create 7)
                    let struct (lower, upper) = Continuous.support d
                    Expect.exists xs (fun x -> x = lower || x = upper) (name + " has samples on a bound")

                    for x in xs do
                        if x = lower || x = upper then
                            Expect.equal (Continuous.logDensity d x) infinity (name + " logDensity at " + string x)
                        else
                            Expect.isFalse
                                (Double.IsNaN(Continuous.logDensity d x))
                                (name + " logDensity at " + string x))

            testCase "moments where they exist" (fun () ->
                let isNaN (name: string) (x: float) = Expect.isTrue (Double.IsNaN x) (name + " is NaN")

                let near (name: string) (actual: float) (expected: float) =
                    Expect.isLessThanOrEqual (rel actual expected) 1e-14 name

                // Closed forms: t(n) has mean 0 for n > 1 and variance n / (n - 2) for n > 2; F(m, n) has mean n / (n - 2)
                // for n > 2 and variance 2 n^2 (m + n - 2) / (m (n - 2)^2 (n - 4)) for n > 4.
                let t1 = Continuous.studentT 1.0
                isNaN "t(1) mean" (Continuous.mean t1)
                isNaN "t(1) variance" (Continuous.variance t1)
                let t2 = Continuous.studentT 2.0
                Expect.equal (Continuous.mean t2) 0.0 "t(2) mean"
                Expect.equal (Continuous.variance t2) infinity "t(2) variance"
                near "t(5) variance" (Continuous.variance (Continuous.studentT 5.0)) (5.0 / 3.0)
                let f52 = Continuous.fisherF 5.0 2.0
                Expect.equal (Continuous.mean f52) infinity "F(5, 2) mean"
                isNaN "F(5, 2) variance" (Continuous.variance f52)
                let f54 = Continuous.fisherF 5.0 4.0
                near "F(5, 4) mean" (Continuous.mean f54) 2.0
                Expect.equal (Continuous.variance f54) infinity "F(5, 4) variance"
                let f510 = Continuous.fisherF 5.0 10.0
                near "F(5, 10) mean" (Continuous.mean f510) 1.25
                near "F(5, 10) variance" (Continuous.variance f510) (1300.0 / 960.0)
                // gamma(k, s): mean k s, variance k s^2; beta(a, b): a / (a + b), a b / ((a + b)^2 (a + b + 1)).
                let g = Continuous.gamma 2.5 2.0
                near "gamma mean" (Continuous.mean g) 5.0
                near "gamma variance" (Continuous.variance g) 10.0
                let b = Continuous.beta 2.5 3.5
                near "beta mean" (Continuous.mean b) (2.5 / 6.0)
                near "beta variance" (Continuous.variance b) (8.75 / 252.0)
                let c = Continuous.chiSquared 3.0
                near "chi-squared mean" (Continuous.mean c) 3.0
                near "chi-squared variance" (Continuous.variance c) 6.0)

            testCase "where a plain form would cancel or round 1 - x, against the reference" (fun () ->
                for row in studentTRegimeCases do
                    let struct (dof, t, c, s, f) = row
                    let d = Continuous.studentT dof
                    let name = Continuous.describe d + " at " + string t
                    matches (name + " cdf") (Continuous.cdf d t) c
                    matches (name + " survival") (Continuous.survival d t) s
                    matches (name + " density") (Continuous.density d t) f

                for row in betaRegimeCases do
                    let struct (a, b, x, c, s, f) = row
                    let d = Continuous.beta a b
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f

                for row in fisherFRegimeCases do
                    let struct (d1, d2, x, c, s, f) = row
                    let d = Continuous.fisherF d1 d2
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f)

            testCase "gamma and chi-squared quantile round trips within 1e-14 of 1" (fun () ->
                // Near 1 the inverse of P resolves 1 - p only to the spacing of the doubles near 1 (measured here: at
                // shape 2 and p = 1 - 1e-14 it starts at 39.47 against a root near 35).
                for d in [ Continuous.gamma 2.0 1.0; Continuous.chiSquared 4.0 ] do
                    for p in [ 1.0 - 1e-12; 1.0 - 1e-14; 1.0 - 1e-15 ] do
                        roundTrip d p)

            testCase "the stated domains at their edges against the reference" (fun () ->
                // The density carries no stated bound; the module remarks give 1.2e-12 at gamma shape 1000 and 1.1e-10
                // at chi-squared 1e5 degrees of freedom (measured here), pinned here with that much room.
                for row in gammaEdgeCases do
                    let struct (k, scale, x, c, s, f) = row
                    let d = Continuous.gamma k scale
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matchesWithin (if k > 100.0 then 2e-12 else 1e-12) (name + " density") (Continuous.density d x) f

                for row in chiSquaredEdgeCases do
                    let struct (dof, x, c, s, f) = row
                    let d = Continuous.chiSquared dof
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matchesWithin (if dof > 100.0 then 2e-10 else 1e-12) (name + " density") (Continuous.density d x) f

                for row in betaEdgeCases do
                    let struct (a, b, x, c, s, f) = row
                    let d = Continuous.beta a b
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f

                for row in studentTEdgeCases do
                    let struct (dof, t, c, s, f) = row
                    let d = Continuous.studentT dof
                    let name = Continuous.describe d + " at " + string t
                    matches (name + " cdf") (Continuous.cdf d t) c
                    matches (name + " survival") (Continuous.survival d t) s
                    matches (name + " density") (Continuous.density d t) f

                for row in fisherFEdgeCases do
                    let struct (d1, d2, x, c, s, f) = row
                    let d = Continuous.fisherF d1 d2
                    let name = Continuous.describe d + " at " + string x
                    matches (name + " cdf") (Continuous.cdf d x) c
                    matches (name + " survival") (Continuous.survival d x) s
                    matches (name + " density") (Continuous.density d x) f)

            testCase "quantile round trips at the edges of the stated domains" (fun () ->
                let all = lowerGrid @ upperGrid @ [ 1.0 - 1e-12; 1.0 - 1e-14; 1.0 - 1e-15 ]
                let without (excluded: float list) = all |> List.filter (fun p -> not (List.contains p excluded))
                // Left out (measured here): the p whose root is below the least normal double (1e-100, and 1e-10 at
                // shapes 0.025 and 0.01), and for the beta with b = 0.5 or 0.2 the p whose root lies so near 1 that
                // adjacent doubles move the survival by more than a third of the bound (from 0.999 at (0.5, 0.5),
                // 0.975 at (100, 0.5) and (0.2, 0.2), 0.5 at (500, 0.2)), or whose root rounds to 1. A third, not the
                // whole bound, because the quantile's doc calls such a root unresolved where adjacent doubles move the
                // survival by more than the 1e-12 bound itself: a p that the doubles resolve only to a third of the bound
                // or worse sits near that line, and the result is then within one double of the root rather than within
                // the bound, which this test does not assert.
                let atLeastNormal = without [ 1e-100 ]
                let upTo (largest: float) = all |> List.filter (fun p -> p <= largest)

                for d, ps in
                    [
                        Continuous.gamma 0.01 1.0, without [ 1e-100; 1e-10 ]
                        Continuous.gamma 1000.0 1.0, all
                        Continuous.chiSquared 0.05, without [ 1e-100; 1e-10 ]
                        Continuous.chiSquared 0.5, atLeastNormal
                        Continuous.chiSquared 100.0, all
                        Continuous.chiSquared 1e5, all
                        Continuous.beta 0.5 0.5, upTo 0.975
                        Continuous.beta 0.5 100.0, all
                        Continuous.beta 100.0 0.5, upTo 0.6
                        Continuous.beta 100.0 100.0, all
                        Continuous.beta 0.2 0.2, upTo 0.6 |> List.filter (fun p -> p <> 1e-100)
                        Continuous.beta 0.2 500.0, atLeastNormal
                        Continuous.beta 500.0 0.2, upTo 0.3
                        Continuous.beta 500.0 500.0, all
                        Continuous.studentT 0.05, atLeastNormal
                        Continuous.studentT 0.5, all
                        Continuous.studentT 100.0, all
                        Continuous.studentT 1000.0, all
                        Continuous.fisherF 0.5 0.5, atLeastNormal
                        Continuous.fisherF 0.5 100.0, atLeastNormal
                        Continuous.fisherF 100.0 0.5, all
                        Continuous.fisherF 100.0 100.0, all
                        Continuous.fisherF 1000.0 0.5, all
                        Continuous.fisherF 1000.0 100.0, all
                        Continuous.fisherF 0.5 1000.0, atLeastNormal
                        Continuous.fisherF 100.0 1000.0, all
                    ] do
                    for p in ps do
                        roundTrip d p)
        ]
