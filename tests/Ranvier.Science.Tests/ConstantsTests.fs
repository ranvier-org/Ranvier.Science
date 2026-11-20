// Reference values: the CODATA 2022 ASCII table of all values,
// https://physics.nist.gov/cuu/Constants/Table/allascii.txt (linked from https://physics.nist.gov/cuu/Constants/),
// fetched 2026-11-20 and pasted as printed (value, uncertainty, unit):
//
//   speed of light in vacuum           299 792 458              (exact)                  m s^-1
//   Planck constant                    6.626 070 15 e-34        (exact)                  J Hz^-1
//   reduced Planck constant            1.054 571 817... e-34    (exact)                  J s
//   elementary charge                  1.602 176 634 e-19       (exact)                  C
//   Boltzmann constant                 1.380 649 e-23           (exact)                  J K^-1
//   Avogadro constant                  6.022 140 76 e23         (exact)                  mol^-1
//   Newtonian constant of gravitation  6.674 30 e-11            0.000 15 e-11            m^3 kg^-1 s^-2
//   vacuum electric permittivity       8.854 187 8188 e-12      0.000 000 0014 e-12      F m^-1
//   vacuum mag. permeability           1.256 637 061 27 e-6     0.000 000 000 20 e-6     N A^-2
//   electron mass                      9.109 383 7139 e-31      0.000 000 0028 e-31      kg
//   proton mass                        1.672 621 925 95 e-27    0.000 000 000 52 e-27    kg
//   neutron mass                       1.674 927 500 56 e-27    0.000 000 000 85 e-27    kg
//   atomic mass constant               1.660 539 068 92 e-27    0.000 000 000 52 e-27    kg
//   fine-structure constant            7.297 352 5643 e-3       0.000 000 0011 e-3
//   molar gas constant                 8.314 462 618...         (exact)                  J mol^-1 K^-1
//   Stefan-Boltzmann constant          5.670 374 419... e-8     (exact)                  W m^-2 K^-4
//   Bohr radius                        5.291 772 105 44 e-11    0.000 000 000 82 e-11    m
//   standard acceleration of gravity   9.806 65                 (exact)                  m s^-2
//
// The fetch went through a page-to-text tool that returned these rows; the digits were not retyped from memory.
module Ranvier.Science.Tests.ConstantsTests

open System
open Expecto
open Ranvier
open Ranvier.Numerics
open Ranvier.Quantities

let private relative (actual: float) (expected: float) = abs (actual - expected) / abs expected

let private find (model: Model) (id: string) : Entry =
    match model.TryFind id with
    | ValueSome e -> e
    | ValueNone -> failtestf "the model does not declare \"%s\"" id

let private declared (result: Result<Entry, string>) : Entry =
    match result with
    | Result.Ok e -> e
    | Result.Error message -> failtestf "expected an entry, got: %s" message

[<Tests>]
let tests =
    testList
        "Constants"
        [
            test "every constant is declared" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model

                    let fields: (string * Entry * UnitInfo) list =
                        [
                            "c", pc.SpeedOfLight.Entry, (Units.metre / Units.second).Info
                            "h", pc.Planck.Entry, (Units.joule * Units.second).Info
                            "hbar", pc.ReducedPlanck.Entry, (Units.joule * Units.second).Info
                            "e", pc.ElementaryCharge.Entry, Units.coulomb.Info
                            "k_B", pc.Boltzmann.Entry, (Units.joule / Units.kelvin).Info
                            "N_A", pc.Avogadro.Entry, (Units.one / Units.mole).Info
                            "G",
                            pc.Gravitation.Entry,
                            (UnitOf.squared Units.metre * Units.metre / (Units.kilogram * UnitOf.squared Units.second))
                                .Info
                            "epsilon_0", pc.VacuumPermittivity.Entry, (Units.farad / Units.metre).Info
                            "mu_0", pc.VacuumPermeability.Entry, (Units.newton / UnitOf.squared Units.ampere).Info
                            "m_e", pc.ElectronMass.Entry, Units.kilogram.Info
                            "m_p", pc.ProtonMass.Entry, Units.kilogram.Info
                            "m_n", pc.NeutronMass.Entry, Units.kilogram.Info
                            "m_u", pc.AtomicMassConstant.Entry, Units.kilogram.Info
                            "alpha", pc.FineStructure.Entry, Units.one.Info
                            "R", pc.MolarGas.Entry, (Units.joule / (Units.mole * Units.kelvin)).Info
                            "sigma",
                            pc.StefanBoltzmann.Entry,
                            (Units.watt / (UnitOf.squared Units.metre * UnitOf.squared (UnitOf.squared Units.kelvin)))
                                .Info
                            "a_0", pc.BohrRadius.Entry, Units.metre.Info
                            "g_n", pc.StandardGravity.Entry, (Units.metre / UnitOf.squared Units.second).Info
                            "au", pc.AstronomicalUnit.Entry, Units.metre.Info
                        ]

                    Expect.equal fields.Length 19 "the record has 19 fields"

                    for id, entry, unit in fields do
                        let found = find model id
                        Expect.equal found.Id entry.Id (id + " is the record's field")
                        Expect.isNonEmpty found.Source (id + " names its source")
                        Expect.isNonEmpty found.Name (id + " has a name")
                        Expect.isTrue (UnitInfo.sameUnit found.Unit unit) (id + " has the field's unit"))
            }

            test "the defining constants are exact" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model

                    for id, entry, literal in
                        [
                            "c", pc.SpeedOfLight.Entry, 299792458.0
                            "h", pc.Planck.Entry, 6.62607015e-34
                            "e", pc.ElementaryCharge.Entry, 1.602176634e-19
                            "k_B", pc.Boltzmann.Entry, 1.380649e-23
                            "N_A", pc.Avogadro.Entry, 6.02214076e23
                            "g_n", pc.StandardGravity.Entry, 9.80665
                        ] do
                        Expect.equal (entry.Read ()) literal (id + " is NIST's value")
                        Expect.equal (entry.Uncertainty ()) 0.0 (id + " is exact")
                        Expect.equal entry.Kind QuantityKind.Constant (id + " is a constant"))
            }

            test "the measured constants" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model

                    for id, entry, value, uncertainty in
                        [
                            "alpha", pc.FineStructure.Entry, 7.2973525643e-3, 1.1e-12
                            "G", pc.Gravitation.Entry, 6.67430e-11, 1.5e-15
                            "m_e", pc.ElectronMass.Entry, 9.1093837139e-31, 2.8e-40
                            "m_p", pc.ProtonMass.Entry, 1.67262192595e-27, 5.2e-37
                            "m_n", pc.NeutronMass.Entry, 1.67492750056e-27, 8.5e-37
                            "m_u", pc.AtomicMassConstant.Entry, 1.66053906892e-27, 5.2e-37
                            "a_0", pc.BohrRadius.Entry, 5.29177210544e-11, 8.2e-21
                        ] do
                        Expect.equal (entry.Read ()) value (id + " is NIST's value")
                        Expect.isTrue (relative (entry.Uncertainty ()) uncertainty < 1e-12) (id + " has NIST's standard uncertainty"))
            }

            test "constants computed from exact ones" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model

                    // NIST truncates the exact value after the last printed digit, whose unit is the third number.
                    for id, entry, printed, unit in
                        [
                            "hbar", pc.ReducedPlanck.Entry, 1.054571817e-34, 1e-43
                            "R", pc.MolarGas.Entry, 8.314462618, 1e-9
                            "sigma", pc.StefanBoltzmann.Entry, 5.670374419e-8, 1e-17
                        ] do
                        let difference = entry.Read () - printed
                        Expect.isTrue (difference >= 0.0) (id + " is at or above the truncated print")
                        Expect.isTrue (difference < unit) (id + " is below the print plus one unit of its last digit")
                        Expect.equal (entry.Uncertainty ()) 0.0 (id + " is exact"))
            }

            test "mu_0 and epsilon_0 come from alpha" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model
                    let alpha = pc.FineStructure.Entry
                    let mu = pc.VacuumPermeability.Entry
                    let eps = pc.VacuumPermittivity.Entry
                    let ur = alpha.Uncertainty () / alpha.Read ()

                    Expect.isTrue (relative (mu.Read ()) 1.25663706127e-6 < 1e-11) "mu_0 is NIST's value"
                    Expect.isTrue (relative (eps.Read ()) 8.8541878188e-12 < 1e-11) "epsilon_0 is NIST's value"
                    Expect.isTrue (relative (mu.Uncertainty ()) 2.0e-16 < 0.07) "u(mu_0) is NIST's two digits"
                    Expect.isTrue (relative (eps.Uncertainty ()) 1.4e-21 < 0.07) "u(epsilon_0) is NIST's two digits"
                    Expect.isTrue (relative (mu.Uncertainty () / mu.Read ()) ur < 1e-12) "u_r(mu_0) = u_r(alpha)"
                    Expect.isTrue (relative (eps.Uncertainty () / eps.Read ()) ur < 1e-12) "u_r(epsilon_0) = u_r(alpha)"

                    Expect.isTrue
                        (abs (Uncertain.correlation (alpha.Measured ()) (mu.Measured ()) - 1.0) < 1e-12)
                        "alpha and mu_0 are fully correlated"

                    Expect.isTrue
                        (abs (Uncertain.correlation (mu.Measured ()) (eps.Measured ()) + 1.0) < 1e-12)
                        "mu_0 and epsilon_0 are fully anti-correlated")
            }

            test "epsilon_0 times mu_0 is 1/c² with no propagated uncertainty" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model
                    let c = pc.SpeedOfLight.Entry.Read ()
                    let alpha = pc.FineStructure.Entry
                    let ur = alpha.Uncertainty () / alpha.Read ()
                    let p = pc.VacuumPermittivity.Entry.Measured () * pc.VacuumPermeability.Entry.Measured ()

                    Expect.isTrue (abs (Uncertain.value p * c * c - 1.0) < 1e-15) "the product is 1/c²"

                    Expect.isTrue
                        (Uncertain.standardUncertainty p <= 1e-6 * Uncertain.value p * ur)
                        "the shared source cancels")
            }

            test "units agree with the constants" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model
                    Expect.equal Units.electronvolt.Info.Scale (pc.ElementaryCharge.Entry.Read ()) "eV is e joules"
                    Expect.equal Units.dalton.Info.Scale (pc.AtomicMassConstant.Entry.Read ()) "Da is m_u kilograms"

                    Expect.equal
                        (pc.AstronomicalUnit.Entry.Read ())
                        Units.astronomicalUnit.Info.Scale
                        "au is the unit's scale")
            }

            test "independent measured constants are uncorrelated" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model

                    Expect.equal
                        (Uncertain.correlation (pc.Gravitation.Entry.Measured ()) (pc.ElectronMass.Entry.Measured ()))
                        0.0
                        "G and m_e"

                    Expect.equal
                        (Uncertain.correlation (pc.ProtonMass.Entry.Measured ()) (pc.NeutronMass.Entry.Measured ()))
                        0.0
                        "m_p and m_n")
            }

            test "declaring twice in one model raises" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    Constants.declare model |> ignore
                    Expect.throwsT<ArgumentException> (fun () -> Constants.declare model |> ignore) "the second declare raises")
            }

            test "a formula over the constants" {
                withGraph (fun () ->
                    let model = Model.create "m"
                    let pc = Constants.declare model
                    let rest = declared (Quantity.formula model "rest" "Electron rest energy" "m_e * c^2")
                    let c = pc.SpeedOfLight.Entry.Read ()
                    let me = pc.ElectronMass.Entry

                    Expect.isTrue (UnitInfo.sameUnit rest.Unit Units.joule.Info) "the formula is in joules"
                    Expect.equal (rest.Read ()) (me.Read () * (c * c)) "the evaluator's value"

                    Expect.isTrue
                        (relative (rest.Uncertainty ()) (c * c * me.Uncertainty ()) < 1e-14)
                        "u(m_e c²) = c² u(m_e)")
            }
        ]
