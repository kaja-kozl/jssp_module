# PLANNING.md — Planning Mode Skill

## Purpose

This skill governs how Claude approaches every new feature in the JSSP project. No implementation code is written until a complete plan exists and has been confirmed. Planning is not a formality — it is where architectural mistakes are caught cheaply, before they exist in code.

---

## Trigger

This skill activates whenever:
- A new feature, class, or interface is requested
- An existing component needs significant modification
- A bug fix touches more than one file
- A refactor is proposed

---

## Step 0 — Codebase Research (Always First)

Before producing any plan, Claude MUST:

1. **Read `CLAUDE.md`** — re-read the full document, not from memory. Identify which architectural decisions (A1–A9) are relevant to the feature being planned.
2. **Scan the existing solution structure** — list which files already exist that the feature will touch or depend on.
3. **Check for conflicts** — does the proposed feature contradict any decision in `CLAUDE.md`? If so, surface the conflict explicitly before planning.
4. **Check registered skill files** — if a relevant skill exists (e.g. `TERMINALGUI.md`, `ALGORITHMS.md`), read it now.

Only after completing Step 0 does Claude proceed to the plan template.

---

## Plan Template

Every plan MUST use this exact structure. No section may be omitted. If a section is not applicable, write "N/A — [reason]" rather than leaving it blank.

```
══════════════════════════════════════════════════════
PLAN: [Feature Name]
Status: DRAFT | APPROVED | IN PROGRESS | COMPLETE
══════════════════════════════════════════════════════

## 1. Purpose
One to three sentences. What problem does this feature solve?
What user-visible or system-level outcome does it produce?

## 2. Architectural Decisions Touched
List every CLAUDE.md decision (A1–A9) this feature interacts with.
For each one, state whether it is being followed, extended, or whether
a deviation is proposed (deviations require explicit justification).

  - A1 (Graph Orchestration): [how this feature fits into the flow graph]
  - A2 (Disjunctive Graph): [relevant / not relevant — why]
  - A3 (Algorithm Architecture): [relevant / not relevant — why]
  - ... (only list decisions that apply)

## 3. Classes and Interfaces
List every class or interface involved. For each one state:
  - NEW / MODIFIED / UNCHANGED
  - Which project it lives in (JSSP.Core / JSSP.Algorithms / JSSP.Orchestration / JSSP.IO / JSSP.UI / JSSP.Tests)
  - A one-line description of its role in this feature

## 4. Files to Create or Modify
Exact file paths relative to solution root. No ambiguity.

  CREATE:
    - JSSP.Core/Graph/CriticalPathFinder.cs
  MODIFY:
    - JSSP.Algorithms/Tabu/TabuSearch.cs  — add dynamic CP recomputation call

## 5. Data Flow
Trace the data from its entry point to its final output, step by step.
Use arrows (→) to show transitions. Be specific about what object
holds the data at each stage and what transformation occurs.

  Example:
  CsvLoader.LoadAsync(path) → List<Job>
    → DisjunctiveGraph.Build(jobs) → DisjunctiveGraph
    → CriticalPathFinder.Find(graph) → List<GraphNode> (critical path)
    → N7Neighbourhood.GenerateMoves(criticalPath) → List<Move>
    → TabuSearch.EvaluateMoves(moves) → Schedule (best neighbour)

## 6. Flow Graph Integration
How does this feature connect to the execution flow graph (A1)?
  - Which FlowNode(s) trigger this feature?
  - What NodeSignal does successful completion return?
  - What NodeSignal does failure return?
  - Are new nodes or edges needed? If so, draw them:
      [ExistingNode] --signal--> [NewNode] --signal--> [NextNode]

## 7. OOP Pillar Coverage
Explicitly state which of the four pillars this feature demonstrates
and exactly where in the code each pillar appears.

  - Encapsulation: [class name — what is hidden and why]
  - Abstraction: [interface name — what is exposed vs hidden]
  - Inheritance: [parent → child relationship if applicable, or N/A]
  - Polymorphism: [where a base type reference is used for a concrete type]

## 8. Test Plan
List every test case that must be written before this feature is
considered complete. Use the AAA format description. All test data
must be hardcoded — no dependency on external CSV files.

  Test 1: [Name]
    Arrange: [describe setup — concrete values]
    Act:     [method called]
    Assert:  [exact expected outcome]

  Test 2: ...

  Edge cases to cover:
    - [empty input]
    - [single job, single operation]
    - [all operations on the same machine]
    - [any domain-specific edge case]

## 9. Risks and Open Questions
List anything uncertain, potentially problematic, or requiring a
decision before implementation can begin.

  Risks:
    - [e.g. Thread safety if convergence event fires during UI update]
    - [e.g. Performance regression if critical path called in tight loop]

  Open Questions (Claude asks the user to answer these before proceeding):
    - [Specific question that cannot be resolved from existing context]
    - [Another question if genuinely ambiguous]

  Note: Claude should ask open questions here rather than making
  silent assumptions. If Claude makes an assumption, it must be
  stated explicitly so the user can correct it.

## 10. Big O Analysis
State the time and space complexity of the new code being added.
Compare to any existing code it replaces or extends.

  Time:  O(?) — [brief justification]
  Space: O(?) — [brief justification]
  Regression risk: [none | low | high — explain if high]

## 11. Progress Tracker
Updated as implementation proceeds. Claude updates this block
after each sub-task is complete.

  [ ] Step 0 — Codebase research complete
  [ ] Plan approved by user
  [ ] Classes/interfaces created (stubs)
  [ ] Core logic implemented
  [ ] Unit tests written and passing
  [ ] Feedback loop completed
  [ ] CLAUDE.md updated if any new decisions were made
══════════════════════════════════════════════════════
```

---

## Open Questions Protocol

When Claude identifies genuine ambiguity in a plan, it MUST ask the question rather than silently assuming. Questions should be:

- **Specific** — not "how should this work?" but "should `FitnessCache` use a `string` hash key or a `int[]` structural hash? The string approach is simpler but allocates more; the structural hash is faster but more complex to implement correctly."
- **Binary or multiple-choice where possible** — give the user concrete options with trade-offs stated
- **Ordered by importance** — most blocking questions first
- **Minimal** — only ask what cannot be inferred from `CLAUDE.md`, the literature, or the existing codebase

Claude should never ask questions whose answers are already in `CLAUDE.md`. If the answer is there, state "Per CLAUDE.md decision A[n], I will use X" and proceed.

---

## Architecture Improvement Protocol

When researching the codebase for a plan, Claude should actively look for opportunities to improve the existing architecture. If Claude notices:

- A class with too many responsibilities (low cohesion)
- Two classes that are tightly coupled without a clear reason
- A pattern that contradicts a `CLAUDE.md` decision
- A missing interface that would enable better polymorphism
- Dead code, duplicate logic, or an inefficient data structure

...it should flag this in a separate block **before** the plan:

```
## ARCHITECTURE NOTE
[What was found]
[Why it is a problem]
[Proposed improvement]
[Impact on current plan — does it block, complement, or conflict?]
```

The user decides whether to address it now, defer it, or dismiss it. Claude does not refactor silently.

---

## Plan States and Transitions

```
DRAFT
  └─► User reviews and asks questions / requests changes
        └─► DRAFT (revised)
              └─► User says "approved" or "proceed"
                    └─► APPROVED
                          └─► Claude begins implementation
                                └─► IN PROGRESS (progress tracker updates)
                                      └─► All progress items checked
                                            └─► Feedback loop complete
                                                  └─► COMPLETE
```

A plan may only move to IN PROGRESS after the user explicitly approves it or says "proceed". Claude must not begin writing code based on implicit approval.

---

## What a Good Plan Looks Like

A good plan is one where:
- A second developer could implement the feature from the plan alone without asking questions
- Every class name is final (not "some kind of cache class")
- Every data transformation is described precisely
- Every test case has concrete input values, not vague descriptions
- The flow graph integration is explicit — no "hooks into the UI somehow"
- Risks are honest — if something might be hard, say so

A bad plan is one that uses vague language ("it will probably connect to the algorithm somehow"), skips sections, or defers decisions to implementation time.

---

## Integration with Feedback Loop

When a plan reaches COMPLETE status, Claude immediately triggers the `FEEDBACK.md` protocol. The feedback loop closes the loop on the plan — it checks whether what was built matches what was planned, and whether the architecture is still coherent after the addition.

If the feedback loop finds a discrepancy between the plan and the implementation, it is recorded in the FEEDBACK block and the plan status is updated to reflect the deviation. `CLAUDE.md` is updated if the deviation represents a new architectural decision.
