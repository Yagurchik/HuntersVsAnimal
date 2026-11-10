---
paths: ["**/*.cs"]
---

# Shared C# Style

Project-local conventions and formatter settings take priority. Use these rules
as the default for new Unity code and when the project has no established style.

## Structure And Naming

- Use namespaces that match the project’s assembly or stable feature boundary.
  Do not derive a namespace mechanically from every folder.
- Write access modifiers explicitly except on interface members.
- Use `PascalCase` for types, methods, properties, events, and public members.
- Use `camelCase` for parameters and local variables.
- Use `_camelCase` for private instance fields.
- Name booleans with `Is`, `Can`, or `Has`; use `TryX` for operations that
  report whether they succeeded.
- Name events as completed facts such as `Damaged` or `ItemAdded`, and event
  handlers as `OnDamaged` or `OnItemAdded`.
- Prefer precise responsibility names over vague `Manager`, `Controller`,
  `Helper`, or `Utility` names.
- Use plural nouns for collections and avoid abbreviations that make the call
  site harder to understand.

Keep member ordering consistent within the project. A sensible default is:
fields and events, constructors/initialization, properties, public methods,
protected methods, private methods.

## Language And Formatting

- Use `var` when the assigned type is obvious; use an explicit type when it
  improves understanding.
- Use idiomatic boolean expressions such as `if (!isReady)`.
- Use braces consistently, including for single-line control flow.
- Keep one blank line between logical blocks and methods; avoid vertical noise.
- Extract repeated or conceptually named logic into a method.
- Replace unexplained literals with a named constant or setting when the name
  adds meaning. Ordinary values such as `0` and `1` are fine when obvious.
- Write comments to explain why, constraints, or non-obvious engine behavior.
  Do not replace useful comments with runtime logs.
- Log actionable state transitions and failures. Never log every frame or emit
  noisy traces by default.

## Unity

- Use `[SerializeField] private` fields for Inspector references and local
  tunables.
- Preserve serialized field names. If a rename is required, perform an explicit
  serialization migration.
- Cache required components during initialization when lookup would otherwise
  repeat.
- Keep `Update` and other per-frame callbacks small; delegate decisions to
  named methods or plain C# objects.
- Prefer plain C# classes for deterministic rules and MonoBehaviours for Unity
  lifecycle, scene binding, and presentation.
- Use ScriptableObjects for shared authored data, reusable definitions, or
  content catalogs—not for every component setting.
- Avoid new global singletons, service locators, DI containers, event buses,
  object pools, and broad interfaces without a concrete current need.

## Async Code

- Follow the async stack already used by the project.
- Use `Task`/`Task<T>` when no Unity-specific async package is established.
- Use an alternative async type only when its package is already installed and
  the project has chosen it deliberately.
- Add the `Async` suffix to awaitable methods.
- Avoid `async void` except framework-mandated event handlers; handle
  exceptions explicitly in those handlers.
- Put `CancellationToken cancellationToken` last and propagate it to nested
  async calls.

## Generated And Third-Party Code

Do not edit generated code, package-cache contents, vendored dependencies, or
machine-produced files unless the task explicitly owns that source. Extend or
wrap them from project code when possible.
