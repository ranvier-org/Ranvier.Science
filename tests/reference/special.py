"""Reference values for Ranvier.Numerics.Special, printed as F# literals.

Run: python -I tests/reference/special.py <section>

Sections:
    gamma   ln|Gamma|, Gamma, psi, B and ln B (with pairs where |ln B| >= 256), ln n!, ln C(n, k), ln(1 + x) and
            e^x - 1 at negative x; the constants and series coefficients of Special.fs
    erf     erf and erfc; the constants 2 / sqrt(pi) and 1 / sqrt(pi) of Special.fs
    incgamma
            the regularised incomplete gamma functions P(a, x) and Q(a, x), printed as 0.0 below 1e-300

Every value is computed in decimal arithmetic at DIGITS significant digits from the exact value of each double input
(Decimal(x) of the float the test passes), then again at DIGITS + 20; the script exits 1 unless both round to the same
double, and exits 1 with a message when a decimal operation signals (an overflow, say). Each value is printed with
repr(float(value)). The helpers work at the precision of the current decimal context plus GUARD digits, and round their
result to the context.

Methods:
    pi          Machin's formula, pi = 16 arctan(1/5) - 4 arctan(1/239).
    B(n)        Bernoulli numbers B(0) to B(60) as exact fractions, by the Akiyama-Tanigawa algorithm (B(1) = +1/2;
                only the even ones are used).
    ln Gamma    z > 0: a whole z below 40 gives ln((z - 1)!) exactly; otherwise z is shifted to w = z + N >= 40,
                ln Gamma(z) = ln Gamma(w) - ln(z (z + 1) ... (z + N - 1)), and ln Gamma(w) is Stirling's series
                (w - 1/2) ln w - w + ln(2 pi)/2 + sum over k = 1..30 of B(2k) / (2k (2k - 1) w^(2k - 1)).
                z < 0: ln|Gamma(z)| = ln pi - ln|sin(pi z)| - ln Gamma(1 - z).
    Gamma       exp(ln Gamma(x)) for x > 0; pi / (sin(pi x) Gamma(1 - x)) for x < 0.
    sin, cos    of pi x: x reduced modulo 2 to [-1, 1], then the Taylor series at pi times the reduced x.
    psi         z > 0: shifted to w = z + N >= 40, psi(z) = psi(w) - sum over k = 0..N-1 of 1/(z + k), and psi(w) is
                ln w - 1/(2w) - sum over k = 1..30 of B(2k) / (2k w^(2k)). z < 0: psi(1 - z) - pi cos(pi z) / sin(pi z).
    ln B        ln Gamma(a) + ln Gamma(b) - ln Gamma(a + b), at the context precision plus GUARD plus the number of digits
                in the integer part of |ln Gamma(a + b)|, so the cancellation leaves the context's precision in absolute
                terms.
    ln n!       ln of math.factorial(n) for n <= 10^5, ln Gamma(n + 1) above.
    ln C(n, k)  ln of math.comb(n, k).
    ln(1 + x)   Decimal's ln of 1 + x; e^x - 1 by Decimal's exp.
    erf         the Maclaurin series (2 / sqrt(pi)) sum over n >= 0 of (-1)^n x^(2n + 1) / (n! (2n + 1)), summed until a
                term leaves the sum unchanged; past its largest term the terms fall in magnitude and alternate in sign,
                so the remainder is below that term. The terms reach about e^(x^2) while erf(x) stays below 1, so erf is
                summed at the context precision plus ceil(x^2 / ln 10) plus GUARD digits.
    erfc        1 - erf(x), summed at the context precision plus ceil(2 x^2 / ln 10) plus GUARD digits: erfc(x) falls
                like e^(-x^2), and the subtraction cancels another x^2 / ln 10 digits.
    P(a, x)     gamma_p: x^a e^-x / Gamma(a + 1) times the sum over n >= 0 of x^n / ((a + 1) ... (a + n)). The exponent
                a ln x - x - ln Gamma(a + 1) is formed at the context precision plus GUARD plus the digits of its largest
                term, so it keeps that precision in absolute terms. The sum runs until a term leaves it unchanged and
                2x < a + n + 1; from there each term is below half the one before, so the remainder is below the last
                term. ln Gamma(a + 1) comes from ln_gamma_precise: ln Gamma's method with K = max(30, 50 ceil(d / 200))
                terms for d = the precision plus GUARD, and the shift raised until the first omitted term is below
                10^-d (asserted), so it holds at the raised precision Q needs.
    Q(a, x)     gamma_q: 1 - P(a, x), at the context precision plus ceil(x / ln 10) + 20 digits, since the subtraction
                cancels the leading -log10 Q(a, x) digits. Where that leaves fewer digits than the context's (Q falls
                like a for small a), it is computed again with 20 more digits than it cancelled, or twice the extra
                digits where the difference is 0.

For real w > 0 the error of each truncated series is at most its first omitted term in magnitude (general knowledge:
DLMF 5.11(ii), not read here); the script asserts that term is below 1e-60, which holds for every w >= 40.
"""

import math
import sys
from decimal import Decimal, DecimalException, getcontext, localcontext
from fractions import Fraction
from functools import lru_cache

DIGITS = 60
CHECK_DIGITS = DIGITS + 20
GUARD = 10
SHIFT_TO = 40
TERMS = 30
TRUNCATION = Decimal("1e-60")


def bernoulli(n_max: int) -> list[Fraction]:
    """B(0) to B(n_max) by the Akiyama-Tanigawa algorithm, with B(1) = +1/2."""
    a = [Fraction(0)] * (n_max + 1)
    numbers = []
    for m in range(n_max + 1):
        a[m] = Fraction(1, m + 1)
        for j in range(m, 0, -1):
            a[j - 1] = j * (a[j - 1] - a[j])
        numbers.append(a[0])
    return numbers


B = bernoulli(2 * TERMS + 2)
# Known values (general knowledge) as a check of the recurrence.
assert B[2] == Fraction(1, 6) and B[4] == Fraction(-1, 30) and B[12] == Fraction(-691, 2730)
assert B[60] == Fraction(-1215233140483755572040304994079820246041491, 56786730)


def stirling_coefficient(k: int) -> Fraction:
    """B(2k) / (2k (2k - 1)), the k-th coefficient of Stirling's series for ln Gamma."""
    return B[2 * k] / (2 * k * (2 * k - 1))


def psi_coefficient(k: int) -> Fraction:
    """B(2k) / (2k), the k-th coefficient of the asymptotic series for psi."""
    return B[2 * k] / (2 * k)


def dec(q: Fraction) -> Decimal:
    """A fraction to the context's precision."""
    return Decimal(q.numerator) / Decimal(q.denominator)


@lru_cache(maxsize=None)
def pi_at(digits: int) -> Decimal:
    """pi to `digits` significant digits, by Machin's formula."""
    with localcontext() as ctx:
        ctx.prec = digits + GUARD

        def arctan_inverse(n: int) -> Decimal:
            # arctan(1/n) = sum over j >= 0 of (-1)^j / ((2j + 1) n^(2j + 1))
            power = Decimal(1) / n
            n2 = n * n
            total = power
            j = 0
            while True:
                j += 1
                power /= n2
                term = power / (2 * j + 1)
                if total + term == total:
                    return total
                total = total - term if j % 2 == 1 else total + term

        result = 16 * arctan_inverse(5) - 4 * arctan_inverse(239)
    with localcontext() as ctx:
        ctx.prec = digits
        return +result


def pi() -> Decimal:
    return pi_at(getcontext().prec)


def reduced(x: Decimal) -> Decimal:
    """x modulo 2, in [-1, 1]."""
    r = x % 2  # exact; the sign of x
    if r > 1:
        r -= 2
    elif r < -1:
        r += 2
    return r


def sin_pi(x) -> Decimal:
    """sin(pi x)."""
    x = Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        t = pi() * reduced(x)
        term = t
        total = t
        n = 1
        while True:
            term = -term * t * t / ((n + 1) * (n + 2))
            n += 2
            if total + term == total:
                break
            total += term
    return +total


def cos_pi(x) -> Decimal:
    """cos(pi x)."""
    x = Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        t = pi() * reduced(x)
        term = Decimal(1)
        total = term
        n = 0
        while True:
            term = -term * t * t / ((n + 1) * (n + 2))
            n += 2
            if total + term == total:
                break
            total += term
    return +total


def shift_count(z: Decimal) -> int:
    """The least N >= 0 with z + N >= SHIFT_TO."""
    return max(0, math.ceil(SHIFT_TO - z))


def ln_gamma(z) -> Decimal:
    """ln|Gamma(z)| for z > 0 and for non-integer z < 0."""
    z = Decimal(z)
    with localcontext() as ctx:
        ctx.prec += GUARD
        if z < 0:
            result = pi().ln() - abs(sin_pi(z)).ln() - ln_gamma(1 - z)
        elif z == z.to_integral_value() and z < SHIFT_TO:
            result = Decimal(math.factorial(int(z) - 1)).ln()
        else:
            n = shift_count(z)
            w = z + n
            product = Decimal(1)
            for k in range(n):
                product *= z + k
            series = Decimal(0)
            for k in range(1, TERMS + 1):
                series += dec(stirling_coefficient(k)) / w ** (2 * k - 1)
            omitted = dec(stirling_coefficient(TERMS + 1)) / w ** (2 * TERMS + 1)
            assert abs(omitted) < TRUNCATION, (z, omitted)
            stirling = (w - Decimal("0.5")) * w.ln() - w + (2 * pi()).ln() / 2 + series
            result = stirling - product.ln()
    return +result


def gamma_value(x) -> Decimal:
    """Gamma(x) for x > 0 and for non-integer x < 0."""
    x = Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        if x > 0:
            result = ln_gamma(x).exp()
        else:
            result = pi() / (sin_pi(x) * ln_gamma(1 - x).exp())
    return +result


def digamma(z) -> Decimal:
    """psi(z) for z > 0 and for non-integer z < 0."""
    z = Decimal(z)
    with localcontext() as ctx:
        ctx.prec += GUARD
        if z < 0:
            result = digamma(1 - z) - pi() * cos_pi(z) / sin_pi(z)
        else:
            n = shift_count(z)
            w = z + n
            shift = Decimal(0)
            for k in range(n):
                shift += 1 / (z + k)
            series = Decimal(0)
            for k in range(1, TERMS + 1):
                series += dec(psi_coefficient(k)) / w ** (2 * k)
            omitted = dec(psi_coefficient(TERMS + 1)) / w ** (2 * TERMS + 2)
            assert abs(omitted) < TRUNCATION, (z, omitted)
            result = w.ln() - 1 / (2 * w) - series - shift
    return +result


def beta_ln(a, b) -> Decimal:
    """ln B(a, b) = ln Gamma(a) + ln Gamma(b) - ln Gamma(a + b), a, b > 0, at the digits of |ln Gamma(a + b)| more."""
    a, b = Decimal(a), Decimal(b)
    with localcontext() as ctx:
        ctx.prec += GUARD
        scale = abs(ln_gamma(a + b))
        if scale >= 1:
            ctx.prec += scale.adjusted() + 1
        result = ln_gamma(a) + ln_gamma(b) - ln_gamma(a + b)
    return +result


def beta_value(a, b) -> Decimal:
    """B(a, b), a, b > 0."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = beta_ln(a, b).exp()
    return +result


def log_factorial(n: int) -> Decimal:
    """ln n!."""
    if n <= 10**5:
        return Decimal(math.factorial(n)).ln()
    return ln_gamma(n + 1)


def log_choose(n: int, k: int) -> Decimal:
    """ln C(n, k)."""
    return Decimal(math.comb(n, k)).ln()


def log1p_value(x) -> Decimal:
    """ln(1 + x), x > -1."""
    x = Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = (1 + x).ln()
    return +result


def expm1_value(x) -> Decimal:
    """e^x - 1."""
    x = Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = x.exp() - 1
    return +result


def erf_series(x: Decimal) -> Decimal:
    """erf(x) by its Maclaurin series, at the precision of the current context."""
    x2 = x * x
    power = x  # (-1)^n x^(2n + 1) / n!
    total = x
    n = 0
    while True:
        n += 1
        power = -power * x2 / n
        term = power / (2 * n + 1)
        if total + term == total:
            break
        total += term
    return 2 * total / pi().sqrt()


def erf_value(x) -> Decimal:
    """erf(x)."""
    with localcontext() as ctx:
        ctx.prec += math.ceil(x * x / math.log(10)) + GUARD
        result = erf_series(Decimal(x))
    return +result


def erfc_value(x) -> Decimal:
    """erfc(x) = 1 - erf(x)."""
    with localcontext() as ctx:
        ctx.prec += math.ceil(2 * x * x / math.log(10)) + GUARD
        result = 1 - erf_series(Decimal(x))
    return +result


@lru_cache(maxsize=None)
def bernoulli_table(n_max: int) -> tuple[Fraction, ...]:
    """B(0) to B(n_max), computed once for each n_max."""
    return tuple(bernoulli(n_max))


def ln_gamma_precise(z) -> Decimal:
    """ln Gamma(z) for z > 0 at any context precision p: computed with absolute error below 10^-(p + GUARD), then
    rounded to p digits. Stirling's series after a shift, as in ln_gamma, with the number of terms and the shift raised
    with p."""
    z = Decimal(z)
    with localcontext() as ctx:
        digits = ctx.prec + GUARD
        terms = max(TERMS, 50 * math.ceil(digits / 200))
        numbers = bernoulli_table(2 * terms + 2)
        coefficient = numbers[2 * terms + 2] / ((2 * terms + 2) * (2 * terms + 1))
        # The least w >= SHIFT_TO whose first omitted term, coefficient / w^(2 terms + 1), is below 10^-digits.
        log_w = (math.log10(abs(coefficient.numerator)) - math.log10(coefficient.denominator) + digits) / (
            2 * terms + 1
        )
        n = max(0, math.ceil(max(SHIFT_TO, 10**log_w + 1) - float(z)))
        w = z + n
        # ln Gamma(w) and ln(z (z + 1) ... (z + n - 1)) reach w ln w, and the product rounds n times.
        ctx.prec = digits + len(str(math.ceil(float(w) * math.log(float(w))))) + len(str(n)) + 2
        product = Decimal(1)
        for k in range(n):
            product *= z + k
        series = Decimal(0)
        power = w
        w2 = w * w
        for k in range(1, terms + 1):
            series += dec(numbers[2 * k] / (2 * k * (2 * k - 1))) / power
            power *= w2
        omitted = dec(coefficient) / power
        assert abs(omitted) < Decimal(10) ** -digits, (z, omitted)
        result = (w - Decimal("0.5")) * w.ln() - w + (2 * pi()).ln() / 2 + series - product.ln()
    return +result


def incomplete_gamma_sum(a: Decimal, x: Decimal) -> Decimal:
    """The sum over n >= 0 of x^n / ((a + 1) ... (a + n)), at the precision of the current context, for a > 0, x > 0."""
    term = Decimal(1)
    total = term
    n = 0
    while True:
        n += 1
        term = term * x / (a + n)
        # Once 2x < a + n + 1, each later term is below half the one before, so the remainder is below this term.
        if total + term == total and 2 * x < a + n + 1:
            return total
        total += term


def gamma_p(a, x) -> Decimal:
    """P(a, x), the regularised lower incomplete gamma function, for a > 0 and x >= 0."""
    a, x = Decimal(a), Decimal(x)
    if x == 0:
        return Decimal(0)
    with localcontext() as ctx:
        ctx.prec += GUARD
        # The exponent's terms reach x, |a ln x| and ln Gamma(a + 1) <= (a + 1) ln(a + 1); it needs absolute precision.
        magnitude = max(x, abs(a * x.ln()), (a + 1) * (a + 1).ln(), Decimal(1))
        ctx.prec += magnitude.adjusted() + 1
        exponent = a * x.ln() - x - ln_gamma_precise(a + 1)
        result = exponent.exp() * incomplete_gamma_sum(a, x)
    return +result


def gamma_q(a, x) -> Decimal:
    """Q(a, x) = 1 - P(a, x), the regularised upper incomplete gamma function, for a > 0 and x >= 0."""
    extra = math.ceil(float(x) / math.log(10)) + 20
    while True:
        with localcontext() as ctx:
            ctx.prec += extra
            result = 1 - gamma_p(a, x)
        # The subtraction leaves extra + result.adjusted() digits more than the context's; for small a, Q falls like a
        # and cancels more than x / ln 10 digits.
        if result == 0:
            extra *= 2
        elif extra + result.adjusted() < 0:
            extra = 20 - result.adjusted()
        else:
            return +result


def two_over_sqrt_pi() -> Decimal:
    return 2 / pi().sqrt()


def one_over_sqrt_pi() -> Decimal:
    return 1 / pi().sqrt()


def sqrt_two_pi() -> Decimal:
    return (2 * pi()).sqrt()


def half_ln_two_pi() -> Decimal:
    return (2 * pi()).ln() / 2


def ln_pi() -> Decimal:
    return pi().ln()


def value(f, *args) -> float:
    """f(*args) rounded to a double, computed at DIGITS and at CHECK_DIGITS; exits 1 unless both give the same double,
    and exits 1 when a decimal operation signals."""
    results = []
    for digits in (DIGITS, CHECK_DIGITS):
        with localcontext() as ctx:
            ctx.prec = digits
            try:
                results.append(float(f(*args)))
            except DecimalException as e:
                sys.exit(f"{f.__name__}{args}: decimal {type(e).__name__} at {digits} digits")
    if results[0] != results[1]:
        sys.exit(f"{f.__name__}{args}: {results[0]!r} at {DIGITS} digits, {results[1]!r} at {CHECK_DIGITS} digits")
    return results[0]


def literal(v) -> str:
    """An F# literal: repr of a float, or an int as written."""
    return repr(v)


def print_cases(name: str, fsharp_type: str, rows: list[tuple]) -> None:
    print(f"let private {name}: {fsharp_type} =")
    print("    [|")
    for row in rows:
        print("        " + ", ".join(literal(v) for v in row))
    print("    |]")
    print()


def section_gamma() -> None:
    print("// python -I tests/reference/special.py gamma")
    print()
    print("// sqrt(2 pi), ln(2 pi) / 2 and ln pi:")
    for f in (sqrt_two_pi, half_ln_two_pi, ln_pi):
        print(f"//   {f.__name__} = {value(f)!r}")
    print()
    print("// B(2k) / (2k (2k - 1)), k = 1..12, the coefficients of Stirling's series for ln Gamma:")
    for k in range(1, 13):
        print(f"//   k = {k}: {stirling_coefficient(k)}")
    print()
    print("// B(2k) / (2k), k = 1..12, the coefficients of the asymptotic series for psi:")
    for k in range(1, 13):
        print(f"//   k = {k}: {psi_coefficient(k)}")
    print()

    gamma_ln_xs = [
        1e-300, 1e-8, 0.001, 0.1, 0.5, 0.9, 1.0, 1.1, 1.5, 1.9, 2.0, 2.5, 3.7, 7.9, 8.0, 10.1, 33.3, 100.5, 171.5,
        1000.25, 100000.5, 1e10, 1e300, -0.5, -1.5, -2.75, -10.3, -100.7, -170.4, -1000.5,
        # Near a zero of ln|Gamma| in (-n - 1, -n), n = 10..15, and negative subnormals.
        -10.999999976954987, -11.999999997871052, -12.99999999983939, -13.999999999988548, -14.000000000010768,
        -15.00000000000072, -1e-320, -5e-324,
    ]
    print_cases("gammaLnCases", "(float * float)[]", [(x, value(ln_gamma, x)) for x in gamma_ln_xs])

    gamma_xs = [
        1e-300, 1e-8, 0.001, 0.1, 0.5, 1.5, 2.5, 3.7, 7.9, 10.1, 33.3, 100.5, 170.5, 171.6, -0.5, -1.5, -2.75, -10.3,
        -100.7, -150.3,
    ]
    print_cases("gammaCases", "(float * float)[]", [(x, value(gamma_value, x)) for x in gamma_xs])

    digamma_xs = [
        1e-8, 0.01, 0.5, 1.0, 1.4616321449683622, 1.5, 2.0, 2.5, 7.9, 10.0, 100.5, 1e6, 1e15, -0.5, -2.3, -10.7,
        # Near the zero of psi in (-10^9 - 1, -10^9).
        -1000000000.9521098,
    ]
    print_cases("digammaCases", "(float * float)[]", [(x, value(digamma, x)) for x in digamma_xs])

    beta_abs = [
        (0.5, 0.5), (1.0, 1.0), (2.0, 3.0), (0.001, 0.001), (0.1, 10.0), (10.5, 20.25), (8.0, 8.0), (7.9, 8.1),
        (100.0, 200.0), (1e6, 4.0), (3.5, 1e5), (1000.0, 0.01),
    ]
    print_cases(
        "betaCases",
        "(float * float * float * float)[]",
        [(a, b, value(beta_value, a, b), value(beta_ln, a, b)) for a, b in beta_abs],
    )

    # Pairs with |ln B| >= 256 from a seeded scan, where exp(ln B) carries the absolute error of ln B into B.
    beta_large_abs = [
        (285012.3312716716, 54.57821217759673), (56.66781809454644, 409868.9057467205),
        (221798.3284124938, 57.378267480692294), (163.29755153612564, 2693.0586087351535),
        (34.62098830767503, 32691.762932782338), (90.80721825997759, 41402.73972602267),
        # Both arguments in the hundreds, where (a - 1/2) log1p(-b/s) and (b - 1/2) ln(b/s) are both large.
        (194.49704136848504, 1057.1279356594662), (338.87483434172145, 636.8072643768838),
    ]
    print_cases(
        "betaLargeCases",
        "(float * float * float * float)[]",
        [(a, b, value(beta_value, a, b), value(beta_ln, a, b)) for a, b in beta_large_abs],
    )

    factorial_ns = [0, 1, 2, 10, 20, 21, 100, 1000, 1000000, 2147483647]
    print_cases("logFactorialCases", "(int * float)[]", [(n, value(log_factorial, n)) for n in factorial_ns])

    choose_nks = [(0, 0), (5, 0), (5, 5), (10, 3), (50, 25), (1000, 500), (1000000, 3), (1000000, 999997), (2147483647, 2)]
    print_cases("logChooseCases", "(int * int * float)[]", [(n, k, value(log_choose, n, k)) for n, k in choose_nks])

    print_cases("log1pCases", "(float * float)[]", [(x, value(log1p_value, x)) for x in [-0.5, -0.9, -1e-3]])
    print_cases("expm1Cases", "(float * float)[]", [(x, value(expm1_value, x)) for x in [-0.5, -5.0]])


def section_erf() -> None:
    print("// python -I tests/reference/special.py erf")
    print()
    print("// 2 / sqrt(pi) and 1 / sqrt(pi):")
    for f in (two_over_sqrt_pi, one_over_sqrt_pi):
        print(f"//   {f.__name__} = {value(f)!r}")
    print()

    erf_xs = [
        1e-300, 1e-20, 1e-8, 0.1, 0.4, 0.5, 0.9, 1.0, 1.5, 1.99, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, -0.7, -3.0,
        # The worst points of a dense seeded scan of 21,425 cases on .NET and JavaScript.
        1.4155672647003859, 1.3842121810791699, -0.5754788416632675,
    ]
    print_cases("erfCases", "(float * float)[]", [(x, value(erf_value, x)) for x in erf_xs])

    erfc_xs = [
        -6.0, -3.0, -1.0, -0.5, 1e-10, 0.3, 0.5, 0.9, 1.0, 1.5, 1.99, 2.0, 2.01, 2.5, 3.0, 4.5, 6.0, 10.0, 15.0, 20.0,
        26.0, 26.2,
        # The worst points of the same scan: below 2, where 1 - erf(x) multiplies the relative error of erf(x) by
        # erf(x) / erfc(x), 213 at x = 2; in [1, 1.8); in the continued fraction's range; and near the edge of 1e-300
        # (26.209397193028817, the largest scanned x where erfc(x) is at least 1e-300).
        1.9900586191485314, 1.9965492769326505, 1.993136684222406, 1.9984920195828624, 1.9999999999999993,
        1.7971114942683344, 1.799206427704676, 2.097369509069908, 13.556885625435964, 26.079080580576477,
        26.209397193028817,
        # The worst points of a second seeded scan, of 30,000 points in [1.9, 2), on .NET and JavaScript.
        1.9984023829417783, 1.9952152428717123,
    ]
    print_cases("erfcCases", "(float * float)[]", [(x, value(erfc_value, x)) for x in erfc_xs])


def floored(v: float) -> float:
    """v, or 0.0 when v is below 1e-300."""
    return 0.0 if v < 1e-300 else v


def section_incgamma() -> None:
    print("// python -I tests/reference/special.py incgamma")
    print()

    incgamma_axs = [
        (0.5, 0.01), (0.5, 0.1), (0.5, 2.0), (1.0, 1e-5), (1.0, 1.0), (1.0, 690.0), (2.5, 3.0), (3.0, 0.5),
        (10.0, 10.0), (10.0, 30.0), (10.0, 100.0), (30.0, 10.0), (100.0, 90.0), (100.0, 110.0), (1000.0, 1000.0),
        (1000.0, 900.0), (0.01, 0.001), (0.001, 5.0),
        # The worst points of a dense seeded scan of 7,932 points on .NET and JavaScript: the lower tail from a = 8 on,
        # the upper tail near 1e-300, and Q below a + 1 for a < 1.
        (951.0231977761456, 272.1197103557075), (183.62946971865998, 3.2110236532743675),
        (720.8248999971448, 169.1963751588507), (999.9999999999999, 2665.0), (709.2095670657058, 1809.3012846853007),
        (1e-200, 0.5615), (0.2398004852923794, 1.2191387010096424),
        # Where the method as first written missed the bound: (x - a) / a loses x when x is far below a, and 1 - P
        # cancels below a + 1 for small a.
        (18.09467398327835, 2.9282007020591715e-15), (112.97305207532186, 0.21468962230833327),
        (1.691588477341068e-10, 0.8505543045792839),
        # The ends of the double range: a subnormal x, a subnormal a, and x0 / x overflowing.
        (0.5, 5e-324), (5e-324, 0.5), (1e-12, 5e-324),
    ]
    print_cases(
        "incompleteGammaCases",
        "(float * float * float * float)[]",
        [(a, x, floored(value(gamma_p, a, x)), floored(value(gamma_q, a, x))) for a, x in incgamma_axs],
    )


SECTIONS = {"gamma": section_gamma, "erf": section_erf, "incgamma": section_incgamma}

if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in SECTIONS:
        sys.exit(f"usage: python -I tests/reference/special.py <section>, the section one of: {', '.join(SECTIONS)}")
    SECTIONS[sys.argv[1]]()
