# Game Validation Sample

This sample demonstrates how to validate an entire game simulation for determinism.

## Overview

Full-game validation is useful for:
- Verifying that your entire game is deterministic
- Finding the first tick where nondeterminism occurs
- Exporting hash logs for cross-platform comparison
- Recording and replaying game sessions

## Files

- `GameValidationExample.cs` - MonoBehaviour that demonstrates game validation
- `GameValidation.asmdef` - Assembly definition for the sample

## Usage

### Basic Validation

```csharp
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

// Listen for completion
GameValidator.Instance.OnValidationComplete += (result) =>
{
    if (result.nondeterminismDetected)
    {
        Debug.LogError($"Nondeterminism at tick {result.firstNondeterministicTick}");
    }
    else
    {
        Debug.Log("Game is deterministic!");
    }
};
```

### Recording Sessions

```csharp
// Start recording
SessionRecorder.Instance.StartRecording(world, randomSeed: 12345);

// ... run your game ...

// Stop and save
var session = SessionRecorder.Instance.StopRecording();
SessionRecorder.Instance.SaveSession(session, "my_session.json");
```

### Replaying Sessions

```csharp
// Load and replay
var session = SessionRecorder.LoadSession("path/to/session.json");
SessionReplayer.Instance.StartReplay(session);

SessionReplayer.Instance.OnReplayComplete += (result) =>
{
    if (result.matchesOriginal)
    {
        Debug.Log("Replay matches original recording!");
    }
};
```

### Cross-Platform Comparison

```csharp
// On each platform, export a hash log
var log = HashLogExporter.CreateFromSession(session);
HashLogExporter.ExportToJson(log, "platform_name.json");

// Later, compare logs from different platforms
var result = HashLogComparer.CompareFiles("windows.json", "macos.json");
var report = HashLogComparer.GenerateReport(result);
Debug.Log(report);
```

## Yamato Integration

For CI/CD integration with Yamato:

1. Create a headless build that runs validation and exports logs
2. Create Yamato jobs for each target platform
3. Collect hash logs from all platforms
4. Use `HashLogComparer.CompareAndReport()` to compare and generate reports

Example Yamato workflow:
```yaml
# Run on Windows
- name: Validate on Windows
  script: ./run_validation.sh
  artifacts:
    - ValidationLogs/HashLogs/*.json

# Run on macOS  
- name: Validate on macOS
  script: ./run_validation.sh
  artifacts:
    - ValidationLogs/HashLogs/*.json

# Compare results
- name: Compare Platforms
  script: ./compare_logs.sh windows.json macos.json
```

## Tips

1. **Use fixed random seeds** - Pass the same seed to all runs
2. **Start simple** - Validate with fewer ticks first, then increase
3. **Export logs** - Always export logs for debugging
4. **Check per-tick** - Use PerTick mode to find exact divergence point

