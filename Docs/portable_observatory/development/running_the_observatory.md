---
po_doc_type: development
title: Running the Observatory
status: partial
engine_commit: "5ce15c13"
generated: false
---

# Running the Observatory

Documentation-oriented operator notes for the Godot host. Engine build steps remain in project README / release docs.

## Minimal interactive path

1. Launch the Godot project with Transport Chamber / Observatory scenes as shipped.
2. Choose **Experiment** (**E**). H (Hermetic presentation) is retired / inert.
3. Enable **Formal G SNAPSHOT** (**G**) for scientific work. LIVE film is a separate preview clock.
4. Set field (**0** straight baseline; **1** / fine **,** **.** for curved policy).
5. Open **Inspector** (**Tab**).
6. Cycle **Display Mode** (**N**) only after reading outcomes.
7. Use **Region Probe** (**P**) only on Complete outcome planes.
8. Open **Evidence Console** (**Esc**) for recipes.

## Diagnostics

See [Diagnostics](../architecture/diagnostics.md). Default Live verbosity is **Summary**; Snapshot leans **Frame**. TransportTrace is opt-in.

## Docs site

```bash
# from repo root, with MkDocs available
mkdocs build
# or serve for local review
mkdocs serve
```

Site config: `mkdocs.yml` · docs root: `Docs/`.

## See also

- [Controls](../reference/controls.md)
- [Commit Discipline](commit_discipline.md)
