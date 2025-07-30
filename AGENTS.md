# Contributor Guidelines

This project uses Unity and has a few conventions to keep things consistent.

## Running Edit Mode Tests

Instructions are mirrored from the README:

- **GUI:** Open the Test Runner window in the Unity Editor and run the **Edit Mode** tests.
- **CLI:**
  ```bash
  unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit
  ```
  The command exits non-zero on failure.

## Code Style (`Assets/Scripts/`)

- Indent with **four spaces**.
- Use **PascalCase** for class names, public members and methods.
- Use **camelCase** for private fields.
- Place opening braces on the **same line** as declarations.
- Include XML `<summary>` comments on public methods when appropriate.

## Workflow

- Every change should be committed with a clear message and tests should pass before opening a pull request.
- For first-time setup, run the existing `setup_env.sh` script.
