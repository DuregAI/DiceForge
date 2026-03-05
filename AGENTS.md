# DiceForge Agent Instructions

## Project Baseline
- Unity version is `6000.3.9f1` (`ProjectSettings/ProjectVersion.txt`).
- Treat this as a Unity 6.3 URP project with UI Toolkit.
- Do not introduce features/packages requiring a different Unity version unless explicitly requested.

## Coding Preferences (C# + Unity)
- Follow Microsoft C# conventions, but prioritize consistency with nearby existing repo code.
- Prefer clear, small methods and explicit guard clauses for invalid state.
- Prefer descriptive names over abbreviations.
- Avoid large refactors unless requested; make focused, practical changes.
- Minimize allocations in gameplay loops and per-frame paths.
- Keep logs concise and useful with context when needed.
- Match existing style/nullability patterns in touched files; avoid unrelated style churn.

## Architecture Defaults
- Prefer data-driven design: ScriptableObject configs for authored gameplay data.
- Keep core rules/simulation logic pure C# where practical.
- Keep MonoBehaviours thin: orchestration, binding, scene wiring.
- Keep UI Toolkit controllers focused on presentation and interaction.
- Prefer explicit ownership of shared runtime state.

## Output Expectations for Agent Responses
- Start with what changed and why.
- List touched files with brief purpose.
- Note assumptions and next recommended steps if relevant.
