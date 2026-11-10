# Why these diagrams and guides are structured this way

The first overview showed all connections, but its tall host box and long labels made a reader parse implementation detail before seeing the structure. The views separate three questions: **where does software run?**, **which module owns what?**, and **what happens to one assessment?** This is a documentation design choice, not an architecture change.

## Practices adopted from primary guides

| Source | Practice applied here |
|---|---|
| [Microsoft diagram guidance](https://learn.microsoft.com/en-us/azure/well-architected/architect-role/design-diagrams) and [IBM technical diagrams](https://www.ibm.com/design/language/infographics/technical-diagrams/usage/) | Use consistent icon sizes and line styles; pair recognizable icons with words so the picture never depends on guessing a symbol. |
| [C4 notation](https://c4model.com/diagrams/notation) and [review checklist](https://c4model.com/diagrams/checklist) | Name the scope, identify the kinds of components, keep descriptions short, label directed relationships, and include a key. |
| [C4 component diagrams](https://c4model.com/diagrams/component) and [deployment diagrams](https://c4model.com/diagrams/deployment) | Show logical module ownership separately from environment-specific runtime placement, interfaces and retained state. |
| [C4 container diagrams](https://c4model.com/diagrams/container) and [dynamic diagrams](https://c4model.com/diagrams/dynamic) | Separate the static system map from an ordered runtime example. Our overview is C4-inspired; it is not a claim of full C4 conformance. |
| [arc42 building blocks](https://docs.arc42.org/section-5/) and [runtime view](https://docs.arc42.org/section-6/) | Keep module ownership and runtime scenarios available as distinct explanations. |
| [Diátaxis explanation](https://diataxis.fr/explanation/) and [how-to guides](https://diataxis.fr/how-to-guides/) | Explain relationships, constraints and design decisions in the knowledge docs; keep startup and recovery procedures in task-focused operating guidance. |
| [GitHub README guidance](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/customizing-your-repository/about-readmes) | Explain what the project does, why it is useful, how to start and where to get help; use relative links for repository documentation. |

These are specific sources for our choices, not a survey proving one universal industry style. Vendor marks identify technologies actually used by this system. Their sources and applicable terms are recorded in [Vendor icons](vendor-icons.md). The diagrams make no endorsement, performance or production-readiness claims.

## Read the figures

- **[System architecture](../diagrams/system-architecture.svg):** the engineer-facing local deployment, including execution boundaries, interfaces and retained state.
- **[Component architecture](../diagrams/module-architecture.svg):** module ownership, the allowed read contract and separate consistency boundaries inside one backend process.

- **[System map](../diagrams/overview.svg):** read from the people and browser into the single .NET application, then follow the labeled connections to storage, Kafka and local AI. The business modules live inside one application boundary.
- **[Assessment journey](../diagrams/journey.svg):** follow the numbered stages for one successful attempt. The human reviewer owns final scores and publication. Written answers do not require transcription; manual-only attempts skip AI processing.
- **[Architecture details](../architecture.md):** use this after the overview for the module contract, transaction, durable jobs and recovery. The existing runtime, sequence, recovery and Azure views answer narrower questions.

The diagrams summarize code verified in `src/Host`, `src/Assessments`, `src/Evaluation` and `compose.yaml`. Database schema names, endpoint details, retry internals and cloud resource nesting belong in those detailed views and source links, not in every node.

## Visual direction and editable sources

The already-selected **Laboratory Notebook** direction in [DESIGN.md](../../DESIGN.md) supplies the visual style. The reusable [Architecture Diagram skill](https://github.com/junrillg/skills/blob/main/skills/engineering/architecture-diagram/SKILL.md) supplies the icon-led architecture conventions: titled dashed execution groups, softly tinted responsibility areas, recognizable vendor marks and short directed connections. Diagram Design supplies reusable SVG layout and geometry checks; Clarity guides documentation wording. The skill is maintained separately from this application; the figures need no frontend dependency or network asset.

| Role | Project value |
|---|---|
| Paper / quiet field | `#ffffff` / `#f5f7fa` |
| Primary ink / supporting ink | `#172e45` / `#516377` |
| Accent and HTTP links | `#245ea7` |
| Rules / essential connector strokes | `#c4d0dc` / `#516377` |
| Type | Native system sans; monospace reserved for technical identifiers |

The opaque paper artboard keeps the SVG readable when GitHub uses a dark page. Labels and shapes carry meaning alongside color. Titled dashed frames distinguish execution and ownership scopes; they do not imply network isolation. Vendor marks retain their own approved colors. Short names, alignment, whitespace and orthogonal connectors establish hierarchy; the figures need no animation or network fonts.

| File | Edit for |
|---|---|
| [system-architecture.html](../diagrams/system-architecture.html), [module-architecture.html](../diagrams/module-architecture.html), [overview.html](../diagrams/overview.html), [journey.html](../diagrams/journey.html) | The final layout, text, styling and accessible inline SVG; also viewable locally in a browser |
| [system-architecture.mmd](../diagrams/system-architecture.mmd), [module-architecture.mmd](../diagrams/module-architecture.mmd), [overview.mmd](../diagrams/overview.mmd), [journey.mmd](../diagrams/journey.mmd) | The semantic components and flow, editable with Mermaid tools |
| Their corresponding `.svg` files | Committed README images, exported from the HTML sources |

Run `make diagrams` to export these curated figures and render the other Mermaid diagrams. It preserves the editorial layout instead of overwriting it with an automatic graph. Keep both the semantic models and the HTML content aligned when architecture changes. [Dependency setup](../operations.md#editable-diagrams).

## Verification scope

Check source accuracy, readable labels, connector geometry, SVG naming and bounds, keyboard/scroll access to the standalone pages, and local links. Inspect the figures against light and dark page surroundings and at desktop/mobile widths. Documentation verification is separate from the application's automated tests.

The system map expands the former six grouped elements into eight by separating people from the browser and transcription from scoring. No runtime dependency was removed. Module internals, schema names and recovery guards remain in the technical notes. The journey has seven stages plus the published-feedback outcome.
