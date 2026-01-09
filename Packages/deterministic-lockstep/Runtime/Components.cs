using System;
using Unity.Collections;
using Unity.Entities;

namespace DeterministicLockstep
{
    /// <summary>
    /// Validation mode for determinism checking.
    /// </summary>
    public enum DeterminismValidationMode
    {
        /// <summary>
        /// Full game validation - run entire simulation multiple times and compare hashes.
        /// Can compare final hash only (fast) or per-tick hashes (debugging).
        /// </summary>
        FullGame,
        
        /// <summary>
        /// System-level validation - validate individual ECS systems in isolation.
        /// Useful for finding which system causes nondeterminism.
        /// </summary>
        PerSystem
    }
    
    /// <summary>
    /// Hash comparison mode for validation.
    /// </summary>
    public enum HashComparisonMode
    {
        /// <summary>
        /// Only compare the final hash after all ticks complete (fast YES/NO answer).
        /// </summary>
        FinalHashOnly,
        
        /// <summary>
        /// Compare hash after each tick (slower but helps find first divergent tick).
        /// </summary>
        PerTick,
        
        /// <summary>
        /// Compare hash after each system within each tick (most detailed, finds exact system causing nondeterminism).
        /// </summary>
        PerSystemPerTick
    }
    
    /// <summary>
    /// State of validation.
    /// </summary>
    public enum ValidationState
    {
        /// <summary>
        /// No validation running.
        /// </summary>
        Idle,
        
        /// <summary>
        /// Validation is currently running.
        /// </summary>
        Running,
        
        /// <summary>
        /// Validation completed successfully (deterministic).
        /// </summary>
        Passed,
        
        /// <summary>
        /// Validation completed with nondeterminism detected.
        /// </summary>
        Failed
    }
    
    /// <summary>
    /// Component used to store validation settings and state.
    /// </summary>
    public struct DeterminismValidationSettings : IComponentData
    {
        /// <summary>
        /// The validation mode to use.
        /// </summary>
        public DeterminismValidationMode validationMode;
        
        /// <summary>
        /// How to compare hashes during validation.
        /// </summary>
        public HashComparisonMode hashComparisonMode;
        
        /// <summary>
        /// Number of times to run the simulation for comparison.
        /// </summary>
        public int numberOfRuns;
        
        /// <summary>
        /// Current run index when performing multiple run validation.
        /// </summary>
        public int currentRunIndex;
        
        /// <summary>
        /// Current validation state.
        /// </summary>
        public ValidationState validationState;
        
        /// <summary>
        /// Whether nondeterminism was detected during validation.
        /// </summary>
        public bool nondeterminismDetected;
        
        /// <summary>
        /// The tick at which nondeterminism was first detected (-1 if none detected).
        /// </summary>
        public int firstNondeterministicTick;
        
        /// <summary>
        /// The system index at which nondeterminism was first detected (-1 if not per-system mode or none detected).
        /// </summary>
        public int firstNondeterministicSystemIndex;
        
        /// <summary>
        /// Total number of ticks to simulate for validation.
        /// </summary>
        public int ticksToSimulate;
        
        /// <summary>
        /// Random seed for deterministic simulation.
        /// </summary>
        public uint randomSeed;
    }
    
    /// <summary>
    /// Component used to store simulation time related variables.
    /// </summary>
    public struct DeterministicSimulationTime : IComponentData
    {
        /// <summary>
        /// Number of times the simulation has ticked this frame.
        /// </summary>
        public int numTimesTickedThisFrame;

        /// <summary>
        /// Target tick rate of the simulation (ticks per second).
        /// </summary>
        public int tickRate;

        /// <summary>
        /// Time remaining until next tick should be processed.
        /// </summary>
        public double timeUntilNextTick;

        /// <summary>
        /// Current simulation tick number.
        /// </summary>
        public int currentTick;

        /// <summary>
        /// Hashes computed during the current tick (one per system if per-system mode, otherwise one total).
        /// </summary>
        public NativeList<ulong> hashesForCurrentTick;
    }

    /// <summary>
    /// Buffer element for tracking which component types should be included in hash calculations.
    /// </summary>
    public struct DeterministicComponent : IBufferElementData
    {
        public ComponentType type;
    }
    
    /// <summary>
    /// Component to ensure deterministic sorting of entities when computing hashes.
    /// Add this to entities that should be included in determinism validation.
    /// The ID should be assigned incrementally when entities are created to ensure deterministic ordering.
    /// </summary>
    public struct DeterministicEntityID : IComponentData, IComparable<DeterministicEntityID>
    {
        /// <summary>
        /// Unique deterministic identifier for this entity.
        /// </summary>
        public int id;

        public int CompareTo(DeterministicEntityID other)
        {
            return id.CompareTo(other.id);
        }
    }
    
    /// <summary>
    /// Tag component to mark entities that should be included in whitelist-based validation.
    /// Only entities with this component will be hashed when using whitelist validation mode.
    /// </summary>
    public struct CountEntityForWhitelistedDeterminismValidation : IComponentData
    {
    }
}
