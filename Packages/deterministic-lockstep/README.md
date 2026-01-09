# Determinism Validation for Unity DOTS

A comprehensive determinism validation package for Unity DOTS (Data-Oriented Technology Stack). This package helps you verify that your ECS simulations produce identical results across multiple runs and platforms.

## Features

- **System-Level Validation**: Test individual ECS systems for determinism
- **Full-Game Validation**: Validate entire simulations by running multiple times and comparing hashes
- **Cross-Platform Comparison**: Export hash logs in standard JSON format for CI comparison
- **Session Recording & Replay**: Record game sessions and replay them for validation
- **Test Scenarios**: Framework for testing sparse/conditional system behavior
- **Detailed Logging**: Identify exactly which tick and system causes nondeterminism

## Installation

### Via Unity Package Manager

Add to your `manifest.json`:

```json
{
  "dependencies": {
    "com.michal-chrobot.determinism-validation": "https://github.com/michalooo/Thesis.git?path=Packages/deterministic-lockstep"
  }
}
```

### Via Git URL

1. Open Package Manager (Window > Package Manager)
2. Click "+" > "Add package from git URL"
3. Enter: `https://github.com/michalooo/Thesis.git?path=Packages/deterministic-lockstep`

## Quick Start

### 1. Mark Entities for Validation

Add `DeterministicEntityID` to entities that should be tracked:

```csharp
public class MyEntityAuthoring : MonoBehaviour
{
    class Baker : Baker<MyEntityAuthoring>
    {
        public override void Bake(MyEntityAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DeterministicEntityID { id = someUniqueId });
        }
    }
}
```

### 2. Add Systems to DeterministicSimulationSystemGroup

```csharp
[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
public partial struct MyGameSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Your deterministic game logic
    }
}
```

### 3. Validate Individual Systems

```csharp
var result = SystemValidator.ValidateWithSnapshot<MySystem>(world, numberOfRuns: 3);
if (!result.isDeterministic)
{
    Debug.LogError($"System is nondeterministic! First mismatch at run {result.firstMismatchRun}");
}
```

### 4. Validate Full Game

```csharp
var config = new ValidationConfig
{
    numberOfRuns = 2,
    ticksToSimulate = 1000,
    randomSeed = 12345,
    exportLogs = true
};

GameValidator.Instance.StartValidation(config);
GameValidator.Instance.OnValidationComplete += (result) =>
{
    if (result.nondeterminismDetected)
    {
        Debug.LogError($"Nondeterminism at tick {result.firstNondeterministicTick}");
    }
};
```

## Validation Modes

### System-Level Validation

Test if a specific system produces the same output given the same input:

```csharp
// Snapshot approach - captures world state, runs system N times
var result = SystemValidator.ValidateWithSnapshot<MySystem>(world, 3);

// Setup function approach - uses a function to create identical initial state
var result = SystemValidator.ValidateWithSetup<MySystem>(
    world,
    setup: (em) => { /* create entities */ },
    cleanup: (em) => { /* destroy entities */ },
    numberOfRuns: 3
);
```

### Full-Game Validation

Validate an entire game simulation:

```csharp
GameValidator.Instance.StartValidation(new ValidationConfig
{
    numberOfRuns = 2,
    ticksToSimulate = 1000,
    randomSeed = 12345
});
```

### Cross-Platform Validation

Export hash logs for comparison across platforms:

```csharp
// On each platform
var log = HashLogExporter.CreateFromSession(session);
HashLogExporter.ExportToJson(log, "platform_name.json");

// Compare logs
var result = HashLogComparer.CompareFiles("windows.json", "macos.json");
Debug.Log(HashLogComparer.GenerateReport(result));
```

## Common Sources of Nondeterminism

1. **Unordered Operations**: `EntityQuery.ToEntityArray()` without sorting
2. **Real Time**: Using `Time.deltaTime` instead of fixed simulation time
3. **Random Numbers**: Not using deterministic seeded random
4. **Hash Collections**: Iterating over HashSet/Dictionary (order not guaranteed)
5. **Floating Point**: Platform-specific floating point differences
6. **Parallel Jobs**: Race conditions in job scheduling

## Package Structure

```
Runtime/
├── Components.cs           # Core ECS components
├── DeterministicLogger.cs  # Logging utilities
├── Determinism/           # Hashing systems
├── Export/                # Hash log export/compare
├── Settings/              # Configuration authoring
├── Ticking/               # Fixed-step simulation
└── Validation/            # Validation framework
    ├── GameValidator.cs
    ├── SystemValidator.cs
    ├── TestScenario.cs
    ├── SessionRecorder.cs
    ├── SessionReplayer.cs
    └── WorldStateSnapshot.cs
```

## Requirements

- Unity 2022.3 or later
- Entities 1.4.4 or later
- Burst 1.8.0 or later

## Samples

Import samples via Package Manager:

- **System Validation Example**: Demonstrates system-level validation
- **Game Validation Example**: Demonstrates full-game validation

## License

MIT License - see LICENSE file for details.

## Author

Michał Chrobot - [LinkedIn](https://www.linkedin.com/in/micha%C5%82-chrobot/)

