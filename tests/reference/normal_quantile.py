# The standard normal quantile at p = Decimal(0.975), the exact value of the double nearest 0.975: the x with
# Phi(x) = p, found by bisection on [0, 10] with Phi evaluated in decimal arithmetic at 60 significant digits. Prints x
# rounded to the nearest double, with repr.
#
# Run: python -I tests/reference/normal_quantile.py
#
# Phi(x) = 1/2 + phi(x) * sum over n >= 0 of x^(2n+1) / (1 * 3 * ... * (2n+1)), phi(x) = exp(-x^2/2) / sqrt(2 pi), a
# series of positive terms for x > 0 (general knowledge; checked below against math.erfc in double precision).
# statistics.NormalDist is not used: it works in double precision.

import math
from decimal import Decimal, localcontext

DIGITS = 60


def pi() -> Decimal:
    """pi to the context's precision, by the recipe in the documentation of the decimal module."""
    with localcontext() as ctx:
        ctx.prec += 2
        three = Decimal(3)
        lasts, t, s, n, na, d, da = 0, three, 3, 1, 0, 0, 24
        while s != lasts:
            lasts = s
            n, na = n + na, na + 8
            d, da = d + da, da + 32
            t = (t * n) / d
            s += t
    return +s


def phi_cdf(x: Decimal, root_two_pi: Decimal) -> Decimal:
    """The standard normal distribution function at x >= 0, by the series above."""
    term = x
    total = x
    n = 0
    while True:
        n += 1
        term = term * x * x / (2 * n + 1)
        if total + term == total:
            break
        total += term
    return Decimal(1) / 2 + (-(x * x) / 2).exp() / root_two_pi * total


with localcontext() as ctx:
    ctx.prec = DIGITS
    p = Decimal(0.975)
    root_two_pi = (2 * pi()).sqrt()
    lo, hi = Decimal(0), Decimal(10)
    for _ in range(400):
        mid = (lo + hi) / 2
        if mid == lo or mid == hi:
            break
        if phi_cdf(mid, root_two_pi) < p:
            lo = mid
        else:
            hi = mid
    x = (lo + hi) / 2

z = float(x)
# The series against the library's erfc, in double precision: Phi(z) = erfc(-z / sqrt 2) / 2.
assert abs(0.5 * math.erfc(-z / math.sqrt(2.0)) - 0.975) < 1e-15
print(repr(z))
