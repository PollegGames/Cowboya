# Cowboya

## Running Edit Mode Tests

This project uses Unity's built in Test Framework. Edit mode tests live under
`Assets/Tests/EditMode` and are compiled into their own assembly.

### Using the Unity Editor

1. Open the project in the Unity Editor.
2. Open **Window > General > Test Runner**.
3. Select the **Edit Mode** tab and click **Run All** to execute the tests.

### Using the Command Line

If you have the Unity Editor installed and available as `unity`, tests can be
run non‑interactively:

```bash
unity -runTests -testPlatform EditMode -projectPath "$(pwd)" -quit
```

The command will return a non‑zero exit code if any tests fail.
