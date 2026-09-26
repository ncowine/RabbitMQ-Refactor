## What and why

<!-- What does this change, and why? Link the ADR section if it's a design change. -->

## Checklist

- [ ] **`README.md` is updated** for anything this change affects (projects, APIs, options, defaults, behaviour, recipes, commands, versions, test rules, known limits), in simple language, or this change affects nothing it describes.
- [ ] Tests pass on net472 and net8.0 against a real broker (`RABBITMQ_TESTS_REQUIRED=1`).
- [ ] The legacy adapter's public API has only additions, and any new public member is listed in `ApprovedAdditions`.
- [ ] No edits to the frozen baseline or golden files. Legacy model changes are matched in `docs/legacy-baseline-assumptions.md`.
- [ ] ADRs are updated if the design changed.
