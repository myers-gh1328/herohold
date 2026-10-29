# Blazor component tests

These MSTest tests render the actual Razor components with
[bUnit](https://bunit.dev/docs/getting-started/writing-tests.html). The project
pins MIT-licensed [bUnit 2.6.2](https://www.nuget.org/packages/bunit/2.6.2), which
supports .NET 10. No browser, Python runtime, saved characters or network service
is required.

```sh
dotnet test dotnet/Herohold.Components.Tests/Herohold.Components.Tests.csproj
```

Player control tests cover modifier explanations through focus/hover/click,
identity-aware physical counters, source choice repair, multiselect search and
recursive feat editors. Character workflow tests use the real `WorkspaceSession`
and isolated temporary storage, replacing only the external workflow response.
They cover source/build adoption gates, explicit separate-copy adoption, compact
PDF review, safe uploaded text, pending HP preview controls and required-choice
counts. Gameplay tests exercise their components through actual public controls.

These tests verify rendered semantics and events. The published browser gate
remains responsible for CSS, responsive layout, real browser behavior and complete
character/rules/PDF integration:

```sh
.venv/bin/python tools/check_dotnet_published.py .tmp/herohold-dotnet-candidate
```

The browser driver is development tooling. The published application and its PDF
worker execute .NET code.
