using System;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Hash scope options for validation - what entities to include.
    /// </summary>
    public enum HashScope
    {
        /// <summary>
        /// Only hash entities marked with CountEntityForWhitelistedDeterminismValidation component.
        /// This is faster and focuses on game-relevant entities.
        /// </summary>
        WhitelistedEntitiesOnly,
        
        /// <summary>
        /// Hash all entities that have the DeterministicEntityID component.
        /// This is more thorough but slower.
        /// </summary>
        AllTrackedEntities
    }
    
    /// <summary>
    /// Settings component for determinism validation.
    /// </summary>
    [Serializable]
    public struct DeterministicSettings : IComponentData
    {
        /// <summary>
        /// The validation mode (FullGame or PerSystem).
        /// </summary>
        public DeterminismValidationMode validationMode;
        
        /// <summary>
        /// How to compare hashes (FinalHashOnly, PerTick, PerSystemPerTick).
        /// </summary>
        public HashComparisonMode hashComparisonMode;
        
        /// <summary>
        /// Which entities to include in hash calculations.
        /// </summary>
        public HashScope hashScope;
        
        /// <summary>
        /// How many ticks per second should the simulation run at.
        /// </summary>
        public int simulationTickRate;
        
        /// <summary>
        /// Number of times to run the simulation for comparison.
        /// </summary>
        public int numberOfValidationRuns;
        
        /// <summary>
        /// Total number of ticks to simulate for validation.
        /// </summary>
        public int ticksToSimulate;
        
        /// <summary>
        /// Random seed for deterministic simulation.
        /// </summary>
        public uint randomSeed;
        
        /// <summary>
        /// Whether to export hash logs to file after validation.
        /// </summary>
        public bool exportHashLogs;
        
        /// <summary>
        /// Whether validation is currently in progress.
        /// </summary>
        public bool isValidationRunning;
    }
    
    /// <summary>
    /// MonoBehaviour for authoring DeterministicSettings in the Unity Editor.
    /// </summary>
    public class DeterministicSettingsAuthoring : MonoBehaviour
    {
        [Header("Validation Mode")]
        [Tooltip("FullGame: Validate entire simulation. PerSystem: Validate individual systems.")]
        public DeterminismValidationMode validationMode = DeterminismValidationMode.FullGame;
        
        [Tooltip("FinalHashOnly: Fast YES/NO. PerTick: Find first divergent tick. PerSystemPerTick: Find exact system.")]
        public HashComparisonMode hashComparisonMode = HashComparisonMode.PerTick;
        
        [Tooltip("WhitelistedEntitiesOnly: Only marked entities. AllTrackedEntities: All entities with DeterministicEntityID.")]
        public HashScope hashScope = HashScope.WhitelistedEntitiesOnly;
        
        [Header("Simulation Settings")]
        [Tooltip("How many ticks per second should the simulation run at.")]
        public int simulationTickRate = 60;
        
        [Tooltip("Random seed for deterministic simulation. Use same seed for reproducible results.")]
        public uint randomSeed = 12345;
        
        [Header("Validation Settings")]
        [Tooltip("Number of times to run the simulation for comparison.")]
        [Range(2, 10)]
        public int numberOfValidationRuns = 2;
        
        [Tooltip("Total number of ticks to simulate for validation.")]
        public int ticksToSimulate = 1000;
        
        [Header("Export Settings")]
        [Tooltip("Whether to export hash logs to file after validation for cross-platform comparison.")]
        public bool exportHashLogs = true;
        
        class SettingBaker : Baker<DeterministicSettingsAuthoring>
        {
            public override void Bake(DeterministicSettingsAuthoring authoring)
            {
                var component = new DeterministicSettings
                {
                    validationMode = authoring.validationMode,
                    hashComparisonMode = authoring.hashComparisonMode,
                    hashScope = authoring.hashScope,
                    simulationTickRate = authoring.simulationTickRate,
                    numberOfValidationRuns = authoring.numberOfValidationRuns,
                    ticksToSimulate = authoring.ticksToSimulate,
                    randomSeed = authoring.randomSeed,
                    exportHashLogs = authoring.exportHashLogs,
                    isValidationRunning = false
                };
                
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, component);
            }
        }
    }
}
