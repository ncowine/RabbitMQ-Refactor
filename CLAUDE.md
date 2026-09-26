# Project instructions

RabbitMQ messaging for legacy WPF (.NET Framework 4.7.2), modern WPF (.NET 8) and an ASP.NET server (.NET 10).
Start with `README.md`; the design is in `docs/adr/`.

## Always update the README

**Every change that affects anything described in `README.md` must update `README.md` in the same change.** That
includes projects, public APIs, options and defaults, headers, queue or exchange behaviour, recipes, configuration,
commands, versions, test rules and known limits. Keep its language simple enough for a newcomer. If you're unsure
whether a change affects the README, check the relevant section and update it.

## Compatibility rules (don't break legacy apps)

- `src/Common.RabbitMQ` (the legacy adapter): **additions only.** A new public member must be added to
  `ApprovedAdditions` in `tests/Common.RabbitMQ.Tests/PublicApi/PublicApiTests.cs`, with its ADR reason. Keep
  exactly one public constructor on `RabbitMQService` and `RabbitMQServiceRouter` (DryIoc).
- Never edit `tests/Fixtures/Common.RabbitMQ.Baseline` (the frozen original) or regenerate `Golden/*.json` to make a
  test pass.
- The legacy model (`tests/Fixtures/LegacyModel`) changes only together with `docs/legacy-baseline-assumptions.md`
  and the test named after the assumption.
- Design changes get an ADR in `docs/adr/`, or an update to the relevant ADR.

## C# conventions

- No `_` prefix on private fields.
- One class (or interface or enum) per file.
- No `var`: use explicit types.
- Match the surrounding comment density and naming.

## Testing

- Run the tests against a real broker, with `RABBITMQ_TESTS_REQUIRED=1`, so missing-broker skips can't hide failures:
  `dotnet test tests/Common.RabbitMQ.Tests`. Both net472 and net8.0 must pass.
- For a new guarantee, check the test actually catches its absence: temporarily break the code and confirm it fails.
- Tests must clean up the durable queues and exchanges they create.

## Workflow

- Branch from `main`, open a pull request, and merge only when CI (`.github/workflows/ci.yml`) is green.
- Local quirks:
  - A stale MSBuild node can cause an OutOfMemoryException or a hung build. Run `dotnet build-server shutdown`, or
    build with `-nr:false`.
  - Git Bash rewrites environment values like `/` into Windows paths; use PowerShell for those.
