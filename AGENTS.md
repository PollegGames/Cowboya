# Contributor Guidelines

This project uses Unity and has a few conventions to keep things consistent.

## Running Edit Mode Tests

Instructions are mirrored from the README:

- **GUI:** Open the Test Runner window in the Unity Editor and run the **Edit Mode** tests.
- **CLI:** `./Tools/run-editmode-tests.sh` (or `Tools/run-editmode-tests.ps1` on Windows).
  Results go to `Logs/CI/`; the script exits non-zero on failure. Close the project in
  the Unity Editor first, or use a separate clone.

## Code Style (`Assets/Scripts/`)

- Indent with **four spaces**.
- Use **PascalCase** for class names, public members and methods.
- Use **camelCase** for private fields.
- Place opening braces on the **same line** as declarations.
- Include XML `<summary>` comments on public methods when appropriate.

## Workflow

- Every change should be committed with a clear message and tests should pass before opening a pull request.
- For first-time setup, run `./setup_env.sh` to check git, the required Unity version and the .NET SDK. It never modifies the project.
