-- Indus Corpus Schema — Alloy specification
-- Third Buyer Advisory, April 2026
--
-- Verifies structural invariants of the normalized schema
-- for ingesting Rajan-Sivanantham TN graffiti, Mahadevan IVC corpus,
-- and Tamil logogram data into a unified SQLite database.
--
-- Run: java -jar alloy.jar indus_corpus.als

-- === SIGNATURES ===

sig Source {}

sig Site {
    source: one Source
}

sig Artefact {
    site: one Site,
    source: one Source
}

sig BaseSign {
    source: one Source
}

-- sign_form hierarchy: variant or composite of a base sign
-- two-level only, no form-of-form
sig SignForm {
    parent: one BaseSign
}

sig SignOccurrence {
    artefact: one Artefact,
    sign: one (BaseSign + SignForm),
    position: one Int
}

sig MorphologicalParallel {
    signA: one (BaseSign + SignForm),
    sourceA: one Source,
    signB: one (BaseSign + SignForm),
    sourceB: one Source
}

-- sign_concordance: maps between numbering systems (Parpola, Mahadevan, Wells)
-- A Parpola sign may map to 0+ Mahadevan signs and 0+ Wells signs
sig SignConcordance {
    parpolaSign: one BaseSign,
    mahadevans: set BaseSign
}

-- cisi_inscriptions: real seal inscriptions from the CISI corpus
-- Each has an ordered sequence of signs and belongs to one source
sig CisiInscription {
    signs: seq BaseSign,
    inscriptionSource: one Source
}

-- === FACTS (schema constraints) ===

-- F1: Every artefact's source must match its site's source
--     (artefacts found at a site belong to that site's corpus)
--     RELAXED: artefact source and site source may differ
--     (IVC seal found at Mesopotamian site). No constraint here.

-- F2: Sign hierarchy is exactly two levels.
--     SignForm -> BaseSign. No SignForm -> SignForm.
fact twoLevelHierarchy {
    all sf: SignForm | sf.parent in BaseSign
    -- SignForm.sign resolves through parent to a BaseSign
}

-- F3: No duplicate position within an artefact
fact noDuplicatePosition {
    all disj o1, o2: SignOccurrence |
        o1.artefact = o2.artefact implies o1.position != o2.position
}

-- F4: Morphological parallels link signs from DIFFERENT sources
fact parallelCrossesSources {
    all mp: MorphologicalParallel | mp.sourceA != mp.sourceB
}

-- F5: Parallel source matches the sign's actual source
fact parallelSourceConsistency {
    all mp: MorphologicalParallel |
        (mp.signA in BaseSign implies
            mp.sourceA = mp.signA.source) and
        (mp.signA in SignForm implies
            mp.sourceA = mp.signA.parent.source) and
        (mp.signB in BaseSign implies
            mp.sourceB = mp.signB.source) and
        (mp.signB in SignForm implies
            mp.sourceB = mp.signB.parent.source)
}

-- F6: Concordance maps within a single numbering system's source
--     parpolaSign must be from the CISI/Parpola source
--     mahadevans must all be from the Mahadevan source
fact concordanceSourceSeparation {
    all sc: SignConcordance |
        all m: sc.mahadevans | m.source != sc.parpolaSign.source
}

-- F7: No duplicate concordance entries for the same Parpola sign
fact concordanceUnique {
    all disj c1, c2: SignConcordance | c1.parpolaSign != c2.parpolaSign
}

-- F8: Every CISI inscription sign must be a valid BaseSign
fact cisiSignsExist {
    all ci: CisiInscription | ci.signs.elems in BaseSign
}

-- F9: BaseSign IDs unique within a source (modeled by Alloy's
--     atom identity — two BaseSign atoms are always distinct)
--     In SQL: PRIMARY KEY (source, sign_id). Alloy handles this
--     by construction.

-- === ASSERTIONS ===

-- A1: Every artefact belongs to exactly one site
assert artefactHasOneSite {
    all a: Artefact | one a.site
}

-- A2: Every sign occurrence belongs to exactly one artefact
assert occurrenceHasOneArtefact {
    all o: SignOccurrence | one o.artefact
}

-- A3: Every SignForm references exactly one BaseSign
assert formHasOneParent {
    all sf: SignForm | one sf.parent
}

-- A4: Sign hierarchy is max two levels (no form-of-form)
--     This is structural: SignForm.parent is BaseSign, not SignForm.
assert noFormOfForm {
    no sf: SignForm | sf.parent in SignForm
}

-- A5: Every morphological parallel crosses source boundaries
assert parallelsCrossSources {
    all mp: MorphologicalParallel | mp.sourceA != mp.sourceB
}

-- A6: No artefact has two occurrences at the same position
assert noPositionCollision {
    all disj o1, o2: SignOccurrence |
        o1.artefact = o2.artefact implies o1.position != o2.position
}

-- A7: Every sign occurrence references a sign that exists
--     (BaseSign or SignForm — Alloy typing guarantees this)
assert occurrenceSignExists {
    all o: SignOccurrence | o.sign in (BaseSign + SignForm)
}

-- A8: A SignForm's parent's source is the form's effective source
--     (forms inherit source from their base sign)
assert formInheritsSource {
    all sf: SignForm | sf.parent.source in Source
}

-- A9: No self-parallel (a sign paralleled with itself)
assert noSelfParallel {
    all mp: MorphologicalParallel | mp.signA != mp.signB
}

-- A10: Concordance maps cross source boundaries
assert concordanceCrossesSources {
    all sc: SignConcordance |
        all m: sc.mahadevans | m.source != sc.parpolaSign.source
}

-- A11: No duplicate concordance for the same Parpola sign
assert concordanceNoDuplicates {
    all disj c1, c2: SignConcordance | c1.parpolaSign != c2.parpolaSign
}

-- A12: CISI inscription signs are valid
assert cisiSignsValid {
    all ci: CisiInscription | ci.signs.elems in BaseSign
}

-- === CHECKS ===

check artefactHasOneSite for 6
check occurrenceHasOneArtefact for 6
check formHasOneParent for 6
check noFormOfForm for 6
check parallelsCrossSources for 6
check noPositionCollision for 6
check occurrenceSignExists for 6
check formInheritsSource for 6
check noSelfParallel for 6
check concordanceCrossesSources for 6
check concordanceNoDuplicates for 6
check cisiSignsValid for 6

-- === EXPLORATION ===

-- Generate a small valid instance to sanity-check the model
run showExample {
    #Source = 3
    #Site >= 2
    #Artefact >= 3
    #BaseSign >= 6
    #SignForm >= 2
    #SignOccurrence >= 4
    #MorphologicalParallel >= 1
    #SignConcordance >= 2
    #CisiInscription >= 1
} for 6
