# System Validation Sample

This sample demonstrates how to validate individual ECS systems for determinism.

## Overview

System-level validation is useful for:
- Testing if a specific system is deterministic
- Finding which system causes nondeterminism in a larger game
- Creating automated tests for system determinism

## Files

- `ExampleSystem.cs` - A simple example system to validate
- `SystemValidationExample.cs` - MonoBehaviour that demonstrates system validation
- `SystemValidationExample.asmdef` - Assembly definition for the sample

## Usage

### Method 1: Snapshot-based Validation

```csharp
// Captures current world state, runs system N times, compares outputs
var result = SystemValidator.ValidateWithSnapshot<MySystem>(world, numberOfRuns: 3);
SystemValidator.LogResult(result);
```

### Method 2: Setup Function Validation

```csharp
// Uses a setup function to create identical initial state for each run
var result = SystemValidator.ValidateWithSetup<MySystem>(
    world,
    setup: (em) => {
        // Create your test entities here
        var entity = em.CreateEntity();
        em.AddComponentData(entity, new Position { Value = float3.zero });
    },
    cleanup: (em) => {
        // Clean up entities after each run
    },
    numberOfRuns: 3
);
```

### Method 3: Test Scenario

```csharp
// For more complex scenarios, implement ITestScenario
public class MyScenario : ITestScenario
{
    public string Name => "My Custom Scenario";
    
    public void Setup(EntityManager em) { /* ... */ }
    public void Execute(World world) { /* ... */ }
    public void Cleanup(EntityManager em) { /* ... */ }
}

var result = ScenarioValidator.Validate(world, new MyScenario(), numberOfRuns: 3);
```

## Common Sources of Nondeterminism

1. **Unordered operations** - Using `EntityQuery.ToEntityArray()` without sorting
2. **Time.deltaTime** - Using real time instead of fixed simulation time
3. **Random** - Not using deterministic seeded random
4. **Floating point** - Different platforms may have different precision
5. **Hash maps/sets** - Iteration order is not guaranteed

