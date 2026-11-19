/// Four formula bodies copied from AstroShowcase, each as a float -> float with one input varied and
/// the rest fixed at the page's default control values. Array and tuple results are reduced to one float.
module Ranvier.Science.Benchmarks.Bodies

open System

// ---- Shared definitions, copied unchanged.

// AstroShowcase/Astro.fs:8-17
[<Struct>]
type V3 =
    { X: float
      Y: float
      Z: float }

    static member (+)(a: V3, b: V3) = { X = a.X + b.X; Y = a.Y + b.Y; Z = a.Z + b.Z }
    static member (-)(a: V3, b: V3) = { X = a.X - b.X; Y = a.Y - b.Y; Z = a.Z - b.Z }
    static member (*)(s: float, a: V3) = { X = s * a.X; Y = s * a.Y; Z = s * a.Z }
    member a.Norm = sqrt (a.X * a.X + a.Y * a.Y + a.Z * a.Z)

// AstroShowcase/Astro.fs:19
let v3 x y z = { X = x; Y = y; Z = z }

// AstroShowcase/Astro.fs:24-28
let muEarth = 398600.4418
let rEarth = 6378.137
let deg = Math.PI / 180.0

// ---- Body 1: Astro.keplerToState, AstroShowcase/Astro.fs:36-50

let keplerToState mu a e i raan argp nu =
    let p = a * (1.0 - e * e)
    let r = p / (1.0 + e * cos nu)
    let xp, yp = r * cos nu, r * sin nu
    let h = sqrt (mu * p)
    let vxp, vyp = -mu / h * sin nu, mu / h * (e + cos nu)
    let cO, sO, cw, sw, ci, si = cos raan, sin raan, cos argp, sin argp, cos i, sin i

    let map x y =
        v3
            ((cO * cw - sO * sw * ci) * x + (-cO * sw - sO * cw * ci) * y)
            ((sO * cw + cO * sw * ci) * x + (-sO * sw + cO * cw * ci) * y)
            (sw * si * x + cw * si * y)

    map xp yp, map vxp vyp

/// The x component of the position, varying the true anomaly [rad]. The other inputs are the defaults of the
/// Event location page (AstroShowcase/Pages.fs:1430-1433 and :1318): a = 30000 km, eccentricity 0.2,
/// inclination 55 deg, RAAN 35 deg, argument of perigee 50 deg.
let kepler (nu: float) =
    let state = keplerToState muEarth 30000.0 0.2 (55.0 * deg) (35.0 * deg) (50.0 * deg) nu
    (fst state).X

// ---- Body 2: one rk4 step of the rigid-body derivative, AstroShowcase/Astro.fs:72-79 and PagesFlight.fs:176-195

let rk4 (f: float[] -> float[]) (y: float[]) (h: float) =
    let add (a: float[]) s (b: float[]) = Array.init a.Length (fun i -> a[i] + s * b[i])
    let k1 = f y
    let k2 = f (add y (h / 2.0) k1)
    let k3 = f (add y (h / 2.0) k2)
    let k4 = f (add y h k3)
    Array.init y.Length (fun i -> y[i] + h / 6.0 * (k1[i] + 2.0 * k2[i] + 2.0 * k3[i] + k4[i]))

// AstroShowcase/PagesFlight.fs:149 with the Attitude page default p[1] = 3.0 (PagesFlight.fs:302)
let private inertia = 2.0, 3.0, 4.0

// The deriv closure of PagesFlight.fs:179-195, with the inertias taken from the line above.
let private rigidDeriv (s: float[]) =
    let i1, i2, i3 = inertia
    let wx, wy, wz = s[0], s[1], s[2]
    let qw, qx, qy, qz = s[3], s[4], s[5], s[6]

    [| (i2 - i3) / i1 * wy * wz
       (i3 - i1) / i2 * wz * wx
       (i1 - i2) / i3 * wx * wy
       0.5 * (-qx * wx - qy * wy - qz * wz)
       0.5 * (qw * wx + qy * wz - qz * wy)
       0.5 * (qw * wy + qz * wx - qx * wz)
       0.5 * (qw * wz + qx * wy - qy * wx) |]

/// The first element (the x body rate) after one rk4 step of h = 0.01 (the step cap of PagesFlight.fs:194),
/// varying the initial tilt [deg]. The initial state is that of begin' (PagesFlight.fs:167-175) at the page defaults:
/// spin about the intermediate axis (index 1), so w = (0, cos tilt, sin tilt) and the quaternion is the identity.
let rigidStep (tilt: float) =
    let t = tilt * deg
    let state = [| 0.0; cos t; sin t; 1.0; 0.0; 0.0; 0.0 |]
    (rk4 rigidDeriv state 0.01)[0]

// ---- Bodies 3 and 4: the drag decay model, AstroShowcase/PagesOrbit.fs:567-604

// AstroShowcase/PagesOrbit.fs:568-581
let private bands =
    [| 120.0, 2.438e-8, 9.473
       130.0, 8.484e-9, 12.636
       140.0, 3.845e-9, 16.149
       150.0, 2.070e-9, 22.523
       180.0, 5.464e-10, 29.740
       200.0, 2.789e-10, 37.105
       250.0, 7.248e-11, 45.546
       300.0, 2.418e-11, 53.628
       350.0, 9.518e-12, 53.298
       400.0, 3.725e-12, 58.515
       450.0, 1.585e-12, 60.828
       500.0, 6.967e-13, 63.822
       600.0, 1.454e-13, 71.835 |]

// AstroShowcase/PagesOrbit.fs:584-587
let private density (activity: int) (h: float) =
    let h0, rho0, hs = bands |> Array.findBack (fun (b, _, _) -> h >= b || b = 120.0)
    let k = [| 0.4; 1.0; 2.6 |][activity]
    rho0 * exp (-(h - h0) / hs) * (1.0 + (k - 1.0) * Math.Clamp((h - 120.0) / 180.0, 0.0, 1.0))

// AstroShowcase/PagesOrbit.fs:590
let private rateOf activity bc (h: float) = density activity h * sqrt (muEarth * 1e9 * (rEarth + h) * 1000.0) / bc

// AstroShowcase/PagesOrbit.fs:593-604
let private decayOf (h0: float) (bc: float) (activity: int) =
    let out = ResizeArray<float * float>()
    let mutable h, t = h0, 0.0
    out.Add(0.0, h0)

    while h > 120.0 do
        let dh = if h > 200.0 then 0.5 else 0.25
        t <- t + dh * 1000.0 / rateOf activity bc h
        h <- h - dh
        out.Add(t / 86400.0, h)

    out.ToArray()

/// The decay rate [m/s], varying the altitude [km]. The other inputs are the defaults of the Atmospheric Drag
/// Decay page (AstroShowcase/PagesOrbit.fs:688-689): ballistic coefficient 80 kg/m2, solar activity Moderate (1).
let rate (h: float) = rateOf 1 80.0 h

/// The lifetime in days, which is the last row's time as in the page's `lifetime` (PagesOrbit.fs:607), varying the
/// initial altitude [km]. The first row is the constant (0, h0), so reducing to it would time nothing. The other
/// inputs are the page defaults: ballistic coefficient 80 kg/m2, solar activity Moderate (1).
let decay (h0: float) = fst (Array.last (decayOf h0 80.0 1))
