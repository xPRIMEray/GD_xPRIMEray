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
2. Use **Field Structure** (**F**) and **Optical-only World** (**O**) to choose the observer presentation.
3. Enable **Formal G SNAPSHOT** (**G**) for scientific work. LIVE film is a separate preview clock.
4. Cycle sealed Probe Views with **Q** after Complete.
5. Open the **Workbench** (**Esc**) for configuration.

For Demo v1, **E** is reserved / inert and **H** remains retired / inert. Experiment selection remains available to internal and programmatic qualification paths.

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
