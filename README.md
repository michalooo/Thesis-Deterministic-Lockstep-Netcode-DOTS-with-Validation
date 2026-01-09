# Determinism Validation for Unity DOTS

A research project and toolkit for validating and debugging determinism in Unity's Entity Component System (ECS).

## Project Overview

This project provides a Unity package for **determinism validation** - the process of verifying that a simulation produces identical results across:
- Multiple runs on the same machine
- Different platforms (Windows, macOS, Linux, consoles)
- Different hardware configurations

### Why Determinism Matters

Deterministic simulations are essential for:

| Use Case | Why Determinism? |
|----------|------------------|
| **Multiplayer Games** | Lockstep netcode requires all clients to compute identical game states |
| **Replay Systems** | Recorded inputs must reproduce exact gameplay |
| **Competitive Gaming** | Fair play requires consistent behavior across machines |
| **Testing & QA** | Reproducible bugs are easier to fix |
| **Cross-Platform** | Players expect the same experience everywhere |

### The Challenge

Unity DOTS (Data-Oriented Technology Stack) introduces unique challenges for determinism:
- **Entity iteration order** is not guaranteed
- **Parallel job scheduling** can vary between runs
- **Floating-point operations** differ across platforms
- **System update order** must be carefully controlled

This package provides tools to **detect, locate, and debug** nondeterminism in DOTS projects.

## Package Features

The `com.michal-chrobot.determinism-validation` package provides:

- **System-Level Validation**: Test individual ECS systems in isolation
- **Full-Game Validation**: Compare multiple simulation runs tick-by-tick
- **Cross-Platform Export**: JSON hash logs for CI/CD comparison (Yamato, GitHub Actions)
- **Session Recording**: Record and replay game sessions for validation
- **Test Scenarios**: Framework for testing conditional/sparse behaviors
- **Detailed Logging**: Identify exact tick and system causing issues

## Installation

### Via Git URL (Recommended)

1. Open **Window > Package Manager**
2. Click **"+" > Add package from git URL**
3. Enter:
```
https://github.com/michalooo/Thesis.git?path=Packages/deterministic-lockstep
```

### Via manifest.json

Add to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.michal-chrobot.determinism-validation": "https://github.com/michalooo/Thesis.git?path=Packages/deterministic-lockstep"
  }
}
```

## Quick Example

```csharp
using DeterministicLockstep;
using Unity.Entities;

// Validate a system
var result = SystemValidator.ValidateWithSnapshot<MyGameSystem>(
    World.DefaultGameObjectInjectionWorld,
    numberOfRuns: 3
);

if (result.isDeterministic)
{
    Debug.Log("System is deterministic!");
}
else
{
    Debug.LogError($"Nondeterminism detected at run {result.firstMismatchRun}");
}
```

See the [Package README](Packages/deterministic-lockstep/README.md) for complete documentation.

## Repository Structure

```
├── Packages/
│   └── deterministic-lockstep/     # The validation package
│       ├── Runtime/
│       │   ├── Components.cs       # Core ECS components
│       │   ├── Validation/         # Validation framework
│       │   ├── Export/             # Hash log export/compare
│       │   ├── Determinism/        # Hashing systems
│       │   ├── Settings/           # Configuration
│       │   └── Ticking/            # Fixed-step simulation
│       ├── Samples~/               # Example projects
│       └── README.md               # Package documentation
│
├── Assets/
│   └── Pong/                       # Sample Pong game
│
└── README.md                       # This file
```

## Research Context

This project originated from thesis research on deterministic simulation in game development:

> **"Deterministic Lockstep Netcode Model with Determinism Validation and Debugging Tooling for Unity DOTS"**

The research explores:
1. How to achieve deterministic ECS simulations in Unity DOTS
2. Techniques for detecting nondeterminism (hash comparison, per-system validation)
3. Tools for debugging and locating sources of nondeterminism
4. Cross-platform validation approaches

## Common Sources of Nondeterminism

| Source | Problem | Solution |
|--------|---------|----------|
| `Time.deltaTime` | Varies with frame rate | Use fixed simulation time |
| `System.Random` | Not seeded | Use `Unity.Mathematics.Random` with seed |
| Entity iteration | Order not guaranteed | Sort by `DeterministicEntityID` |
| HashSet/Dictionary | Iteration order varies | Use sorted collections |
| Parallel jobs | Race conditions | Ensure deterministic scheduling |
| Floating-point | Platform differences | Quantize or use fixed-point |

## Requirements

- Unity 2022.3 or later
- Entities package 1.4.4 or later

## Future Work

Planned improvements:

- **Automatic Entity ID Assignment**: Code generation to auto-assign `DeterministicEntityID`
- **Enhanced Logging**: Auto-generate component field logging
- **Performance Mode**: Skip visuals during validation runs
- **Yamato Integration**: Pre-built CI/CD job templates
- **Multiplayer Extension**: Add deterministic lockstep netcode support

## Contributing

Contributions are welcome! To contribute:

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## License

MIT License - free for commercial and non-commercial use.

## Author

**Michał Chrobot**
- [LinkedIn](https://www.linkedin.com/in/micha%C5%82-chrobot/)
- [GitHub](https://github.com/michalooo)

## Acknowledgments

- Unity Technologies for the DOTS framework
- The game development community for research on deterministic simulation
