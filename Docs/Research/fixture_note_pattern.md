# Fixture Research Note Pattern

## Purpose

This note captures the shared documentation pattern for fixture research notes.
Each fixture note should explicitly state which spatial-analysis basis is
canonical so interpretation does not drift as the fixture family expands from
centered / symmetric cases into offset / asymmetric cases.

## Canonical Analysis Basis Section

Every fixture research note should include a section with these three fields:

- Primary analysis basis
- Companion analysis basis
- Reason for the choice

Recommended template:

```md
## Canonical Analysis Basis

- Primary analysis basis: `image-center` or `field-relative`
- Companion analysis basis: `field-relative` or `image-center`
- Reason: short explanation of why the primary basis is the correct canonical
  frame for this fixture's geometry and transport interpretation.
```

## Selection Guidance

- Use `image-center` as the primary basis when the fixture is symmetric or when
  the effective field center remains aligned with the image center.
- Use `field-relative` as the primary basis when the fixture intentionally
  introduces asymmetry through off-axis field placement or another shift in the
  transport origin.
- Keep the non-primary basis as a companion diagnostic when the shared
  characterization pipeline can emit it without changing runtime behavior.

## Artifact Guidance

When `categorical_final` is active, future fixture notes should list the
artifacts that match the declared basis choice:

- image-center companions such as `radial_profile.*` and
  `radial_sector_profile.*`
- field-relative companions such as `field_radial_profile.*` and
  `field_radial_sector_profile.*`

The note should make clear which basis is canonical for interpretation and
which basis is retained mainly for continuity, comparison, or screen-space
diagnostics.
