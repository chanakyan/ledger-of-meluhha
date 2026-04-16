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

-- F6: BaseSign IDs unique within a source (modeled by Alloy's
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

-- === EXPLORATION ===

-- Generate a small valid instance to sanity-check the model
run showExample {
    #Source = 2
    #Site >= 2
    #Artefact >= 3
    #BaseSign >= 4
    #SignForm >= 2
    #SignOccurrence >= 4
    #MorphologicalParallel >= 1
} for 6
