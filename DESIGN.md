---
name: Candidate Assessment
description: Cool paper workspaces for accepted answers and human review.
colors:
  ink: "#172e45"
  muted: "#516377"
  line: "#c4d0dc"
  blue: "#245ea7"
  paper: "#fff"
  field: "#f5f7fa"
  danger: "#a72b32"
  control-border: "#71879d"
  blue-hover: "#194b89"
  acceptance-ink: "#334e6c"
typography:
  headline:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif"
    fontSize: "2rem"
    fontWeight: 650
    lineHeight: 1.2
    letterSpacing: "-0.025em"
  title:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif"
    fontSize: "1.25rem"
    fontWeight: 650
    lineHeight: 1.35
  section:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif"
    fontSize: "1rem"
    fontWeight: 650
    lineHeight: 1.55
  body:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif"
    fontSize: "1rem"
    fontWeight: 400
    lineHeight: 1.55
  label:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif"
    fontSize: "0.875rem"
    fontWeight: 600
    lineHeight: 1.55
  support:
    fontFamily: "-apple-system, BlinkMacSystemFont, \"Segoe UI\", sans-serif"
    fontSize: "0.875rem"
    fontWeight: 400
    lineHeight: 1.55
rounded:
  compact: "4px"
  field: "5px"
  action: "6px"
spacing:
  tight: "8px"
  control: "12px"
  small: "16px"
  medium: "20px"
  panel: "24px"
  roomy: "28px"
  section: "32px"
  wide: "40px"
components:
  button-primary:
    backgroundColor: "{colors.blue}"
    textColor: "{colors.paper}"
    rounded: "{rounded.action}"
    padding: "10px 18px"
  button-primary-hover:
    backgroundColor: "{colors.blue-hover}"
  button-secondary:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.action}"
    padding: "10px 18px"
  button-quiet:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.action}"
    padding: "10px 18px"
  button-danger:
    backgroundColor: "{colors.danger}"
    textColor: "{colors.paper}"
    rounded: "{rounded.action}"
    padding: "10px 18px"
  input:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "11px 12px"
    width: "100%"
  question-navigation:
    backgroundColor: "transparent"
    textColor: "{colors.ink}"
    rounded: "{rounded.field}"
    padding: "11px 10px"
    width: "100%"
  status-chip:
    backgroundColor: "#e7eef8"
    textColor: "#1b4d88"
    rounded: "{rounded.compact}"
    padding: "5px 10px"
  response-sheet:
    backgroundColor: "{colors.paper}"
    textColor: "{colors.ink}"
    rounded: "{rounded.action}"
    padding: "36px 40px"
  acceptance-band:
    backgroundColor: "{colors.field}"
    textColor: "{colors.ink}"
    padding: "22px 0"
---
# Design System: Candidate Assessment

## Overview

**Creative North Star: "The Laboratory Notebook"**

The Laboratory Notebook makes work legible through cool paper, ink-blue controls and ruled regions. White working sheets sit on a cool gray field; visible acceptance, question state and human scoring carry the visual emphasis.

The system is calm, direct and deliberately flat. Compact navigation supports spacious answer and evidence regions. The learning walkthrough uses the same paper, ink, blue focus and ruled divisions, with its own reading proportions. Source evidence is the shipped frontend stylesheet and components, plus the walkthrough’s inline styles; the recorded system follows those implementations rather than the preliminary direction.

**Key Characteristics:**

- Cool field and white working sheets.
- Ink-blue actions, explicit textual states and tabular quantities.
- Ruled separation without shadows or ornamental imagery.
- Persistent work context; responsive navigation preserves state.

## Colors

Cool white and blue-gray neutrals make one blue action accent easy to find. Frontmatter values are normative; component-local state treatments stay attached to their components.

### Primary

- **Ink Blue** (`blue`): primary buttons, links, checkbox accent, caret and keyboard focus. The deeper hover value belongs to primary controls.

### Neutral

- **Notebook Ink** (`ink`): headings, body and control text; also the login introduction’s dark ground.
- **Slate Annotation** (`muted`): supporting text and metadata.
- **Ruled Line** (`line`): sheet edges, dividers and table separation.
- **White Paper** (`paper`): response, review, preparation and form surfaces.
- **Cool Field** (`field`): page ground and quiet nested work areas.
- **Control Edge** (`control-border`): input strokes, outlined buttons and question-number boxes.
- **Acceptance Ink** (`acceptance-ink`): persistent acceptance summary and mobile state captions.

Danger is a functional red for stop recording, failure and urgent time. Informational notices use pale blue, success uses pale green, and offline/conflict notices use amber. Each treatment includes words; these semantic tints are not additional brand accents.

**The State Has Words Rule.** Pair state color with readable status text; selection, completion and acceptance remain distinct.

## Typography

**Display Font:** No independent display family or repeated display scale is established.
**Body Font:** Native system UI sans, using the frontmatter stack.
**Label/Mono Font:** The same sans; timers, answer counts, revisions and scores use tabular numerals.

The system uses a compact utilitarian hierarchy rather than an editorial font pairing. The native system face is the deliberate task-interface choice. The walkthrough adds `ui-sans-serif, system-ui` before the shared stack.

### Hierarchy

- **Headline:** page headings; on narrow screens the app reduces these to (1.75rem).
- **Title:** ordinary section headings.
- **Section:** smaller section headings, using the body size at the stronger heading weight.
- **Body:** base application prose with a maximum paragraph measure of (72ch).
- **Label / Support:** form labels and supporting copy share a size while differing in weight.
- **Working question:** candidate sheet title (1.75rem), narrowing to (1.5rem); reviewer evidence title (1.5rem).
- **Reading prose:** prompts and submitted evidence loosen leading to (1.65–1.8) according to region. The walkthrough body uses (1.6).

The ramp is a set of functional roles, not a fixed mathematical ratio. The isolated larger login headline and small audit/navigation captions do not create additional recommended roles.

**The Title First Rule.** Question and checkpoint titles precede their metadata. Keep labels in sentence case and quantities tabular.

## Layout

Ordinary app pages use a centered (1240px) maximum frame with (40px) padding. Workspaces use a wider (1664px) maximum frame with (32px) horizontal gutters. The repeated spacing steps are in frontmatter; they describe a compact, uneven task rhythm rather than a universal baseline grid.

Candidate work pairs a (250px) question index with a flexible response sheet, separated by (28px). The sheet caps at (1100px), with a spacious answer field. The timer, acceptance summary and review action share a sticky top band (`top: 0`, `z-index: 4`). The desktop question index sticks below it (`top: 130px`) and scrolls independently when needed. The band is sticky, not viewport-fixed. Reviewer work uses an index, evidence sheet and scoring sheet: (215px), `minmax(330px, 1fr)`, `minmax(300px, 0.8fr)`, separated by (24px).

At widths up to (1100px), candidate navigation narrows to (210px); reviewer scoring moves below evidence while the (180px) index remains alongside it. Page padding reduces. At widths up to (760px), both workspaces become one column, the work band stacks its context above the timer/actions, and question navigation becomes four columns of numbered controls with textual state. The final mobile navigation override uses `calc(25% - 4px)` width and a minimum (68px) height; older (44px) square rules are overridden. Full question titles and state remain in accessible names.

At widths from (1500px), candidate navigation becomes (260px), the gap becomes (40px), and response-sheet padding grows to (44px 56px). Standard sheet padding is in frontmatter; it becomes (28px) through the intermediate breakpoint and (24px 20px) on mobile.

The walkthrough uses a centered (1100px) reading frame, six lesson controls, and a `1.3fr 1fr` prose/diagram pair. At (760px), navigation becomes three columns and the diagram follows the prose. Its own reading proportions are not imposed on application workspaces.

## Elevation & Depth

No box shadows are used. White surfaces, cool grounds and thin blue-gray rules establish structure. Selection uses pale blue fill plus a border; completion adds a pale green numbered box. Focus is a visible blue outline (3px), offset (3px). The selected state does not replace keyboard focus.

**The Ruled Sheet Rule.** Use paper, tonal grounds and thin rules to separate work regions; the shipped system has no box shadows.

## Shapes

Forms are softly squared, with compact corners for tags and number boxes, slightly larger field corners and a shared action/sheet corner. Borders are generally (1px). The app includes isolated (7–8px) outer preparation/login shells; these do not establish another reusable radius scale. The receipt’s circular confirmation mark is a local semantic symbol, not the general container shape. Native audio controls and disclosure markers remain native; the book mark is an inline stroked SVG.

## Components

### Buttons

Quiet, solid controls with sentence-case labels. Primary, secondary, quiet and danger actions share the frontmatter geometry, a minimum height of (44px), and weight (600). Primary fills blue with white text; secondary uses white paper and the stronger control edge; quiet is transparent; danger fills red. Primary hover deepens blue, secondary hover uses pale blue and a blue edge, and quiet hover adds a pale blue ground. Buttons transition background and border over (0.16s ease-out). Disabled buttons use opacity (0.55) and a not-allowed cursor. Under reduced motion, transitions are removed. The compact mobile sign-out control is a local (36px) exception, not the standard target.

### Chips

Small rectangular status labels use compact corners, a pale blue ground and blue text. They communicate status and scores; they are not filter controls. Text remains the meaning of the chip.

### Cards / Containers

Response, evidence and scoring sheets are white, bordered and flat. Evidence and scoring sheets share (28px) desktop padding, narrowing to (22px 20px) on mobile. Use ruled internal sections instead of repeatedly nesting cards. Preserve long evidence through wrapping and pre-wrapped text where authored whitespace matters.

### Inputs / Fields

White full-width fields use the control edge, field corners and frontmatter padding. Labels sit above the input. Textareas resize vertically; the candidate answer region uses (18px) padding, a minimum (280px) height, and (1.8) leading, narrowing to (250px) minimum height on mobile. Focus uses the common outline. Errors are communicated in explicit notices, not an invented field-error border variant. Checkboxes use the blue accent.

### Navigation

Desktop question controls pair a numbered box with a title and state beneath it. Current selection uses pale blue fill, a blue-gray border and blue ink through `aria-current`. Answered numbers gain a green treatment while the text still says answered. Mobile keeps number and textual state; accessible names retain title and status. Candidate grouping distinguishes written from spoken responses. The walkthrough’s lesson controls use the same blue/white relationship, with solid blue for the current checkpoint.

### Persistent Acceptance Band

The candidate work band keeps assignment context, answered count, acceptance summary, remaining time and review action above the sheet. Acceptance text distinguishes local/pending work, syncing, failure, conflict and server-saved state. Timer numerals are tabular and become red below two minutes. Keep this explicit vocabulary when extending related surfaces.

### Notices and Evidence

Info, error and success notices use flat tinted grounds with matching borders and text; error notices have alert semantics. Offline and conflict regions remain visible in amber with recovery actions. Native disclosures hold rubric detail. Native audio controls appear only for available recordings; empty recording states describe the missing evidence. Reviewer suggestion quotations sit on a pale inset ground, separately from the human scoring form.

## Do's and Don'ts

### Do:

- **Do** preserve the distinction between local work, pending acceptance and server-saved work in persistent status text.
- **Do** keep question titles above metadata and reveal rubric detail through native disclosure.
- **Do** retain visible keyboard focus, textual mobile state, wrapping evidence and reduced-motion behavior.
- **Do** use the existing paper sheets and ruled sections for both candidate work and human review.

### Don't:

- **Don't** use success color alone as proof that work was accepted.
- **Don't** add ornamental notebook grids behind reading or response text.
- **Don't** merge AI suggestions into the visual or verbal identity of a final human score.
- **Don't** inherit the build’s 10–11px mobile navigation and audit metadata as a recommended type scale; these are carried density defects.


Recorded from `frontend/src/style.css`, `frontend/src/App.tsx` and `docs/follow-an-assessment.html`, with desktop/mobile review captures in `.impeccable/review/`. This is a source-grounded design record, not a claim of a comprehensive computed-style or accessibility audit. Product truth remains in PRODUCT.md; route-specific mode and composition remain outside this global visual system.
