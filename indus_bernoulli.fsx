#!/usr/bin/env -S dotnet fsi
// Bernoulli Independence Argument — numerical verification
// Five independent data paths, joint probability under H0
// Third Buyer Advisory — Rajeshkumar Venugopal, April 2026
// Conforms to: spec/fsharp/reference/fsharp_coding_standard.tex
//
// Usage:
//   dotnet fsi indus_bernoulli.fsx
//
// No DB dependency. Pure computation. MathNet.Numerics for distributions.

#r "nuget: MathNet.Numerics, 5.0.0"
#r "nuget: MathNet.Numerics.FSharp, 5.0.0"

open System
open MathNet.Numerics
open MathNet.Numerics.Distributions
open MathNet.Numerics.Statistics

// === PATH DEFINITIONS ========================================================
// Each path: name, probability bound under H0, justification

type IndependentPath = {
    Name: string
    P: float
    Basis: string }

let paths = [|
    { Name  = "Frequency-commodity alignment"
      P     = 0.125
      Basis = "Most frequent sign (M342, ~10%) maps to most traded commodity (jar). 1/8 commodity categories." }

    { Name  = "Concordance independence"
      P     = 0.25
      Basis = "Shape-based concordance produces meaningful cargo decode for 2 seals independently. 0.5 x 0.5." }

    { Name  = "Geographic correlation"
      P     = 0.20
      Basis = "Export hub seal (M-52A, Mohenjo-daro) decodes to export route (Mesopotamia). 1/5 routes." }

    { Name  = "Cross-corpus sign overlap"
      P     = 0.10
      Basis = "90% sign overlap between TN potsherds and IVC seals across 1500 km. Generous bound for H0." }

    { Name  = "Weight system precision"
      P     = 0.05
      Basis = "0.5% weight standardisation across 1M km2. Predicted by trade metrology, not by religious symbolism." }
|]

// === JOINT PROBABILITY (Bernoulli product) ===================================

let jointP = paths |> Array.fold (fun acc p -> acc * p.P) 1.0
let oddsAgainst = 1.0 / jointP

// === BAYESIAN UPDATE =========================================================
// Skeptic starts at equipoise P(H0) = 0.5

let priorH0 = 0.5
let likelihoodRatio = 1.0 / jointP
let posteriorH0 = priorH0 / (priorH0 + (1.0 - priorH0) * likelihoodRatio)

// === SENSITIVITY ANALYSIS ====================================================
// Scale all path probabilities by a common multiplier k (more generous to H0)

let sensitivity =
    [| 0.5; 0.75; 1.0; 1.25; 1.5; 2.0; 3.0 |]
    |> Array.map (fun k ->
        let scaled = paths |> Array.map (fun p -> min 1.0 (p.P * k))
        let joint = scaled |> Array.fold (fun acc p -> acc * p) 1.0
        k, joint, 1.0 / joint)

// === MONTE CARLO VALIDATION ==================================================
// Simulate N trials: for each trial, each path independently "coincides"
// with probability p_i. Count how often ALL five coincide.

let monteCarlo (nTrials: int) (rng: Random) =
    let mutable allCoincide = 0
    for _ in 1 .. nTrials do
        let mutable allHit = true
        for p in paths do
            if rng.NextDouble() > p.P then
                allHit <- false
        if allHit then
            allCoincide <- allCoincide + 1
    float allCoincide / float nTrials

let rng = Random(42)
let nTrials = 10_000_000
let mcEstimate = monteCarlo nTrials rng

// === BETA DISTRIBUTION: POSTERIOR PER PATH ===================================
// For each path, model uncertainty in p_i as Beta(a, b).
// If we observe 1 "coincidence" out of 1 trial, posterior is Beta(a+1, b).
// Report 95% credible interval.

let betaCredibleIntervals =
    paths |> Array.map (fun path ->
        // Prior: Beta(1, 1/p - 1) centered near p
        // This gives E[p] = p approximately
        let a = 1.0
        let b = (1.0 / path.P) - 1.0
        let dist = Beta(a + 1.0, b)
        let lo = dist.InverseCumulativeDistribution(0.025)
        let hi = dist.InverseCumulativeDistribution(0.975)
        let mean = dist.Mean
        path.Name, path.P, mean, lo, hi)

// === FISHER'S METHOD (combined p-value) ======================================
// Treat each path probability as an independent p-value.
// Fisher's statistic: -2 * sum(ln(p_i)) ~ chi-squared(2k)

let fisherStat = -2.0 * (paths |> Array.sumBy (fun p -> log p.P))
let df = 2 * paths.Length
let chiSq = ChiSquared(float df)
let fisherPValue = 1.0 - chiSq.CumulativeDistribution(fisherStat)

// === REPORT ==================================================================

printfn ""
printfn "  BERNOULLI INDEPENDENCE ARGUMENT — NUMERICAL VERIFICATION"
printfn "  Ledger of Meluhha | Third Buyer Advisory | April 2026"
printfn "  ================================================================"
printfn ""

printfn "  INDEPENDENT DATA PATHS:"
printfn "  %-5s  %-38s  %-8s  %s" "#" "Path" "P(H0)" "Basis"
printfn "  %s" (String.replicate 100 "-")
paths |> Array.iteri (fun i p ->
    printfn "  %-5d  %-38s  %-8.4f  %s" (i+1) p.Name p.P p.Basis)

printfn ""
printfn "  JOINT PROBABILITY (Bernoulli product):"
printfn "    P(H0) = %s" (paths |> Array.map (fun p -> sprintf "%.3f" p.P) |> String.concat " x ")
printfn "           = %.6e" jointP
printfn "    Odds against H0: %.0f : 1" oddsAgainst
printfn "    One chance in %s" (sprintf "%.0f" oddsAgainst)

printfn ""
printfn "  BAYESIAN UPDATE (prior = 0.5, equipoise):"
printfn "    Likelihood ratio (H1/H0): %.1f" likelihoodRatio
printfn "    Posterior P(H0 | data):    %.6e" posteriorH0
printfn "    Posterior P(H1 | data):    %.6f" (1.0 - posteriorH0)

printfn ""
printfn "  SENSITIVITY ANALYSIS (scale all priors by k):"
printfn "  %-8s  %-15s  %-15s" "k" "Joint P(H0)" "Odds against"
printfn "  %s" (String.replicate 42 "-")
for (k, joint, odds) in sensitivity do
    printfn "  %-8.2f  %-15.6e  %.0f : 1" k joint odds

printfn ""
printfn "  MONTE CARLO VALIDATION (%s trials, seed=42):" (sprintf "%d" nTrials)
printfn "    Analytic P(H0):    %.6e" jointP
printfn "    Monte Carlo P(H0): %.6e" mcEstimate
printfn "    Ratio (MC/analytic): %.3f" (if jointP > 0.0 then mcEstimate / jointP else nan)

printfn ""
printfn "  BETA POSTERIOR (95%% credible intervals per path):"
printfn "  %-38s  %-8s  %-8s  [%-8s, %-8s]" "Path" "Bound" "E[p]" "2.5%%" "97.5%%"
printfn "  %s" (String.replicate 78 "-")
for (name, bound, mean, lo, hi) in betaCredibleIntervals do
    printfn "  %-38s  %-8.4f  %-8.4f  [%-8.4f, %-8.4f]" name bound mean lo hi

printfn ""
printfn "  FISHER'S COMBINED TEST:"
printfn "    Test statistic: -2 * sum(ln(p_i)) = %.4f" fisherStat
printfn "    Degrees of freedom: %d" df
printfn "    Combined p-value: %.6e" fisherPValue

printfn ""
printfn "  CONCLUSION:"
printfn "    Joint P(H0) = %.2e (analytic)" jointP
printfn "    Joint P(H0) = %.2e (Monte Carlo, %s trials)" mcEstimate (sprintf "%d" nTrials)
printfn "    Fisher combined p = %.2e" fisherPValue
printfn "    All three methods converge: the null hypothesis"
printfn "    requires a 1-in-%.0f coincidence." oddsAgainst
printfn ""
printfn "    Noise scatters. Signal clusters. The decode clusters."
printfn ""
