# Determinism Validation for Unity DOTS

A powerful toolkit for validating and debugging determinism in Unity's Entity Component System (ECS).

## Overview

This package helps you verify that your ECS simulations produce **identical results** across:
- Multiple runs on the same machine
- Different platforms (Windows, macOS, Linux, consoles)
- Different hardware configurations

### Why Determinism Matters

| Use Case | Why Determinism? |
|----------|------------------|
| **Multiplayer Games** | Lockstep netcode requires all clients to compute identical game states |
| **Replay Systems** | Recorded inputs must reproduce exact gameplay |
| **Competitive Gaming** | Fair play requires consistent behavior across machines |
| **Testing & QA** | Reproducible bugs are easier to fix |
| **Cross-Platform** | Players expect the same experience everywhere |

### The Challenge

Unity DOTS introduces unique challenges for determinism:
- **Entity iteration order** is not guaranteed
- **Parallel job scheduling** can vary between runs
- **Floating-point operations** differ across platforms
- **System update order** must be carefully controlled

This package provides tools to **detect, locate, and debug** nondeterminism.

## Features

- **System-Level Validation**: Test individual ECS systems in isolation
- **Full-Game Validation**: Compare multiple simulation runs tick-by-tick
- **Cross-Platform Export**: JSON hash logs for CI/CD comparison
- **Session Recording**: Record and replay game sessions for validation
- **Test Scenarios**: Framework for testing conditional/sparse behaviors
- **Detailed Logging**: Identify exact tick and system causing issues

## Installation

### Via Git URL (Recommended)

1. Open **Window > Package Manager**
2. Click **"+" > Add package from git URL**
3. Enter:
```
https://github.com/michalooo/Thesis.git
```

### Via manifest.json

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.michal-chrobot.determinism-validation": "https://github.com/michalooo/Thesis.git"
  }
}
```

## Quick Start

### 1. Configure Settings

Add `DeterministicSettingsAuthoring` component to a GameObject in your scene.

### 2. Mark Entities for Validation

```csharp
// Add to entity prefabs
AddComponent(entity, new DeterministicEntityID { id = uniqueId });
AddComponent<CountEntityForWhitelistedDeterminismValidation>(entity);
```

### 3. Add Systems to Deterministic Group

```csharp
[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
public partial struct MyGameSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Use SystemAPI.Time.DeltaTime (fixed)
        var dt = SystemAPI.Time.DeltaTime;
        // Your deterministic game logic
    }
}
```

### 4. Validate

```csharp
using DeterministicLockstep;

// Validate a single system
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
    Debug.LogError($"Nondeterminism at run {result.firstMismatchRun}");
}

// Or validate full game
GameValidator.Instance.StartValidation(new ValidationConfig
{
    numberOfRuns = 2,
    ticksToSimulate = 1000,
    randomSeed = 12345
});
```

## Repository Structure

```
├── Runtime/
│   ├── Components.cs           # Core ECS components
│   ├── Validation/             # Validation framework
│   │   ├── GameValidator.cs
│   │   ├── SystemValidator.cs
│   │   ├── TestScenario.cs
│   │   ├── SessionRecorder.cs
│   │   ├── SessionReplayer.cs
│   │   └── WorldStateSnapshot.cs
│   ├── Export/                 # Hash log export/compare
│   │   ├── HashLogExporter.cs
│   │   └── HashLogComparer.cs
│   ├── Determinism/            # Hashing systems
│   ├── Settings/               # Configuration authoring
│   └── Ticking/                # Fixed-step simulation
├── Samples~/
│   ├── SystemValidation/       # System validation example
│   └── GameValidation/         # Full game validation example
├── package.json
└── README.md
```

## Common Sources of Nondeterminism

| Source | Problem | Solution |
|--------|---------|----------|
| `Time.deltaTime` | Varies with frame rate | Use fixed simulation time |
| `System.Random` | Not seeded | Use `Unity.Mathematics.Random` with seed |
| Entity iteration | Order not guaranteed | Sort by `DeterministicEntityID` |
| HashSet/Dictionary | Iteration order varies | Use sorted collections |
| Parallel jobs | Race conditions | Ensure deterministic scheduling |
| Floating-point | Platform differences | Quantize or use fixed-point |

## API Overview

### Core Components

| Component | Purpose |
|-----------|---------|
| `DeterministicEntityID` | Unique ID for deterministic entity sorting |
| `DeterminismValidationSettings` | Runtime validation state |
| `DeterministicSimulationTime` | Tracks current tick and timing |
| `CountEntityForWhitelistedDeterminismValidation` | Tag for whitelist mode |

### Validation Classes

| Class | Purpose |
|-------|---------|
| `SystemValidator` | Validates individual ECS systems |
| `GameValidator` | Validates entire game simulations |
| `ScenarioValidator` | Validates custom test scenarios |
| `SessionRecorder` | Records game sessions |
| `SessionReplayer` | Replays and validates sessions |
| `HashLogExporter` | Exports hashes to JSON |
| `HashLogComparer` | Compares hash logs |

## Requirements

- Unity 6 (6000.3) or later
- Entities 1.4.4 or later

## Samples

Import samples via Package Manager:

- **System Validation**: Demonstrates validating individual systems
- **Game Validation**: Demonstrates full-game validation with recording

## Research Context

This project originated from thesis research:

> **"Deterministic Lockstep Netcode Model with Determinism Validation and Debugging Tooling for Unity DOTS"**

## Future Work

- **Automatic Entity ID Assignment**: Code generation for `DeterministicEntityID`
- **Enhanced Logging**: Auto-generate component field logging
- **Performance Mode**: Skip visuals during validation
- **Yamato Integration**: Pre-built CI/CD job templates
- **Multiplayer Extension**: Deterministic lockstep netcode support

## Contributing

Contributions are welcome! Fork the repository, create a feature branch, and submit a pull request.

## License

MIT License - free for commercial and non-commercial use.

## Author

**Michał Chrobot** - [LinkedIn](https://www.linkedin.com/in/micha%C5%82-chrobot/) | [GitHub](https://github.com/michalooo)
