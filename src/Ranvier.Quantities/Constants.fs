namespace Ranvier.Quantities

open System
open Ranvier
open Ranvier.Numerics

/// The physical constants `Constants.declare` adds to a model, each an entry of the model under the identifier in its
/// field's comment.
type PhysicalConstants =
    {
        /// `c`.
        SpeedOfLight: Quantity<m / s>
        /// `h`.
        Planck: Quantity<J s>
        /// `hbar`.
        ReducedPlanck: Quantity<J s>
        /// `e`.
        ElementaryCharge: Quantity<C>
        /// `k_B`.
        Boltzmann: Quantity<J / K>
        /// `N_A`.
        Avogadro: Quantity<1 / mol>
        /// `G`.
        Gravitation: Quantity<m ^ 3 / (kg s ^ 2)>
        /// `epsilon_0`.
        VacuumPermittivity: Quantity<F / m>
        /// `mu_0`.
        VacuumPermeability: Quantity<N / A ^ 2>
        /// `m_e`.
        ElectronMass: Quantity<kg>
        /// `m_p`.
        ProtonMass: Quantity<kg>
        /// `m_n`.
        NeutronMass: Quantity<kg>
        /// `m_u`.
        AtomicMassConstant: Quantity<kg>
        /// `alpha`.
        FineStructure: Quantity<1>
        /// `R`.
        MolarGas: Quantity<J / (mol K)>
        /// `sigma`.
        StefanBoltzmann: Quantity<W / (m ^ 2 K ^ 4)>
        /// `a_0`.
        BohrRadius: Quantity<m>
        /// `g_n`.
        StandardGravity: Quantity<m / s ^ 2>
        /// `au`.
        AstronomicalUnit: Quantity<m>
    }

module Constants =
    let private codata = "CODATA 2022, https://physics.nist.gov/cuu/Constants/"

    /// The identifiers `declare` adds, in the order it adds them.
    let private identifiers =
        [ "c"; "h"; "hbar"; "e"; "k_B"; "N_A"; "G"; "m_e"; "m_p"; "m_n"; "m_u"; "alpha"; "R"; "sigma"; "a_0"; "mu_0"
          "epsilon_0"; "g_n"; "au" ]

    /// The constants of CODATA 2022 as entries of `model`, with the identifiers `c h hbar e k_B N_A G epsilon_0 mu_0
    /// m_e m_p m_n m_u alpha R sigma a_0 g_n au`, and each entry's `Name` NIST's name of the quantity. The defining
    /// constants c, h, e, k_B, N_A and the standard acceleration of gravity are exact, as are the constants computed
    /// from them: ħ = h/(2π), R = N_A k_B and σ = 2π⁵k_B⁴/(15h³c²), evaluated in double precision. α, G, m_e, m_p,
    /// m_n, m_u and a_0 are `Quantity.measuredConstant`s with NIST's standard uncertainty, each its own uncertainty
    /// source. μ0 = 2αh/(e²c) and ε0 = 1/(μ0c²) are `Quantity.deriveMeasured` over the `Entry.Measured` of α and of
    /// μ0, so both share α's source: they are correlated with α and with each other (correlation +1 and −1). CODATA
    /// publishes correlations among several of the measured constants (general knowledge: for example m_e, m_p and
    /// m_u); 0.2.0 does not keep them, so values computed from two of them treat them as independent. The
    /// astronomical unit is exactly the scale of `Units.astronomicalUnit`. Creates 19 entries and 4 memos (2 per
    /// derived entry), all owned by the current owner, so it needs an active graph. Before it declares anything it
    /// raises `ArgumentException`, as `Quantity.input` does, when the model already declares one of the 19
    /// identifiers, and `InvalidOperationException` when no graph is active (`Graph.TryCurrent` is `ValueNone`); the
    /// model is then unchanged. Inside a pure body it raises as every declaration does, also before adding an entry.
    let declare (model: Model) : PhysicalConstants =
        for id in identifiers do
            model.Check id

        if Graph.TryCurrent.IsNone then
            raise (InvalidOperationException "Constants.declare needs an active graph: mu_0 and epsilon_0 are memos.")

        let c = 299792458.0
        let h = 6.62607015e-34
        let e = 1.602176634e-19
        let kB = 1.380649e-23
        let nA = 6.02214076e23
        let exactSource = codata + " (defining constant, exact)"

        let speedOfLight =
            Quantity.constant model "c" "speed of light in vacuum" (Units.metre / Units.second) (c * 1.0<m / s>) exactSource

        let planck =
            Quantity.constant model "h" "Planck constant" (Units.joule * Units.second) (h * 1.0<J s>) exactSource

        let reducedPlanck =
            Quantity.constant
                model
                "hbar"
                "reduced Planck constant"
                (Units.joule * Units.second)
                (h / (2.0 * Math.PI) * 1.0<J s>)
                ("computed from h as h/(2 pi), " + codata)

        let elementaryCharge =
            Quantity.constant model "e" "elementary charge" Units.coulomb (e * 1.0<C>) exactSource

        let boltzmann =
            Quantity.constant model "k_B" "Boltzmann constant" (Units.joule / Units.kelvin) (kB * 1.0<J / K>) exactSource

        let avogadro =
            Quantity.constant model "N_A" "Avogadro constant" (Units.one / Units.mole) (nA * 1.0</mol>) exactSource

        let gravitation =
            Quantity.measuredConstant
                model
                "G"
                "Newtonian constant of gravitation"
                (UnitOf.squared Units.metre * Units.metre / (Units.kilogram * UnitOf.squared Units.second))
                (6.67430e-11 * 1.0<m ^ 3 / (kg s ^ 2)>)
                (1.5e-15 * 1.0<m ^ 3 / (kg s ^ 2)>)
                codata

        let electronMass =
            Quantity.measuredConstant model "m_e" "electron mass" Units.kilogram 9.1093837139e-31<kg> 2.8e-40<kg> codata

        let protonMass =
            Quantity.measuredConstant model "m_p" "proton mass" Units.kilogram 1.67262192595e-27<kg> 5.2e-37<kg> codata

        let neutronMass =
            Quantity.measuredConstant model "m_n" "neutron mass" Units.kilogram 1.67492750056e-27<kg> 8.5e-37<kg> codata

        let atomicMassConstant =
            Quantity.measuredConstant
                model
                "m_u"
                "atomic mass constant"
                Units.kilogram
                1.66053906892e-27<kg>
                5.2e-37<kg>
                codata

        let fineStructure =
            Quantity.measuredConstant model "alpha" "fine-structure constant" Units.one 7.2973525643e-3 1.1e-12 codata

        let molarGas =
            Quantity.constant
                model
                "R"
                "molar gas constant"
                (Units.joule / (Units.mole * Units.kelvin))
                (nA * kB * 1.0<J / (mol K)>)
                ("computed from N_A and k_B as N_A k_B, " + codata)

        let stefanBoltzmann =
            let pi5 = Math.PI * Math.PI * Math.PI * Math.PI * Math.PI
            let sigma = 2.0 * pi5 * (kB * kB * kB * kB) / (15.0 * (h * h * h) * (c * c))

            Quantity.constant
                model
                "sigma"
                "Stefan-Boltzmann constant"
                (Units.watt / (UnitOf.squared Units.metre * UnitOf.squared (UnitOf.squared Units.kelvin)))
                (sigma * 1.0<W / (m ^ 2 K ^ 4)>)
                ("computed from k_B, h and c as 2 pi^5 k_B^4/(15 h^3 c^2), " + codata)

        let bohrRadius =
            Quantity.measuredConstant model "a_0" "Bohr radius" Units.metre 5.29177210544e-11<m> 8.2e-21<m> codata

        let hFactor = 2.0 * h / (e * e * c)
        let alphaEntry = fineStructure.Entry

        let vacuumPermeability =
            Quantity.deriveMeasuredFrom
                model
                "mu_0"
                "vacuum mag. permeability"
                (Units.newton / UnitOf.squared Units.ampere)
                ("computed from alpha, h, e and c as 2 alpha h/(e^2 c), " + codata)
                (fun () -> alphaEntry.Measured () * hFactor)

        let permeabilityEntry = vacuumPermeability.Entry

        let vacuumPermittivity =
            Quantity.deriveMeasuredFrom
                model
                "epsilon_0"
                "vacuum electric permittivity"
                (Units.farad / Units.metre)
                ("computed from mu_0 and c as 1/(mu_0 c^2), " + codata)
                (fun () -> 1.0 / (permeabilityEntry.Measured () * (c * c)))

        let standardGravity =
            Quantity.constant
                model
                "g_n"
                "standard acceleration of gravity"
                (Units.metre / UnitOf.squared Units.second)
                (9.80665 * 1.0<m / s ^ 2>)
                "3rd CGPM, 1901 (exact); value as printed in CODATA 2022, https://physics.nist.gov/cuu/Constants/"

        let astronomicalUnit =
            Quantity.constant
                model
                "au"
                "astronomical unit"
                Units.metre
                (Units.astronomicalUnit.Info.Scale * 1.0<m>)
                "IAU 2012 Resolution B2 (exact)"

        {
            SpeedOfLight = speedOfLight
            Planck = planck
            ReducedPlanck = reducedPlanck
            ElementaryCharge = elementaryCharge
            Boltzmann = boltzmann
            Avogadro = avogadro
            Gravitation = gravitation
            VacuumPermittivity = vacuumPermittivity
            VacuumPermeability = vacuumPermeability
            ElectronMass = electronMass
            ProtonMass = protonMass
            NeutronMass = neutronMass
            AtomicMassConstant = atomicMassConstant
            FineStructure = fineStructure
            MolarGas = molarGas
            StefanBoltzmann = stefanBoltzmann
            BohrRadius = bohrRadius
            StandardGravity = standardGravity
            AstronomicalUnit = astronomicalUnit
        }
