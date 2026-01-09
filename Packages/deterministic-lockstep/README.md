# Determinism Validation for Unity DOTS

A powerful determinism validation and debugging toolkit for Unity's Entity Component System (ECS). Verify that your simulations produce identical results across multiple runs and platforms.

## Table of Contents

- [Why Determinism Matters](#why-determinism-matters)
- [Features](#features)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Validation Modes](#validation-modes)
- [API Reference](#api-reference)
- [Architecture](#architecture)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)
- [Requirements](#requirements)

## Why Determinism Matters

Determinism is critical for:
- **Multiplayer Games**: Lockstep netcode requires all clients to compute identical results
- **Replay Systems**: Recorded inputs must reproduce the same gameplay
- **Testing**: Automated tests need reproducible results
- **Debugging**: Consistent behavior makes bugs easier to track down
- **Cross-Platform**: Games must behave identically on Windows, macOS, consoles, etc.

This package helps you **find and fix nondeterminism** in your Unity DOTS projects.

## Features

| Feature | Description |
|---------|-------------|
| **System Validation** | Test individual ECS systems in isolation |
| **Game Validation** | Validate entire simulations across multiple runs |
| **Cross-Platform Export** | Export hash logs for CI/CD comparison |
| **Session Recording** | Record and replay game sessions |
| **Test Scenarios** | Framework for testing conditional behaviors |
| **World Snapshots** | Capture and restore ECS world state |
| **Detailed Logging** | Identify exact tick and system causing issues |

## Installation

### Option 1: Unity Package Manager (Git URL)

1. Open **Window > Package Manager**
2. Click **"+" > Add package from git URL**
3. Enter:
```
https://github.com/michalooo/Thesis.git?path=Packages/deterministic-lockstep
```

### Option 2: Edit manifest.json

Add to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.michal-chrobot.determinism-validation": "https://github.com/michalooo/Thesis.git?path=Packages/deterministic-lockstep"
  }
}
```

### Option 3: Local Development

Clone the repository and copy `Packages/deterministic-lockstep` to your project's `Packages` folder.

## Quick Start

### Step 1: Configure Settings

Add `DeterministicSettingsAuthoring` to a GameObject in your scene:

```csharp
// In the Unity Editor:
// 1. Create empty GameObject
// 2. Add Component > DeterministicSettingsAuthoring
// 3. Configure validation mode, tick rate, etc.
```

### Step 2: Mark Entities for Validation

Add the `DeterministicEntityAuthoring` component to entity prefabs you want to track:

```csharp
// Or manually in code:
public class MyEntityAuthoring : MonoBehaviour
{
    class Baker : Baker<MyEntityAuthoring>
    {
        public override void Bake(MyEntityAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            // Required: Unique deterministic ID for sorting
            AddComponent(entity, new DeterministicEntityID { id = GetNextId() });
            
            // Optional: Include in whitelist validation mode
            AddComponent<CountEntityForWhitelistedDeterminismValidation>(entity);
        }
    }
}
```

### Step 3: Add Systems to Deterministic Group

Place your game systems in `DeterministicSimulationSystemGroup`:

```csharp
[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
public partial struct MyGameSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // Use SystemAPI.Time.DeltaTime (fixed) not UnityEngine.Time.deltaTime
        var dt = SystemAPI.Time.DeltaTime;
        
        foreach (var (transform, velocity) in 
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<Velocity>>())
        {
            transform.ValueRW.Position += velocity.ValueRO.Value * dt;
        }
    }
}
```

### Step 4: Validate!

#### Validate a Single System

```csharp
using DeterministicLockstep;

// Snapshot approach: captures state, runs system N times, compares
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
    Debug.LogError($"Expected: {result.expectedHash:X16}");
    Debug.LogError($"Actual: {result.actualHash:X16}");
}
```

#### Validate Full Game

```csharp
using DeterministicLockstep;

// Configure validation
var config = new ValidationConfig
{
    numberOfRuns = 2,
    ticksToSimulate = 1000,
    randomSeed = 12345,
    exportLogs = true
};

// Start validation
GameValidator.Instance.StartValidation(config);

// Handle results
GameValidator.Instance.OnValidationComplete += (result) =>
{
    if (result.nondeterminismDetected)
    {
        Debug.LogError($"FAILED at tick {result.firstNondeterministicTick}");
    }
    else
    {
        Debug.Log($"PASSED: {result.numberOfRuns} runs identical!");
    }
};
```

## Validation Modes

### System-Level Validation

Test individual systems in isolation to find which one causes nondeterminism.

#### Snapshot Approach

Best for quick validation of existing game state:

```csharp
var result = SystemValidator.ValidateWithSnapshot<MySystem>(world, numberOfRuns: 3);
```

#### Setup Function Approach

Best for controlled, repeatable tests:

```csharp
var result = SystemValidator.ValidateWithSetup<MySystem>(
    world,
    setup: (em) =>
    {
        // Create identical initial state for each run
        for (int i = 0; i < 100; i++)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, new LocalTransform { Position = new float3(i, 0, 0) });
            em.AddComponentData(entity, new DeterministicEntityID { id = i });
        }
    },
    cleanup: (em) =>
    {
        // Clean up after each run
        var query = em.CreateEntityQuery(typeof(LocalTransform));
        em.DestroyEntity(query);
    },
    numberOfRuns: 3
);
```

### Full-Game Validation

Run entire simulation multiple times and compare hashes:

```csharp
GameValidator.Instance.StartValidation(new ValidationConfig
{
    numberOfRuns = 2,           // How many times to run
    ticksToSimulate = 1000,     // Ticks per run
    randomSeed = 12345,         // Same seed = same results
    exportLogs = true           // Export for debugging
});
```

### Test Scenarios

For testing sparse or conditional behaviors:

```csharp
public class CollisionScenario : ITestScenario
{
    public string Name => "Ball-Wall Collision";
    
    public void Setup(EntityManager em)
    {
        // Create ball heading toward wall
        var ball = em.CreateEntity();
        em.AddComponentData(ball, new LocalTransform { Position = new float3(9, 0, 0) });
        em.AddComponentData(ball, new Velocity { Value = new float3(1, 0, 0) });
        em.AddComponentData(ball, new DeterministicEntityID { id = 0 });
    }
    
    public void Execute(World world)
    {
        // Run physics for 10 ticks - should trigger collision
        var physicsSystem = world.GetExistingSystem<PhysicsSystem>();
        for (int i = 0; i < 10; i++)
        {
            physicsSystem.Update(world.Unmanaged);
        }
    }
    
    public void Cleanup(EntityManager em)
    {
        var query = em.CreateEntityQuery(typeof(Velocity));
        em.DestroyEntity(query);
    }
}

// Run the scenario
var result = ScenarioValidator.Validate(world, new CollisionScenario(), numberOfRuns: 3);
ScenarioValidator.LogResult(result);
```

### Cross-Platform Validation

Export hash logs for comparison across different machines:

```csharp
// On Machine 1 (Windows)
var session = SessionRecorder.Instance.StopRecording();
var log = HashLogExporter.CreateFromSession(session);
HashLogExporter.ExportToJson(log, "windows_hashes.json");

// On Machine 2 (macOS)
// ... run same simulation with same seed ...
HashLogExporter.ExportToJson(log, "macos_hashes.json");

// Compare
var result = HashLogComparer.CompareFiles("windows_hashes.json", "macos_hashes.json");
Console.WriteLine(HashLogComparer.GenerateReport(result));
```

## API Reference

### Core Components

| Component | Purpose |
|-----------|---------|
| `DeterministicEntityID` | Unique ID for deterministic entity sorting |
| `DeterminismValidationSettings` | Runtime validation state and configuration |
| `DeterministicSimulationTime` | Tracks current tick and timing |
| `DeterministicComponent` | Buffer marking component types to hash |
| `CountEntityForWhitelistedDeterminismValidation` | Tag for whitelist validation mode |

### Validation Classes

| Class | Purpose |
|-------|---------|
| `SystemValidator` | Validates individual ECS systems |
| `GameValidator` | Validates entire game simulations |
| `ScenarioValidator` | Validates custom test scenarios |
| `SessionRecorder` | Records game sessions for replay |
| `SessionReplayer` | Replays and validates recorded sessions |
| `WorldStateSnapshot` | Captures/restores ECS world state |

### Export Classes

| Class | Purpose |
|-------|---------|
| `HashLogExporter` | Exports hashes to JSON/binary |
| `HashLogComparer` | Compares hash logs from different runs/platforms |
| `DeterministicLogger` | Detailed logging for debugging |

### Key Enums

```csharp
// What to validate
public enum DeterminismValidationMode
{
    FullGame,       // Entire simulation
    PerSystem       // Individual systems
}

// How to compare hashes
public enum HashComparisonMode
{
    FinalHashOnly,      // Fast YES/NO answer
    PerTick,            // Find first divergent tick
    PerSystemPerTick    // Find exact system
}

// Which entities to include
public enum HashScope
{
    WhitelistedEntitiesOnly,    // Only tagged entities
    AllTrackedEntities          // All with DeterministicEntityID
}
```

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Your Game Systems                         │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ Movement │  │ Physics  │  │ Combat   │  │ Spawning │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│           DeterministicSimulationSystemGroup                 │
│  • Fixed tick rate (e.g., 60 ticks/sec)                     │
│  • Consistent Time.DeltaTime                                │
│  • Hash computation after each tick/system                  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│              StateHashForValidationSystem                    │
│  • Queries entities with DeterministicEntityID              │
│  • Sorts entities by ID (deterministic order)               │
│  • Hashes component data byte-by-byte                       │
│  • Stores hash in DeterministicSimulationTime               │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    Validation Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐         │
│  │ GameValid.  │  │ SystemValid.│  │ SessionRec. │         │
│  └─────────────┘  └─────────────┘  └─────────────┘         │
│                         │                                    │
│                         ▼                                    │
│  ┌─────────────────────────────────────────────────┐        │
│  │  HashLogExporter / HashLogComparer              │        │
│  │  Export to JSON → Compare across platforms      │        │
│  └─────────────────────────────────────────────────┘        │
└─────────────────────────────────────────────────────────────┘
```

## Best Practices

### DO

1. **Use fixed delta time**: Always use `SystemAPI.Time.DeltaTime` inside `DeterministicSimulationSystemGroup`
2. **Seed your random**: Use `Unity.Mathematics.Random` with a fixed seed
3. **Sort entity arrays**: When iterating entities, sort by `DeterministicEntityID`
4. **Assign unique IDs**: Every validated entity needs a unique `DeterministicEntityID`
5. **Start simple**: Validate with 100 ticks first, then increase
6. **Export logs**: Always export for debugging failed validations

### DON'T

1. **Don't use `Time.deltaTime`**: Use fixed simulation time instead
2. **Don't use `System.Random`**: Use seeded `Unity.Mathematics.Random`
3. **Don't iterate HashSets/Dictionaries**: Order is not guaranteed
4. **Don't use `EntityQuery.ToEntityArray()` without sorting**: Order varies
5. **Don't ignore floating point**: Cross-platform FP differences exist
6. **Don't run parallel jobs with race conditions**: Ensure deterministic scheduling

### Example: Deterministic Random

```csharp
[UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
public partial struct SpawnSystem : ISystem
{
    private Unity.Mathematics.Random _random;
    
    public void OnCreate(ref SystemState state)
    {
        // Initialize with fixed seed
        _random = new Unity.Mathematics.Random(12345);
    }
    
    public void OnUpdate(ref SystemState state)
    {
        // Deterministic random position
        var x = _random.NextFloat(-10f, 10f);
        var y = _random.NextFloat(-10f, 10f);
        // ...
    }
}
```

### Example: Deterministic Entity Iteration

```csharp
// BAD - order not guaranteed
var entities = query.ToEntityArray(Allocator.Temp);
foreach (var entity in entities) { /* ... */ }

// GOOD - sorted by deterministic ID
var entities = query.ToEntityArray(Allocator.Temp);
var ids = new NativeArray<DeterministicEntityID>(entities.Length, Allocator.Temp);
for (int i = 0; i < entities.Length; i++)
{
    ids[i] = entityManager.GetComponentData<DeterministicEntityID>(entities[i]);
}
ids.Sort();
// Now iterate in deterministic order
```

## Troubleshooting

### "Validation fails on first tick"

**Cause**: Initial state is different between runs.

**Solution**: Ensure random seed is set before any entity creation:
```csharp
var settings = SystemAPI.GetSingleton<DeterminismValidationSettings>();
_random = new Unity.Mathematics.Random(settings.randomSeed);
```

### "Validation fails at random ticks"

**Cause**: Usually floating-point accumulation or race conditions.

**Solutions**:
1. Use `PerSystemPerTick` mode to find exact system
2. Check for parallel job race conditions
3. Look for `Time.deltaTime` usage (should be fixed time)

### "Cross-platform hashes differ"

**Cause**: Floating-point differences between platforms.

**Solutions**:
1. Use integer math where possible
2. Round/quantize floating-point values
3. Use `math.round()` before hashing

### "Hash changes when adding unrelated entities"

**Cause**: New entities may affect query iteration order.

**Solution**: Always sort entities by `DeterministicEntityID` before processing.

### "Session replay doesn't match"

**Cause**: State restoration incomplete or random not reset.

**Solutions**:
1. Verify all relevant components are in `DeterministicComponent` buffer
2. Reset random seed before replay
3. Ensure entity IDs are restored correctly

## Requirements

| Dependency | Version |
|------------|---------|
| Unity | 2022.3+ |
| Entities | 1.4.4+ |
| Burst | (via Entities) |

## Samples

Import via Package Manager:

- **System Validation Example**: Demonstrates validating individual systems
- **Game Validation Example**: Demonstrates full-game validation with recording

## License

MIT License - see LICENSE file for details.

## Author

Michał Chrobot - [LinkedIn](https://www.linkedin.com/in/micha%C5%82-chrobot/)

## Contributing

Contributions welcome! See the repository for guidelines.
