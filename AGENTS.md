# Contributor Guidelines

This project uses Unity and has a few conventions to keep things consistent.

## Branch and Documentation

- `tourB` is the active branch (full rewrite). `main` is the old prototype; do
  not use it to describe or change the current game.
- Read the root `README.md` (current game), `Assets/ReadMe.md` (vision and idea
  bank) and `Docs/README.md` (index of design documents) before large changes.
- Current priorities: start of the game (Levels 1-3), the laboratory, and
  reliable robot passing (grab and rotate a robot to go over/under it). Saving
  is intentionally postponed.

## Running Edit Mode Tests

Instructions are mirrored from the README:

- **GUI:** Open the Test Runner window in the Unity Editor and run the **Edit Mode** tests.
- **CLI (Windows):**
  ```powershell
  powershell -ExecutionPolicy Bypass -File Tools/run-editmode-tests.ps1
  ```
- **CLI (Linux/macOS):** `./Tools/run-editmode-tests.sh`

  Both exit non-zero on failure. Use a separate clone when the project is already open in Unity.

## Code Style (`Assets/Scripts/`)

- Indent with **four spaces**.
- Use **PascalCase** for class names, public members and methods.
- Use **camelCase** for private fields.
- Place opening braces on the **same line** as declarations.
- Include XML `<summary>` comments on public methods when appropriate.

## Workflow

- Every change should be committed with a clear message and tests should pass before opening a pull request.
- `setup_env.sh` is outdated (it targets Unity 2021); use Unity `6000.3.22f1` as pinned in `ProjectSettings/ProjectVersion.txt`.
