"""Reference values for Ranvier.Numerics.Special, printed as F# literals.

Run: python -I tests/reference/special.py <section>

Sections:
    gamma   ln|Gamma|, Gamma, psi, B and ln B (with pairs where |ln B| >= 256), ln n!, ln C(n, k); the constants and
            series coefficients of Special.fs

Every value is computed in decimal arithmetic at DIGITS significant digits from the exact value of each double input
(Decimal(x) of the float the test passes), then again at DIGITS + 20; the script exits 1 unless both round to the same
double. Each value is printed with repr(float(value)). The helpers work at the precision of the current decimal context
plus GUARD digits, and round their result to the context.

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
    ln n!       ln of math.factorial(n) for n <= 10^5, ln Gamma(n + 1) above.
    ln C(n, k)  ln of math.comb(n, k).

For real w > 0 the error of each truncated series is at most its first omitted term in magnitude (general knowledge:
DLMF 5.11(ii), not read here); the script asserts that term is below 1e-60, which holds for every w >= 40.
"""

import math
import sys
from decimal import Decimal, getcontext, localcontext
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
    """ln B(a, b) = ln Gamma(a) + ln Gamma(b) - ln Gamma(a + b), a, b > 0."""
    a, b = Decimal(a), Decimal(b)
    with localcontext() as ctx:
        ctx.prec += GUARD
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


def sqrt_two_pi() -> Decimal:
    return (2 * pi()).sqrt()


def half_ln_two_pi() -> Decimal:
    return (2 * pi()).ln() / 2


def ln_pi() -> Decimal:
    return pi().ln()


def value(f, *args) -> float:
    """f(*args) rounded to a double, computed at DIGITS and at CHECK_DIGITS; exits 1 unless both give the same double."""
    results = []
    for digits in (DIGITS, CHECK_DIGITS):
        with localcontext() as ctx:
            ctx.prec = digits
            results.append(float(f(*args)))
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
    ]
    print_cases("gammaLnCases", "(float * float)[]", [(x, value(ln_gamma, x)) for x in gamma_ln_xs])

    gamma_xs = [
        1e-300, 1e-8, 0.001, 0.1, 0.5, 1.5, 2.5, 3.7, 7.9, 10.1, 33.3, 100.5, 170.5, 171.6, -0.5, -1.5, -2.75, -10.3,
        -100.7, -150.3,
    ]
    print_cases("gammaCases", "(float * float)[]", [(x, value(gamma_value, x)) for x in gamma_xs])

    digamma_xs = [
        1e-8, 0.01, 0.5, 1.0, 1.4616321449683622, 1.5, 2.0, 2.5, 7.9, 10.0, 100.5, 1e6, 1e15, -0.5, -2.3, -10.7,
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


SECTIONS = {"gamma": section_gamma}

if __name__ == "__main__":
    if len(sys.argv) != 2 or sys.argv[1] not in SECTIONS:
        sys.exit(f"usage: python -I tests/reference/special.py <section>, the section one of: {', '.join(SECTIONS)}")
    SECTIONS[sys.argv[1]]()
