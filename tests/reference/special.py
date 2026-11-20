"""Reference values for Ranvier.Numerics.Special, printed as F# literals.

Run: python -I tests/reference/special.py <section>

Sections:
    gamma   ln|Gamma|, Gamma, psi, B and ln B (with pairs where |ln B| >= 256), ln n!, ln C(n, k), ln(1 + x) and
            e^x - 1 at negative x; the constants and series coefficients of Special.fs
    erf     erf and erfc; the constants 2 / sqrt(pi) and 1 / sqrt(pi) of Special.fs
    incgamma
            the regularised incomplete gamma functions P(a, x) and Q(a, x), printed as 0.0 below 1e-300
    incbeta the regularised incomplete beta function I_x(a, b), each value at least 1e-300
    normal  the standard normal cdf and survival, the cdf of N(3, 2) and the standard normal quantile, as struct tuples
    lognormal
            the cdf and survival of the log-normal distribution with mu = 0.5 and sigma = 0.8
    zeta    the Riemann zeta function at n = 2 to 30, as doubles
    weibull the Weibull variance scale^2 (Gamma(1 + 2/k) - Gamma(1 + 1/k)^2) and the Weibull survival function
    dist-gamma, dist-chi-squared, dist-beta, dist-student-t, dist-fisher-f
            the cdf, survival and density of the gamma, chi-squared, beta, Student t and F distributions at the
            points of ContinuousTests.fs, and their quantiles at p = 0.05 and 0.975
    dist-regimes
            the same for the t at 100 degrees of freedom, the beta at shape 0.5 near 0 and the F with a degree of
            freedom of 0.5 far into either tail, where a single form would cancel or round 1 - x
    dist-edges
            the same at the ends of the parameter ranges that Continuous.fs states for the five families
    discrete
            the mass, cdf and survival of the binomial, Poisson, negative binomial and hypergeometric distributions
            at the points of DiscreteTests.fs

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
    I_x(a, b)   beta_inc: for 0 < x <= 1/2, x^a (1 - x)^b / (a B(a, b)) times the sum over n >= 0 of
                (a + b)_n / (a + 1)_n x^n, with (q)_n = q (q + 1) ... (q + n - 1). The terms are positive, and the ratio
                of each to the one before, x (a + b + n) / (a + 1 + n), moves monotonically towards x; so once
                r = max(the next ratio, x) is below 1, the remainder is below the last term times r / (1 - r), and the
                sum stops when that bound leaves it unchanged. The exponent a ln x + b ln(1 - x) - ln B(a, b) is formed
                at the context precision plus GUARD plus the digits of its largest term, ln(1 - x) at as many more
                digits as x has leading zeros, and ln B(a, b) from ln_gamma_precise, which holds at any precision. For
                x > 1/2, 1 - I_(1 - x)(b, a) with 1 - x exact, at 30 more digits; where the subtraction leaves fewer
                digits than the context's, it is computed again as Q(a, x) is. For whole a and b with a + b <= 200 the
                section also sums I_x(a, b) = the sum over j = a..a+b-1 of C(a + b - 1, j) x^j (1 - x)^(a + b - 1 - j)
                in exact fractions, and exits 1 unless both give the same double.
    normal_sf   Q(1/2, z^2 / 2) / 2 for z >= 0 and 1 - normal_sf(-z) below, from gamma_q at the context precision plus
                GUARD; normal_cdf(z) = 1 - normal_sf(z) for z >= 0 and normal_sf(-z) below.
    invert      200 bisection steps on an increasing function between two bounds that bracket the target (asserted);
                the bracket of width w shrinks to w 2^-200.
    emit        prints a table of struct tuples, each value as repr(float(value)).
    zeta(n)     Euler-Maclaurin summation at N = 40: the sum of k^-n for k < N, N^(1-n)/(n-1), N^-n/2 and the sum over
                j = 1..25 of B(2j)/(2j)! (n)_(2j-1) N^(-n-2j+1), (n)_m the rising factorial; asserted equal to the same sum
                at N = 50 to 1e-55, and for even n to (-1)^(n/2+1) B(n) (2 pi)^n / (2 n!) to 1e-55.
    Weibull     variance from exp(ln_gamma(1 + 2/k)) - exp(ln_gamma(1 + 1/k))^2 at the context precision plus GUARD + 20
                digits (the difference cancels about 2 log10(k) digits); survival exp(-(x/scale)^k) at 20 more digits.
    gamma(k, s) cdf P(k, x/s) and survival Q(k, x/s) from gamma_p and gamma_q; density
                exp((k - 1) ln t - t - ln Gamma(k) - ln s) with t = x/s. Chi-squared(n) is gamma(n/2, 2).
    beta(a, b)  cdf I_x(a, b) and survival I_(1 - x)(b, a) from beta_inc; density
                exp((a - 1) ln x + (b - 1) ln(1 - x) - ln B(a, b)).
    t(n)        P(T > |t|) = I_x(n/2, 1/2) / 2 with x = n / (n + t^2), from beta_inc; the cdf is that tail below 0 and
                1 minus it above, the survival the reverse; density
                exp(-((n + 1)/2) ln(1 + t^2/n) - ln(n)/2 - ln B(n/2, 1/2)).
    F(m, n)     cdf I_w(m/2, n/2) with w = m x / (m x + n) and survival I_(1 - w)(n/2, m/2) with 1 - w = n / (n + m x),
                from beta_inc; density
                exp((m/2) ln(m/n) + (m/2 - 1) ln x - ((m + n)/2) ln(1 + m x/n) - ln B(m/2, n/2)).
    densities   each exponent at the context precision plus GUARD + 4 digits (exp_of), so that its terms, below 10^4,
                keep the context's precision in absolute terms.
    quantiles   invert on the cdf for p <= 1/2 and on minus the survival against -(1 - p) above, over a bracket
                asserted by invert.
    Each of these sections first checks beta_inc against I_x(1, 1) = x at x = 0.3 and 0.8 and against
    I_0.3(2.5, 3.5) + I_0.7(3.5, 2.5) = 1, to 1e-50 at 60 digits.
    Poisson sums
                for the chi-squared at 1e5 degrees of freedom, whose shape 5e4 is whole, Q(n, t) is the finite sum over
                k = 0..n-1 of e^-t t^k / k! and P(n, t) the sum over k >= n, from its first term by logarithms, until a
                term leaves it unchanged past k = t (general knowledge: the Poisson identity for a whole shape). Both
                sums are of positive terms. gamma_q forms 1 - P at x / ln 10 extra digits, out of reach at x near 1e5.
                dist-edges first checks both sums against gamma_q and gamma_p at (3, 2.5) and (40, 37) to 1e-50.
    discrete    exact fractions at the exact value of each double p, where the masses are rational: the binomial
                C(n, k) p^k (1 - p)^(n - k), the negative binomial at a whole r, C(k + r - 1, k) p^r (1 - p)^k, and the
                hypergeometric C(K, k) C(N - K, n - k) / C(N, n); the cdf is the exact sum of the masses at and below
                k and the survival 1 minus it, each rounded once to a double (float of a fraction rounds correctly).
                In decimal where they are not: the Poisson mass e^-lambda lambda^k / k!, its cdf and survival from the
                Poisson sums above (poisson_head and poisson_tail); the negative binomial mass at a real r,
                prod_(j<k) (r + j) / k! p^r (1 - p)^k, its cdf the sum of those masses and its survival 1 minus that
                sum at 30 more digits. The section first checks the Poisson sums as dist-edges does, and the decimal
                negative binomial mass at r = 3 against the exact fraction at k = 0, 5 and 15, to 1e-50.

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


def magnitude_digits(*values: float) -> int:
    """The digits in the integer part of the largest |value|, at least 1."""
    return math.floor(math.log10(max(1.0, *(abs(v) for v in values)))) + 1


def beta_ln_precise(a: Decimal, b: Decimal) -> Decimal:
    """ln B(a, b) = ln Gamma(a) + ln Gamma(b) - ln Gamma(a + b) for a, b > 0 at any context precision p: absolute error
    below 10^-p, from ln_gamma_precise at the digits of its largest term more."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        s = float(a + b)
        # |ln Gamma(z)| is below |ln z| + 1 for z < 2 and below z ln z for z >= 2.
        ctx.prec += magnitude_digits(math.log(float(a)) - 1, math.log(float(b)) - 1, s * math.log(s) - 1)
        result = ln_gamma_precise(a) + ln_gamma_precise(b) - ln_gamma_precise(a + b)
    return +result


def incomplete_beta_sum(a: Decimal, b: Decimal, x: Decimal) -> Decimal:
    """The sum over n >= 0 of (a + b)_n / (a + 1)_n x^n, at the precision of the current context, for a, b > 0 and
    0 < x <= 1/2."""
    term = Decimal(1)
    total = term
    n = 0
    while True:
        term = term * x * (a + b + n) / (a + 1 + n)
        n += 1
        total += term
        # The ratio of each term to the one before moves monotonically towards x, so every later ratio is below r.
        r = max(x * (a + b + n) / (a + 1 + n), x)
        if r < 1 and total + term * r / (1 - r) == total:
            return total


def beta_inc_lower(a: Decimal, b: Decimal, x: Decimal) -> Decimal:
    """I_x(a, b) for a, b > 0 and 0 < x <= 1/2, by its series."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        # The exponent's terms reach |a ln x|, |b ln(1 - x)| and |ln B(a, b)|; it needs absolute precision.
        fa, fb, fx = float(a), float(b), float(x)
        s = fa + fb
        ln_b_bound = abs(math.log(fa)) + abs(math.log(fb)) + s * math.log(s + 2) + 2
        ctx.prec += magnitude_digits(fa * math.log(fx), fb * math.log1p(-fx), ln_b_bound)
        with localcontext() as inner:
            # 1 - x keeps the relative precision of x.
            inner.prec += max(0, -x.adjusted())
            ln_y = (1 - x).ln()
        exponent = a * x.ln() + b * ln_y - beta_ln_precise(a, b)
        result = exponent.exp() * incomplete_beta_sum(a, b, x) / a
    return +result


def beta_inc(a, b, x) -> Decimal:
    """I_x(a, b), the regularised incomplete beta function, for a, b > 0 and 0 <= x <= 1."""
    a, b, x = Decimal(a), Decimal(b), Decimal(x)
    if x == 0:
        return Decimal(0)
    if x == 1:
        return Decimal(1)
    if x <= Decimal("0.5"):
        return beta_inc_lower(a, b, x)
    extra = 30
    while True:
        with localcontext() as ctx:
            ctx.prec += extra
            # x has at most 53 digits after the point, so 1 - x is exact.
            result = 1 - beta_inc_lower(b, a, 1 - x)
        # The subtraction leaves extra + result.adjusted() digits more than the context's.
        if result == 0:
            extra *= 2
        elif extra + result.adjusted() < 0:
            extra = 20 - result.adjusted()
        else:
            return +result


def beta_inc_exact(a: int, b: int, x: float) -> Fraction:
    """I_x(a, b) for whole a, b >= 1, exactly: the sum over j = a..a+b-1 of C(a+b-1, j) x^j (1 - x)^(a + b - 1 - j)."""
    n = a + b - 1
    x = Fraction(x)
    return sum(math.comb(n, j) * x**j * (1 - x) ** (n - j) for j in range(a, n + 1))


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


def incbeta_rows(abxs: list[tuple[float, float, float]]) -> list[tuple]:
    """(a, b, x, I_x(a, b)) for each triple; exits 1 where I_x(a, b) is below 1e-300, or where whole a and b with
    a + b <= 200 give another double by the exact binomial sum."""
    rows = []
    for a, b, x in abxs:
        v = value(beta_inc, a, b, x)
        if v < 1e-300:
            sys.exit(f"beta_inc{(a, b, x)} = {v!r} is below 1e-300")
        if a == int(a) and b == int(b) and a + b <= 200:
            exact = float(beta_inc_exact(int(a), int(b), x))
            if exact != v:
                sys.exit(f"beta_inc{(a, b, x)}: {v!r} by the series, {exact!r} by the exact binomial sum")
        rows.append((a, b, x, v))
    return rows


def section_incbeta() -> None:
    print("// python -I tests/reference/special.py incbeta")
    print()

    incbeta_abxs = [
        (1.0, 1.0, 0.3), (0.5, 0.5, 0.2), (2.0, 3.0, 0.4), (5.0, 10.0, 0.1), (10.0, 5.0, 0.9), (0.1, 10.0, 0.01),
        (50.0, 50.0, 0.5), (50.0, 60.0, 0.4), (200.0, 300.0, 0.4), (0.001, 0.001, 0.5), (30.0, 2.0, 0.01),
        (1000.0, 1000.0, 0.49), (0.5, 1000.0, 1e-4), (7.5, 0.25, 0.999),
    ]
    print_cases("incompleteBetaCases", "(float * float * float * float)[]", incbeta_rows(incbeta_abxs))

    incbeta_scan_abxs = [
        # The worst points of a dense seeded scan of 7,748 points on .NET and JavaScript: below the switch by the
        # continued fraction for a, b >= 8, for one of them below 8 and for both below 8; above it by
        # 1 - I_(1 - x)(b, a) for the same three, and by the split for b < 1 with a above and below 8.
        (26.47726446673045, 122.94025167092182, 1.0975185646430612e-11),
        (38.04870023325283, 530.2670642127772, 3.682337338018041e-09),
        (526.934562703818, 377.3827200800417, 0.11432183348485359),
        (648.9439616655302, 0.13591441112284136, 0.3901655144016795),
        (1.33887962351026, 3.0156634547318433, 3.393673180998573e-171),
        (187.03445169233888, 676.1498087962242, 0.21758279285097282),
        (96.77412815163682, 5.399004386837298, 0.9463164204577453),
        (6.729458999421565, 1.0893450340321005, 0.7964308989536404),
        (689.6875600619811, 1.525515418979068e-10, 0.9987418730036892),
        (0.06574199246585886, 0.01713668015682447, 0.524342739714624),
        # The worst point before the rounding of a + b, a/(a + b) and b/(a + b) was carried exactly.
        (802.5335441586208, 364.46238711754455, 0.31261349811999317),
        # Where the method as first written missed the bound: (x - x0) / x0 near -1 loses x for a, b >= 8, and
        # 1 - I_(1 - x)(b, a) cancels for small b (at the split's worst point above it gave 7.4e7 times the bound).
        (12.160964710731095, 36.5970243760941, 7.663753456911437e-19), (2.0, 1e-300, 0.9),
        # The ends of the double range: x = 5e-324, x = 1 - 2^-53, x = 2^-53, a = 5e-324 and a just below 1000.
        (0.5, 0.5, 5e-324), (0.25, 1000.0, 5e-324), (1000.0, 1e-10, 0.9999999999999999), (1e-10, 1000.0, 2**-53),
        (5e-324, 1.0, 0.5), (999.9999999999999, 1000.0, 0.5),
    ]
    print_cases("incompleteBetaScanCases", "(float * float * float * float)[]", incbeta_rows(incbeta_scan_abxs))


def normal_sf(z) -> Decimal:
    """The standard normal survival function, Q(1/2, z^2 / 2) / 2 for z >= 0 and 1 - normal_sf(-z) below."""
    z = Decimal(z)
    if z < 0:
        return 1 - normal_sf(-z)
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = gamma_q(Decimal(1) / 2, z * z / 2) / 2
    return +result


def normal_cdf(z) -> Decimal:
    """The standard normal distribution function, 1 - normal_sf(z) for z >= 0 and normal_sf(-z) below."""
    z = Decimal(z)
    if z < 0:
        return normal_sf(-z)
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = 1 - normal_sf(z)
    return +result


def invert(f, target, lo, hi) -> Decimal:
    """x in [lo, hi] with f(x) = target, f increasing, by 200 bisection steps; exits 1 unless the bounds bracket it."""
    lo, hi, target = Decimal(lo), Decimal(hi), Decimal(target)
    if not f(lo) <= target <= f(hi):
        sys.exit(f"invert: the target {target} is not bracketed by [{lo}, {hi}]")
    for _ in range(200):
        mid = (lo + hi) / 2
        if f(mid) < target:
            lo = mid
        else:
            hi = mid
    return (lo + hi) / 2


def emit(name: str, rows: list[tuple]) -> None:
    """Prints `let private name: struct (float * ...)[]` with one struct tuple per row."""
    fields = " * ".join("float" for _ in rows[0])
    print(f"let private {name}: struct ({fields})[] =")
    print("    [|")
    for row in rows:
        print("        struct (" + ", ".join(literal(v) for v in row) + ")")
    print("    |]")
    print()


def check_normal_helpers() -> None:
    """Exits 1 unless the helpers' building blocks meet four identities to 1e-50 at 60 digits."""
    with localcontext() as ctx:
        ctx.prec = DIGITS
        tolerance = Decimal("1e-50")
        checks = [
            ("normal_cdf(0) = 1/2", normal_cdf(0) - Decimal("0.5")),
            ("gamma_p(1, 0.3) = 1 - e^-0.3", gamma_p(1, 0.3) - (1 - (-Decimal(0.3)).exp())),
            ("gamma_p(1, 7) = 1 - e^-7", gamma_p(1, 7.0) - (1 - (-Decimal(7)).exp())),
            ("exp(ln_gamma(10)) = 362880", ln_gamma(10.0).exp() - 362880),
            ("ln_gamma(0.5) = ln sqrt(pi)", ln_gamma(0.5) - pi().sqrt().ln()),
        ]
        for label, difference in checks:
            if abs(difference) > tolerance:
                sys.exit(f"check failed: {label}, off by {difference}")


def normal_quantile(p: float) -> Decimal:
    """x with normal_cdf(x) = p, bisecting the cdf for p <= 1/2 and the survival against 1 - Decimal(p) above."""
    if p <= 0.5:
        return invert(normal_cdf, Decimal(p), -40, 0)
    return invert(lambda x: -normal_sf(x), -(1 - Decimal(p)), 0, 40)


def section_normal() -> None:
    print("// python -I tests/reference/special.py normal")
    print()
    check_normal_helpers()

    xs = [-37.0, -30.0, -10.0, -5.0, -1.96, -1.0, -0.5, 0.0, 0.3, 1.0, 1.96, 3.0, 5.0, 8.0, 10.0, 30.0, 37.0]
    emit("normalCases", [(x, value(normal_cdf, x), value(normal_sf, x)) for x in xs])

    # N(3, 2): z = (x - 3) / 2 is exact for these x.
    shifted_xs = [-5.0, 1.0, 3.0, 7.5]
    emit(
        "normalShiftedCases",
        [(x, value(lambda x: normal_cdf((Decimal(x) - 3) / 2), x)) for x in shifted_xs],
    )

    ps = [1e-300, 1e-100, 1e-10, 0.025, 0.3, 0.975, 1 - 1e-10]
    emit("normalQuantileCases", [(p, value(normal_quantile, p)) for p in ps])


def lognormal_z(x) -> Decimal:
    """(ln x - mu) / sigma at mu = 0.5 and sigma = 0.8, each the exact value of the double."""
    return (Decimal(x).ln() - Decimal(0.5)) / Decimal(0.8)


def lognormal_cdf(x) -> Decimal:
    return normal_cdf(lognormal_z(x))


def lognormal_sf(x) -> Decimal:
    return normal_sf(lognormal_z(x))


def section_lognormal() -> None:
    print("// python -I tests/reference/special.py lognormal")
    print()
    check_normal_helpers()

    xs = [1e-10, 0.1, 1.0, 2.0, 10.0, 1000.0]
    emit("logNormalCases", [(x, value(lognormal_cdf, x), value(lognormal_sf, x)) for x in xs])


def zeta_sum(n: int, big_n: int) -> Decimal:
    """zeta(n) by Euler-Maclaurin summation at big_n, for n >= 2."""
    total = sum((Decimal(k) ** -n for k in range(1, big_n)), Decimal(0))
    total += Decimal(big_n) ** (1 - n) / (n - 1) + Decimal(big_n) ** -n / 2
    rising = Decimal(n)
    for j in range(1, 26):
        if j > 1:
            rising *= (n + 2 * j - 3) * (n + 2 * j - 2)
        total += dec(B[2 * j] / math.factorial(2 * j)) * rising * Decimal(big_n) ** (-n - 2 * j + 1)
    return total


def zeta_value(n: int) -> Decimal:
    """zeta(n) for an integer n >= 2, checked against N = 50 and, for even n, against the Bernoulli closed form."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = zeta_sum(n, 40)
        if abs(result - zeta_sum(n, 50)) > Decimal("1e-55"):
            sys.exit(f"zeta({n}): N = 40 and N = 50 differ")
        if n % 2 == 0:
            closed = (-1) ** (n // 2 + 1) * dec(B[n]) * (2 * pi()) ** n / (2 * math.factorial(n))
            if abs(result - closed) > Decimal("1e-55") * result:
                sys.exit(f"zeta({n}): {result} against the Bernoulli closed form {closed}")
    return +result


def section_zeta() -> None:
    print("// python -I tests/reference/special.py zeta")
    print()
    print("// zeta(n), n = 2..30:")
    print("let private zetaValues: float[] =")
    print("    [|")
    for n in range(2, 31):
        print(f"        {value(zeta_value, n)!r} // {n}")
    print("    |]")
    print()


def weibull_variance(k, scale) -> Decimal:
    """scale^2 (Gamma(1 + 2/k) - Gamma(1 + 1/k)^2) from ln_gamma."""
    k, scale = Decimal(k), Decimal(scale)
    with localcontext() as ctx:
        ctx.prec += GUARD + 20
        g1 = ln_gamma(1 + 1 / k).exp()
        g2 = ln_gamma(1 + 2 / k).exp()
        result = scale * scale * (g2 - g1 * g1)
    return +result


def weibull_survival(k, scale, x) -> Decimal:
    """exp(-(x / scale)^k) at the exact values of the doubles."""
    with localcontext() as ctx:
        ctx.prec += 20
        result = (-((Decimal(x) / Decimal(scale)) ** Decimal(k))).exp()
    return +result


def section_weibull() -> None:
    print("// python -I tests/reference/special.py weibull")
    print()
    ks = [10.0, 100.0, 1e4, 1e8]
    scales = [1.0, 1.7]
    emit("weibullVarianceCases", [(k, sc, value(weibull_variance, k, sc)) for k in ks for sc in scales])

    xs = {20.0: [1.2, 1.6, 1.7, 1.8, 2.0, 2.2], 1000.0: [1.69, 1.7, 1.7017, 1.705]}
    emit(
        "weibullSurvivalCases",
        [(k, 1.7, x, value(weibull_survival, k, 1.7, x)) for k, row in xs.items() for x in row],
    )


def check_distribution_helpers() -> None:
    """Exits 1 unless beta_inc meets three identities to 1e-50 at 60 digits."""
    with localcontext() as ctx:
        ctx.prec = DIGITS
        tolerance = Decimal("1e-50")
        checks = [
            ("beta_inc(1, 1, 0.3) = 0.3", beta_inc(1, 1, 0.3) - Decimal(0.3)),
            ("beta_inc(1, 1, 0.8) = 0.8", beta_inc(1, 1, 0.8) - Decimal(0.8)),
            (
                "beta_inc(2.5, 3.5, 0.3) + beta_inc(3.5, 2.5, 0.7) = 1",
                beta_inc(2.5, 3.5, Decimal("0.3")) + beta_inc(3.5, 2.5, Decimal("0.7")) - 1,
            ),
        ]
        for label, difference in checks:
            if abs(difference) > tolerance:
                sys.exit(f"check failed: {label}, off by {difference}")


def distribution_quantile(cdf, sf, p: float, lo, hi) -> Decimal:
    """x with cdf(x) = p for p <= 1/2, and with sf(x) = 1 - Decimal(p) above, by invert over [lo, hi]."""
    if p <= 0.5:
        return invert(cdf, Decimal(p), lo, hi)
    return invert(lambda x: -sf(x), -(1 - Decimal(p)), lo, hi)


def exp_of(exponent) -> Decimal:
    """exp of a callable's result, the callable run at the context precision plus GUARD plus 4 digits, so that terms
    below 10^4 in magnitude keep the context's precision in absolute terms."""
    with localcontext() as ctx:
        ctx.prec += GUARD + 4
        result = exponent().exp()
    return +result


def gamma_cdf(k, scale, x) -> Decimal:
    return gamma_p(k, Decimal(x) / Decimal(scale))


def gamma_sf(k, scale, x) -> Decimal:
    return gamma_q(k, Decimal(x) / Decimal(scale))


def gamma_density(k, scale, x) -> Decimal:
    """x^(k - 1) e^(-x/scale) / (Gamma(k) scale^k)."""
    k, scale, x = Decimal(k), Decimal(scale), Decimal(x)

    def exponent() -> Decimal:
        t = x / scale
        return (k - 1) * t.ln() - t - ln_gamma_precise(k) - scale.ln()

    return exp_of(exponent)


def gamma_quantile(k, scale, p) -> Decimal:
    hi = 10 * (k + 10 * math.sqrt(k) + 10) * scale
    return distribution_quantile(
        lambda x: gamma_cdf(k, scale, x), lambda x: gamma_sf(k, scale, x), p, 0, hi
    )


def section_dist_gamma() -> None:
    print("// python -I tests/reference/special.py dist-gamma")
    print()
    check_distribution_helpers()
    points = {
        (2.5, 2.0): [1e-5, 0.1, 1.0, 5.0, 20.0, 100.0],
        (0.5, 1.0): [1e-10, 0.01, 0.5, 3.0, 50.0],
        (100.0, 1.0): [60.0, 90.0, 100.0, 110.0, 150.0],
    }
    emit(
        "gammaCases",
        [
            (k, s, x, value(gamma_cdf, k, s, x), value(gamma_sf, k, s, x), value(gamma_density, k, s, x))
            for (k, s), xs in points.items()
            for x in xs
        ],
    )
    emit("gammaQuantileCases", [(k, s, p, value(gamma_quantile, k, s, p)) for k, s in points for p in (0.05, 0.975)])


def section_dist_chi_squared() -> None:
    print("// python -I tests/reference/special.py dist-chi-squared")
    print()
    check_distribution_helpers()
    points = {3.0: [0.01, 1.0, 3.0, 10.0, 50.0], 10.0: [1.0, 10.0, 30.0, 100.0]}
    # Chi-squared(n) is gamma(n/2, 2); n/2 is exact.
    emit(
        "chiSquaredCases",
        [
            (
                n,
                x,
                value(gamma_cdf, n / 2, 2.0, x),
                value(gamma_sf, n / 2, 2.0, x),
                value(gamma_density, n / 2, 2.0, x),
            )
            for n, xs in points.items()
            for x in xs
        ],
    )
    emit("chiSquaredQuantileCases", [(n, p, value(gamma_quantile, n / 2, 2.0, p)) for n in points for p in (0.05, 0.975)])


def beta_cdf(a, b, x) -> Decimal:
    return beta_inc(a, b, x)


def beta_sf(a, b, x) -> Decimal:
    # 1 - x rounds only beyond the context's 60 or 80 digits, far below the precision of a double.
    return beta_inc(b, a, 1 - Decimal(x))


def beta_density(a, b, x) -> Decimal:
    """x^(a - 1) (1 - x)^(b - 1) / B(a, b)."""
    a, b, x = Decimal(a), Decimal(b), Decimal(x)
    return exp_of(lambda: (a - 1) * x.ln() + (b - 1) * (1 - x).ln() - beta_ln(a, b))


def beta_quantile(a, b, p) -> Decimal:
    return distribution_quantile(lambda x: beta_cdf(a, b, x), lambda x: beta_sf(a, b, x), p, 0, 1)


def section_dist_beta() -> None:
    print("// python -I tests/reference/special.py dist-beta")
    print()
    check_distribution_helpers()
    a, b = 2.5, 3.5
    xs = [1e-6, 0.1, 0.4, 0.8, 0.999]
    emit(
        "betaCases",
        [(a, b, x, value(beta_cdf, a, b, x), value(beta_sf, a, b, x), value(beta_density, a, b, x)) for x in xs],
    )
    emit("betaQuantileCases", [(a, b, p, value(beta_quantile, a, b, p)) for p in (0.05, 0.975)])


def student_t_tail(n, t) -> Decimal:
    """P(T > |t|) = I_x(n/2, 1/2) / 2 with x = n / (n + t^2)."""
    n, t = Decimal(n), Decimal(t)
    with localcontext() as ctx:
        ctx.prec += GUARD
        x = n / (n + t * t)
        result = beta_inc(n / 2, Decimal(1) / 2, x) / 2
    return +result


def student_t_cdf(n, t) -> Decimal:
    if Decimal(t) < 0:
        return student_t_tail(n, t)
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = 1 - student_t_tail(n, t)
    return +result


def student_t_sf(n, t) -> Decimal:
    return student_t_cdf(n, -Decimal(t))


def student_t_density(n, t) -> Decimal:
    """(1 + t^2/n)^(-(n + 1)/2) / (sqrt(n) B(n/2, 1/2))."""
    n, t = Decimal(n), Decimal(t)
    half = Decimal(1) / 2
    return exp_of(lambda: -(n + 1) / 2 * (1 + t * t / n).ln() - n.ln() / 2 - beta_ln(n / 2, half))


def student_t_quantile(n, p) -> Decimal:
    lo, hi = (-1000, 0) if p <= 0.5 else (0, 1000)
    return distribution_quantile(lambda t: student_t_cdf(n, t), lambda t: student_t_sf(n, t), p, lo, hi)


def section_dist_student_t() -> None:
    print("// python -I tests/reference/special.py dist-student-t")
    print()
    check_distribution_helpers()
    dofs = [2.5, 5.0, 30.0]
    ts = [-1000.0, -10.0, -2.5, -1.0, 0.4, 1.96, 4.0, 100.0]
    emit(
        "studentTCases",
        [
            (n, t, value(student_t_cdf, n, t), value(student_t_sf, n, t), value(student_t_density, n, t))
            for n in dofs
            for t in ts
        ],
    )
    emit("studentTQuantileCases", [(n, p, value(student_t_quantile, n, p)) for n in dofs for p in (0.05, 0.975)])


def fisher_f_cdf(m, n, x) -> Decimal:
    m, n, x = Decimal(m), Decimal(n), Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        w = m * x / (m * x + n)
        result = beta_inc(m / 2, n / 2, w)
    return +result


def fisher_f_sf(m, n, x) -> Decimal:
    m, n, x = Decimal(m), Decimal(n), Decimal(x)
    with localcontext() as ctx:
        ctx.prec += GUARD
        w = n / (n + m * x)
        result = beta_inc(n / 2, m / 2, w)
    return +result


def fisher_f_density(m, n, x) -> Decimal:
    """(m/n)^(m/2) x^(m/2 - 1) (1 + m x/n)^(-(m + n)/2) / B(m/2, n/2)."""
    m, n, x = Decimal(m), Decimal(n), Decimal(x)
    return exp_of(
        lambda: m / 2 * (m / n).ln() + (m / 2 - 1) * x.ln() - (m + n) / 2 * (1 + m * x / n).ln() - beta_ln(m / 2, n / 2)
    )


def fisher_f_quantile(m, n, p) -> Decimal:
    return distribution_quantile(lambda x: fisher_f_cdf(m, n, x), lambda x: fisher_f_sf(m, n, x), p, 0, 1000)


def section_dist_fisher_f() -> None:
    print("// python -I tests/reference/special.py dist-fisher-f")
    print()
    check_distribution_helpers()
    points = {(5.0, 10.0): [0.01, 0.5, 1.0, 3.0, 20.0, 1000.0], (2.0, 30.0): [0.1, 1.0, 5.0, 50.0]}
    emit(
        "fisherFCases",
        [
            (m, n, x, value(fisher_f_cdf, m, n, x), value(fisher_f_sf, m, n, x), value(fisher_f_density, m, n, x))
            for (m, n), xs in points.items()
            for x in xs
        ],
    )
    emit("fisherFQuantileCases", [(m, n, p, value(fisher_f_quantile, m, n, p)) for m, n in points for p in (0.05, 0.975)])


def poisson_head(n: int, t: Decimal) -> Decimal:
    """The sum over k = 0..n-1 of e^-t t^k / k!, which is Q(n, t) for a whole n >= 1, at the precision of the context."""
    term = (-t).exp()
    total = term
    for k in range(1, n):
        term = term * t / k
        total += term
    return total


def poisson_tail(n: int, t: Decimal) -> Decimal:
    """The sum over k >= n of e^-t t^k / k!, which is P(n, t) for a whole n >= 1, at the precision of the context:
    the term at k = n from its logarithm, then terms until one leaves the sum unchanged with k > t, past which each
    later term is below the one before."""
    with localcontext() as ctx:
        ctx.prec += GUARD + len(str(n)) + 2
        term = (n * t.ln() - t - ln_gamma_precise(n + 1)).exp()
        total = term
        k = n
        while True:
            k += 1
            term = term * t / k
            if total + term == total and k > t:
                break
            total += term
    return +total


def chi_squared_whole_cdf(n, x) -> Decimal:
    """P(n/2, x/2) for a whole n/2, by poisson_tail; gamma_p's series would need about n terms as well."""
    return poisson_tail(int(n) // 2, Decimal(x) / 2)


def chi_squared_whole_sf(n, x) -> Decimal:
    """Q(n/2, x/2) for a whole n/2, by poisson_head: gamma_q forms 1 - P at x / ln 10 extra digits, out of reach at
    x near 1e5."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = poisson_head(int(n) // 2, Decimal(x) / 2)
    return +result


def check_poisson_sums() -> None:
    """Exits 1 unless the Poisson sums equal gamma_q and gamma_p at a = 3, x = 2.5 and a = 40, x = 37 to 1e-50."""
    with localcontext() as ctx:
        ctx.prec = DIGITS
        tolerance = Decimal("1e-50")
        for n, x in ((3, 2.5), (40, 37.0)):
            t = Decimal(x)
            checks = [
                (f"poisson_head({n}, {x}) = gamma_q", poisson_head(n, t) - gamma_q(n, x)),
                (f"poisson_tail({n}, {x}) = gamma_p", poisson_tail(n, t) - gamma_p(n, x)),
            ]
            for label, difference in checks:
                if abs(difference) > tolerance:
                    sys.exit(f"check failed: {label}, off by {difference}")


def section_dist_regimes() -> None:
    print("// python -I tests/reference/special.py dist-regimes")
    print()
    check_distribution_helpers()
    # t(100) where 1/2 - I_y(1/2, 50)/2 would cancel, and either side of |t| = 1.
    emit(
        "studentTRegimeCases",
        [
            (n, t, value(student_t_cdf, n, t), value(student_t_sf, n, t), value(student_t_density, n, t))
            for n in [100.0]
            for t in [-9.0, -5.0, -1.01, -0.99, 0.99, 1.01]
        ],
    )
    # A shape below 1 at small x, where 1 - x rounds near 1.
    emit(
        "betaRegimeCases",
        [
            (a, b, x, value(beta_cdf, a, b, x), value(beta_sf, a, b, x), value(beta_density, a, b, x))
            for a, b in [(0.5, 100.0)]
            for x in [1e-20, 1e-10, 1e-6]
        ],
    )
    # F(100, 0.5) far above d2/d1 and F(0.5, 100) far below it, where w or 1 - w rounds near 1.
    points = {(100.0, 0.5): [1e3, 1e20], (0.5, 100.0): [1e-20, 1e-3]}
    emit(
        "fisherFRegimeCases",
        [
            (m, n, x, value(fisher_f_cdf, m, n, x), value(fisher_f_sf, m, n, x), value(fisher_f_density, m, n, x))
            for (m, n), xs in points.items()
            for x in xs
        ],
    )


def section_dist_edges() -> None:
    print("// python -I tests/reference/special.py dist-edges")
    print()
    check_distribution_helpers()
    check_poisson_sums()
    gamma_points = {
        (0.01, 1.0): [1e-100, 1e-10, 0.01, 1.0, 10.0, 100.0],
        (1000.0, 1.0): [700.0, 900.0, 1000.0, 1100.0, 1400.0],
    }
    emit(
        "gammaEdgeCases",
        [
            (k, s, x, value(gamma_cdf, k, s, x), value(gamma_sf, k, s, x), value(gamma_density, k, s, x))
            for (k, s), xs in gamma_points.items()
            for x in xs
        ],
    )
    chi_points = {
        0.05: [1e-100, 1e-10, 0.01, 1.0, 20.0, 100.0],
        0.5: [1e-10, 0.1, 1.0, 10.0, 100.0],
        100.0: [50.0, 80.0, 100.0, 130.0, 200.0],
    }
    chi_rows = [
        (n, x, value(gamma_cdf, n / 2, 2.0, x), value(gamma_sf, n / 2, 2.0, x), value(gamma_density, n / 2, 2.0, x))
        for n, xs in chi_points.items()
        for x in xs
    ]
    # 1e5 degrees of freedom: shape 5e4 is whole, so P and Q come from the Poisson sums.
    chi_rows += [
        (n, x, value(chi_squared_whole_cdf, n, x), value(chi_squared_whole_sf, n, x), value(gamma_density, n / 2, 2.0, x))
        for n in [100000.0]
        for x in [98000.0, 99500.0, 100000.0, 100500.0, 102000.0]
    ]
    emit("chiSquaredEdgeCases", chi_rows)
    beta_points = {
        (0.5, 0.5): [1e-100, 1e-10, 0.3, 0.9, 1 - 1e-10],
        (0.5, 100.0): [1e-100, 1e-10, 0.005, 0.05, 0.3],
        (100.0, 0.5): [0.7, 0.95, 0.995, 1 - 1e-10],
        (100.0, 100.0): [0.3, 0.45, 0.5, 0.55, 0.7],
        (0.2, 0.2): [1e-100, 1e-10, 0.5, 1 - 1e-10],
        (0.2, 500.0): [1e-100, 1e-10, 1e-4, 0.01, 0.05],
        (500.0, 0.2): [0.95, 0.99, 0.9999, 1 - 1e-10],
        (500.0, 500.0): [0.45, 0.48, 0.5, 0.52, 0.55],
    }
    emit(
        "betaEdgeCases",
        [
            (a, b, x, value(beta_cdf, a, b, x), value(beta_sf, a, b, x), value(beta_density, a, b, x))
            for (a, b), xs in beta_points.items()
            for x in xs
        ],
    )
    t_points = {
        0.05: [-1e100, -1e10, -10.0, -1.0, 0.5, 100.0],
        0.5: [-1e100, -1e10, -2.0, -0.5, 1.5, 1e5],
        100.0: [-30.0, -3.0, -0.5, 1.0, 4.0, 20.0],
        1000.0: [-30.0, -5.0, -1.2, 0.3, 2.0, 10.0],
    }
    emit(
        "studentTEdgeCases",
        [
            (n, t, value(student_t_cdf, n, t), value(student_t_sf, n, t), value(student_t_density, n, t))
            for n, ts in t_points.items()
            for t in ts
        ],
    )
    f_xs = [1e-10, 1e-3, 0.3, 3.0, 1e3, 1e10]
    f_pairs = [
        (0.5, 0.5), (0.5, 100.0), (100.0, 0.5), (100.0, 100.0),
        (1000.0, 0.5), (1000.0, 100.0), (0.5, 1000.0), (100.0, 1000.0),
    ]
    emit(
        "fisherFEdgeCases",
        [
            (m, n, x, value(fisher_f_cdf, m, n, x), value(fisher_f_sf, m, n, x), value(fisher_f_density, m, n, x))
            for m, n in f_pairs
            for x in f_xs
        ],
    )


def emit_typed(name: str, types: tuple[str, ...], rows: list[tuple]) -> None:
    """Prints `let private name: struct (types)[]` with one struct tuple per row; an int prints as written."""
    print(f"let private {name}: struct ({' * '.join(types)})[] =")
    print("    [|")
    for row in rows:
        print("        struct (" + ", ".join(literal(v) for v in row) + ")")
    print("    |]")
    print()


def exact_rows(masses: dict[int, Fraction], ks) -> list[tuple[int, float, float, float]]:
    """(k, mass, cdf, survival) at each k from exact masses that cover every j <= k of the support, each rounded once
    to a double; the survival is 1 minus the exact cdf."""
    rows = []
    for k in ks:
        cdf = sum((m for j, m in masses.items() if j <= k), Fraction(0))
        rows.append((k, float(masses.get(k, Fraction(0))), float(cdf), float(1 - cdf)))
    return rows


def binomial_masses(n: int, p: float) -> dict[int, Fraction]:
    """C(n, k) p^k (1 - p)^(n - k) for k = 0..n, exact at the double p."""
    q = Fraction(p)
    return {k: math.comb(n, k) * q**k * (1 - q) ** (n - k) for k in range(n + 1)}


def negative_binomial_masses(r: int, p: float, k_max: int) -> dict[int, Fraction]:
    """C(k + r - 1, k) p^r (1 - p)^k for k = 0..k_max and a whole r, exact at the double p."""
    q = Fraction(p)
    return {k: math.comb(k + r - 1, k) * q**r * (1 - q) ** k for k in range(k_max + 1)}


def hypergeometric_masses(population: int, successes: int, draws: int) -> dict[int, Fraction]:
    """C(K, k) C(N - K, n - k) / C(N, n) over the support max(0, n - (N - K))..min(n, K), exact."""
    total = math.comb(population, draws)
    lo, hi = max(0, draws - (population - successes)), min(draws, successes)
    return {
        k: Fraction(math.comb(successes, k) * math.comb(population - successes, draws - k), total)
        for k in range(lo, hi + 1)
    }


def poisson_mass(lam, k: int) -> Decimal:
    """e^-lambda lambda^k / k!."""
    t = Decimal(lam)
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = (-t).exp() * t**k / math.factorial(k)
    return +result


def poisson_cdf(lam, k: int) -> Decimal:
    """P(X <= k), the sum of the masses at 0..k, by poisson_head."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = poisson_head(k + 1, Decimal(lam))
    return +result


def poisson_sf(lam, k: int) -> Decimal:
    """P(X > k), the sum of the masses from k + 1 on, by poisson_tail."""
    return poisson_tail(k + 1, Decimal(lam))


def negative_binomial_mass(r, p, k: int) -> Decimal:
    """prod_(j<k) (r + j) / k! p^r (1 - p)^k, for real r > 0."""
    r, p = Decimal(r), Decimal(p)
    with localcontext() as ctx:
        ctx.prec += GUARD
        product = Decimal(1)
        for j in range(k):
            product *= r + j
        result = product / math.factorial(k) * (r * p.ln()).exp() * (1 - p) ** k
    return +result


def negative_binomial_cdf(r, p, k: int) -> Decimal:
    """P(X <= k), the sum of the masses at 0..k."""
    with localcontext() as ctx:
        ctx.prec += GUARD
        result = sum(negative_binomial_mass(r, p, j) for j in range(k + 1))
    return +result


def negative_binomial_sf(r, p, k: int) -> Decimal:
    """P(X > k) = 1 - P(X <= k), at 30 more digits than the context's."""
    with localcontext() as ctx:
        ctx.prec += 30
        result = 1 - negative_binomial_cdf(r, p, k)
    return +result


def negative_binomial_log_mass(r, p, k: int) -> Decimal:
    """ln Gamma(r + k) - ln Gamma(r) - ln k! + r ln p + k ln(1 - p), from ln_gamma_precise, at the context precision
    plus the digits of its largest term, so that it keeps the context's precision in absolute terms."""
    r, p = Decimal(r), Decimal(p)
    with localcontext() as ctx:
        z = float(r) + k
        ctx.prec += GUARD + magnitude_digits(z * math.log(z + 1), k * math.log(float(p)))
        result = ln_gamma_precise(r + k) - ln_gamma_precise(r) - ln_gamma_precise(k + 1) + r * p.ln() + k * (1 - p).ln()
    return +result


def negative_binomial_tail(r, p, k: int) -> Decimal:
    """P(X > k) for k past the mode: the masses from k + 1 on, the first from its logarithm and each next by the ratio
    (r + j)(1 - p) / (j + 1), until a term leaves the sum unchanged together with the bound term rho / (1 - rho) on the
    rest, rho = max(ratio, 1 - p) bounding every later ratio (the ratios fall towards 1 - p for r > 1 and rise towards
    it for r < 1)."""
    rr, pp = Decimal(r), Decimal(p)
    assert k > (r - 1) * (1 - p) / p, (r, p, k)
    with localcontext() as ctx:
        ctx.prec += GUARD
        term = negative_binomial_log_mass(r, p, k + 1).exp()
        total = term
        j = k + 1
        while True:
            ratio = (rr + j) * (1 - pp) / (j + 1)
            term *= ratio
            j += 1
            rho = max(ratio, 1 - pp)
            if total + term * rho / (1 - rho) == total:
                break
            total += term
    return +total


def negative_binomial_tail_mass(r, p, k: int) -> Decimal:
    """The mass from its logarithm, for k past the mode, where the product of negative_binomial_mass is long."""
    return negative_binomial_log_mass(r, p, k).exp()


def negative_binomial_tail_cdf(r, p, k: int) -> Decimal:
    """P(X <= k) = 1 - P(X > k) for k past the mode, at 30 more digits than the context's."""
    with localcontext() as ctx:
        ctx.prec += 30
        result = 1 - negative_binomial_tail(r, p, k)
    return +result


def geometric_mass(p, k: int) -> Decimal:
    """p (1 - p)^k, the power by its logarithm at the context precision plus the digits of the exponent."""
    t = Decimal(p)
    with localcontext() as ctx:
        ctx.prec += GUARD + magnitude_digits(k * math.log1p(-p))
        result = t * (k * (1 - t).ln()).exp()
    return +result


def geometric_sf(p, k: int) -> Decimal:
    """(1 - p)^(k + 1)."""
    t = Decimal(p)
    with localcontext() as ctx:
        ctx.prec += GUARD + magnitude_digits((k + 1) * math.log1p(-p))
        result = ((k + 1) * (1 - t).ln()).exp()
    return +result


def geometric_cdf(p, k: int) -> Decimal:
    """1 - (1 - p)^(k + 1), at 30 more digits than the context's."""
    with localcontext() as ctx:
        ctx.prec += 30
        result = 1 - geometric_sf(p, k)
    return +result


def check_negative_binomial_mass() -> None:
    """Exits 1 unless the decimal negative binomial mass at r = 3, p = 0.4 equals the exact fraction at k = 0, 5 and 15
    to 1e-50 at 60 digits."""
    exact = negative_binomial_masses(3, 0.4, 15)
    with localcontext() as ctx:
        ctx.prec = DIGITS
        tolerance = Decimal("1e-50")
        for k in (0, 5, 15):
            difference = negative_binomial_mass(3, 0.4, k) - dec(exact[k])
            if abs(difference) > tolerance:
                sys.exit(f"check failed: negative_binomial_mass(3, 0.4, {k}) = exact, off by {difference}")
        checks = [
            (
                "negative_binomial_log_mass(3, 0.4, 15) = ln exact",
                negative_binomial_log_mass(3, 0.4, 15) - dec(exact[15]).ln(),
            ),
            (
                "negative_binomial_tail(3, 0.4, 15) = 1 - exact cdf",
                negative_binomial_tail(3, 0.4, 15) - dec(1 - sum(exact.values())),
            ),
            (
                "geometric_mass(0.4, 7) = negative binomial at r = 1",
                geometric_mass(0.4, 7) - negative_binomial_log_mass(1, 0.4, 7).exp(),
            ),
            (
                "geometric_sf(0.4, 7) = negative binomial tail at r = 1",
                geometric_sf(0.4, 7) - negative_binomial_tail(1, 0.4, 7),
            ),
        ]
        for label, difference in checks:
            if abs(difference) > tolerance:
                sys.exit(f"check failed: {label}, off by {difference}")


def negative_binomial_row(r: float, p: float, k: int, head: bool | None = None) -> tuple:
    """(r, p, k, mass, cdf, survival): from the masses' head sum at and below the mode, or wherever head is True, and
    past the mode otherwise from the tail sum."""
    if head is None:
        head = k <= (r - 1) * (1 - p) / p
    if head:
        fs = (negative_binomial_mass, negative_binomial_cdf, negative_binomial_sf)
    else:
        fs = (negative_binomial_tail_mass, negative_binomial_tail_cdf, negative_binomial_tail)
    return (r, p, k) + tuple(value(f, r, p, k) for f in fs)


def section_discrete() -> None:
    print("// python -I tests/reference/special.py discrete")
    print()
    check_poisson_sums()
    check_negative_binomial_mass()
    emit_typed(
        "binomialCases",
        ("int", "float", "int", "float", "float", "float"),
        [(10, 0.3) + row for row in exact_rows(binomial_masses(10, 0.3), range(-1, 12))],
    )
    tail_points = {(1000, 0.5): [400, 450, 550, 600], (100, 0.01): [0, 3, 5, 10]}
    emit_typed(
        "binomialTailCases",
        ("int", "float", "int", "float", "float", "float"),
        [(n, p) + row for (n, p), ks in tail_points.items() for row in exact_rows(binomial_masses(n, p), ks)],
    )
    poisson_points = {4.0: list(range(21)) + [40], 1000.0: [900, 1100]}
    emit_typed(
        "poissonCases",
        ("float", "int", "float", "float", "float"),
        [
            (lam, k, value(poisson_mass, lam, k), value(poisson_cdf, lam, k), value(poisson_sf, lam, k))
            for lam, ks in poisson_points.items()
            for k in ks
        ],
    )
    whole = [(3.0, 0.4) + row for row in exact_rows(negative_binomial_masses(3, 0.4, 15), range(16))]
    real = [
        (r, p, k, value(negative_binomial_mass, r, p, k), value(negative_binomial_cdf, r, p, k),
         value(negative_binomial_sf, r, p, k))
        for r, p in [(2.5, 0.4)]
        for k in [0, 3, 10]
    ]
    emit_typed("negativeBinomialCases", ("float", "float", "int", "float", "float", "float"), whole + real)
    hypergeometric_points = {(50, 10, 5): range(6), (1000, 400, 100): [20, 40, 60]}
    emit_typed(
        "hypergeometricCases",
        ("int", "int", "int", "int", "float", "float", "float"),
        [
            (n, s, d) + row
            for (n, s, d), ks in hypergeometric_points.items()
            for row in exact_rows(hypergeometric_masses(n, s, d), ks)
        ],
    )
    # The ends of the domains that Discrete.fs states, and its worst points there.
    binomial_edges = {
        (1, 0.3): [0, 1],
        (1000, 1e-10): [0, 1, 2],
        (1000, 0.3): [300, 794],
        (1000, 0.999): [990, 999, 1000],
    }
    emit_typed(
        "binomialEdgeCases",
        ("int", "float", "int", "float", "float", "float"),
        [(n, p) + row for (n, p), ks in binomial_edges.items() for row in exact_rows(binomial_masses(n, p), ks)],
    )
    poisson_edges = {0.001: [0, 1, 10], 1000.0: [1000, 2374]}
    emit_typed(
        "poissonEdgeCases",
        ("float", "int", "float", "float", "float"),
        [
            (lam, k, value(poisson_mass, lam, k), value(poisson_cdf, lam, k), value(poisson_sf, lam, k))
            for lam, ks in poisson_edges.items()
            for k in ks
        ],
    )
    geometric_edges = {1e-10: [0, 2000000000], 0.999: [0, 99]}
    emit_typed(
        "geometricEdgeCases",
        ("float", "int", "float", "float", "float"),
        [
            (p, k, value(geometric_mass, p, k), value(geometric_cdf, p, k), value(geometric_sf, p, k))
            for p, ks in geometric_edges.items()
            for k in ks
        ],
    )
    negative_binomial_edges = {
        (0.5, 0.002): [0, 100, 270000],
        (100.0, 0.0909091): [1000, 5000],
        (0.5, 0.999): [0, 1, 5],
        (100.0, 0.999): [0, 1, 5],
    }
    emit_typed(
        "negativeBinomialEdgeCases",
        ("float", "float", "int", "float", "float", "float"),
        [negative_binomial_row(r, p, k) for (r, p), ks in negative_binomial_edges.items() for k in ks],
    )
    hypergeometric_edges = {(1000, 400, 500): [149, 200, 284], (1000, 10, 999): [9, 10]}
    emit_typed(
        "hypergeometricEdgeCases",
        ("int", "int", "int", "int", "float", "float", "float"),
        [
            (n, s, d) + row
            for (n, s, d), ks in hypergeometric_edges.items()
            for row in exact_rows(hypergeometric_masses(n, s, d), ks)
        ],
    )
    # Where e^-lambda lambda^k / k! from its logarithm loses more than 1e-12, and where the rounding of 1 - p does in
    # I_(1 - p)(k + 1, r): p = 0.00272 and 0.00938 round 1 - p by 2^-54, and 1 - 1e-17 rounds to 1, where the survival
    # at k = 5 is near 1 and comes from the head sum.
    poisson_regimes = {300.0: [858, 1040], 1000.0: [2374]}
    emit_typed(
        "poissonRegimeCases",
        ("float", "int", "float", "float", "float"),
        [
            (lam, k, value(poisson_mass, lam, k), value(poisson_cdf, lam, k), value(poisson_sf, lam, k))
            for lam, ks in poisson_regimes.items()
            for k in ks
        ],
    )
    emit_typed(
        "negativeBinomialRegimeCases",
        ("float", "float", "int", "float", "float", "float"),
        [negative_binomial_row(r, p, k) for r, p, k in [(0.5, 0.00272, 200000), (0.5, 0.00938, 60000)]]
        + [negative_binomial_row(0.5, 1e-17, 5, head=True)],
    )


SECTIONS = {
    "gamma": section_gamma,
    "erf": section_erf,
    "incgamma": section_incgamma,
    "incbeta": section_incbeta,
    "normal": section_normal,
    "lognormal": section_lognormal,
    "zeta": section_zeta,
    "weibull": section_weibull,
    "dist-gamma": section_dist_gamma,
    "dist-chi-squared": section_dist_chi_squared,
    "dist-beta": section_dist_beta,
    "dist-student-t": section_dist_student_t,
    "dist-fisher-f": section_dist_fisher_f,
    "dist-regimes": section_dist_regimes,
    "dist-edges": section_dist_edges,
    "discrete": section_discrete,
}

if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in SECTIONS:
        sys.exit(f"usage: python -I tests/reference/special.py <section>, the section one of: {', '.join(SECTIONS)}")
    SECTIONS[sys.argv[1]]()
