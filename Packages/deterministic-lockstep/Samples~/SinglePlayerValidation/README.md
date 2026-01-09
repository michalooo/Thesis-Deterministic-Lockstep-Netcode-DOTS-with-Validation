# Single-Player Validation Example

This sample demonstrates how to use the determinism validation package to validate a single-player game for determinism.

## Overview

Single-player validation works by running the same simulation multiple times with identical inputs/seeds and comparing the resulting state hashes. If the hashes match across all runs, the simulation is deterministic. If they differ, nondeterminism is detected.

## Setup

1. Add the `DeterministicSettingsAuthoring` component to a GameObject in your scene
2. Set the `Validation Mode` to `SinglePlayer`
3. Configure the number of validation runs and ticks to simulate
4. Add the `SinglePlayerValidationManager` component to a GameObject (or create it via script)

## Usage

### Via Script

```csharp
using DeterministicLockstep;
using UnityEngine;

public class ValidationExample : MonoBehaviour
{
    void Start()
    {
        // Create validation manager if it doesn't exist
        if (SinglePlayerValidationManager.Instance == null)
        {
            var go = new GameObject("ValidationManager");
            go.AddComponent<SinglePlayerValidationManager>();
        }
        
        // Subscribe to events
        SinglePlayerValidationManager.Instance.OnValidationComplete += OnValidationComplete;
        SinglePlayerValidationManager.Instance.OnNondeterminismDetected += OnNondeterminismDetected;
        
        // Start validation
        var config = new ValidationConfig
        {
            numberOfRuns = 3,
            ticksToSimulate = 1000,
            randomSeed = 12345
        };
        
        SinglePlayerValidationManager.Instance.StartValidation(config);
    }
    
    void OnValidationComplete(ValidationResult result)
    {
        if (result.nondeterminismDetected)
        {
            Debug.LogError($"Nondeterminism detected at tick {result.firstNondeterministicTick}!");
        }
        else
        {
            Debug.Log("Simulation is deterministic!");
        }
    }
    
    void OnNondeterminismDetected(int tick, int runIndex, ulong expectedHash, ulong actualHash)
    {
        Debug.LogError($"Tick {tick}: Expected {expectedHash}, got {actualHash} (run {runIndex})");
    }
}
```

### Via Inspector

1. Add `SinglePlayerValidationManager` to your scene
2. Configure `DeterministicSettingsAuthoring`:
   - Set `Validation Mode` to `SinglePlayer`
   - Set `Number Of Validation Runs` (default: 2)
   - Set `Ticks To Simulate` (default: 1000)
3. Use the `ValidationTrigger` component to start validation

## Key Components

- **SinglePlayerValidationManager**: Manages the validation process
- **DeterminismValidationSettings**: ECS component storing validation state
- **ValidationConfig**: Configuration struct for validation parameters
- **ValidationResult**: Result struct containing validation outcome

## Notes

- Ensure your game systems use the `DeterministicEntityID` component for entities that should be validated
- Add components to the `DeterministicComponent` buffer to include them in hash calculations
- Use the same random seed for reproducible results

