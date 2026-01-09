using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Manager for single-player determinism validation.
    /// Runs the simulation multiple times with the same seed/inputs and compares hashes.
    /// </summary>
    public class SinglePlayerValidationManager : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance for easy access.
        /// </summary>
        public static SinglePlayerValidationManager Instance { get; private set; }
        
        /// <summary>
        /// Event fired when validation completes.
        /// </summary>
        public event Action<ValidationResult> OnValidationComplete;
        
        /// <summary>
        /// Event fired when a run completes.
        /// </summary>
        public event Action<int, int> OnRunComplete; // currentRun, totalRuns
        
        /// <summary>
        /// Event fired when nondeterminism is detected.
        /// </summary>
        public event Action<int, int, ulong, ulong> OnNondeterminismDetected; // tick, runIndex, expectedHash, actualHash
        
        /// <summary>
        /// Stores hashes from each run, keyed by run index then tick.
        /// </summary>
        private Dictionary<int, Dictionary<int, ulong>> _hashesPerRun = new Dictionary<int, Dictionary<int, ulong>>();
        
        /// <summary>
        /// Current validation state.
        /// </summary>
        private ValidationState _currentState = ValidationState.Idle;
        
        /// <summary>
        /// Configuration for current validation.
        /// </summary>
        private ValidationConfig _config;
        
        /// <summary>
        /// Current run index.
        /// </summary>
        private int _currentRunIndex = 0;
        
        /// <summary>
        /// Reference to the world being validated.
        /// </summary>
        private World _validationWorld;
        
        /// <summary>
        /// Result of the validation.
        /// </summary>
        private ValidationResult _result;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Start a new validation session.
        /// </summary>
        /// <param name="config">Configuration for the validation.</param>
        public void StartValidation(ValidationConfig config)
        {
            if (_currentState != ValidationState.Idle)
            {
                Debug.LogWarning("Validation is already in progress. Call StopValidation() first.");
                return;
            }
            
            _config = config;
            _currentRunIndex = 0;
            _hashesPerRun.Clear();
            _result = new ValidationResult();
            _currentState = ValidationState.PreparingRun;
            
            Debug.Log($"[SinglePlayerValidation] Starting validation with {_config.numberOfRuns} runs, {_config.ticksToSimulate} ticks each.");
            
            StartNextRun();
        }
        
        /// <summary>
        /// Stop the current validation.
        /// </summary>
        public void StopValidation()
        {
            _currentState = ValidationState.Idle;
            _hashesPerRun.Clear();
            Debug.Log("[SinglePlayerValidation] Validation stopped.");
        }
        
        /// <summary>
        /// Called each tick to record the hash for the current run.
        /// Should be called from StateHashForValidationSystem after hash is computed.
        /// </summary>
        /// <param name="tick">The current tick.</param>
        /// <param name="hash">The computed hash for this tick.</param>
        public void RecordHash(int tick, ulong hash)
        {
            if (_currentState != ValidationState.Running)
                return;
                
            if (!_hashesPerRun.ContainsKey(_currentRunIndex))
            {
                _hashesPerRun[_currentRunIndex] = new Dictionary<int, ulong>();
            }
            
            _hashesPerRun[_currentRunIndex][tick] = hash;
            
            // If this is not the first run, compare with previous runs
            if (_currentRunIndex > 0)
            {
                CompareHashWithPreviousRuns(tick, hash);
            }
        }
        
        /// <summary>
        /// Called when the current run completes (reached target tick count).
        /// </summary>
        public void OnCurrentRunComplete()
        {
            if (_currentState != ValidationState.Running)
                return;
                
            Debug.Log($"[SinglePlayerValidation] Run {_currentRunIndex + 1}/{_config.numberOfRuns} complete.");
            OnRunComplete?.Invoke(_currentRunIndex + 1, _config.numberOfRuns);
            
            _currentRunIndex++;
            
            if (_currentRunIndex >= _config.numberOfRuns)
            {
                CompleteValidation();
            }
            else
            {
                _currentState = ValidationState.PreparingRun;
                StartNextRun();
            }
        }
        
        private void StartNextRun()
        {
            Debug.Log($"[SinglePlayerValidation] Preparing run {_currentRunIndex + 1}/{_config.numberOfRuns}...");
            
            // Get or create the validation world
            _validationWorld = World.DefaultGameObjectInjectionWorld;
            
            if (_validationWorld == null)
            {
                Debug.LogError("[SinglePlayerValidation] No world found for validation.");
                _currentState = ValidationState.Idle;
                return;
            }
            
            // Update validation settings in the world
            var entityManager = _validationWorld.EntityManager;
            
            // Find and update DeterminismValidationSettings
            var query = entityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
            if (query.IsEmpty)
            {
                // Create the singleton if it doesn't exist
                entityManager.CreateSingleton(new DeterminismValidationSettings
                {
                    validationMode = DeterminismValidationMode.SinglePlayer,
                    numberOfRuns = _config.numberOfRuns,
                    currentRunIndex = _currentRunIndex,
                    isValidationInProgress = true,
                    isValidationComplete = false,
                    nondeterminismDetected = false,
                    firstNondeterministicTick = 0,
                    ticksToSimulate = _config.ticksToSimulate
                });
            }
            else
            {
                var entity = query.GetSingletonEntity();
                entityManager.SetComponentData(entity, new DeterminismValidationSettings
                {
                    validationMode = DeterminismValidationMode.SinglePlayer,
                    numberOfRuns = _config.numberOfRuns,
                    currentRunIndex = _currentRunIndex,
                    isValidationInProgress = true,
                    isValidationComplete = false,
                    nondeterminismDetected = _result.nondeterminismDetected,
                    firstNondeterministicTick = _result.firstNondeterministicTick,
                    ticksToSimulate = _config.ticksToSimulate
                });
            }
            
            // Update DeterministicSettings
            var settingsQuery = entityManager.CreateEntityQuery(typeof(DeterministicSettings));
            if (!settingsQuery.IsEmpty)
            {
                var settingsEntity = settingsQuery.GetSingletonEntity();
                var settings = entityManager.GetComponentData<DeterministicSettings>(settingsEntity);
                settings.validationMode = DeterminismValidationMode.SinglePlayer;
                settings.randomSeed = _config.randomSeed;
                settings.numberOfValidationRuns = _config.numberOfRuns;
                settings.ticksToSimulate = _config.ticksToSimulate;
                entityManager.SetComponentData(settingsEntity, settings);
            }
            
            // Reset simulation time
            var timeQuery = entityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
            if (!timeQuery.IsEmpty)
            {
                var timeEntity = timeQuery.GetSingletonEntity();
                var simTime = entityManager.GetComponentData<DeterministicSimulationTime>(timeEntity);
                simTime.currentSimulationTick = 0;
                simTime.currentClientTickToSend = 0;
                simTime.numTimesTickedThisFrame = 0;
                simTime.timeLeftToSendNextTick = 0;
                entityManager.SetComponentData(timeEntity, simTime);
            }
            
            _currentState = ValidationState.Running;
            Debug.Log($"[SinglePlayerValidation] Run {_currentRunIndex + 1} started.");
        }
        
        private void CompareHashWithPreviousRuns(int tick, ulong hash)
        {
            for (int i = 0; i < _currentRunIndex; i++)
            {
                if (_hashesPerRun.ContainsKey(i) && _hashesPerRun[i].ContainsKey(tick))
                {
                    var previousHash = _hashesPerRun[i][tick];
                    if (previousHash != hash)
                    {
                        if (!_result.nondeterminismDetected)
                        {
                            _result.nondeterminismDetected = true;
                            _result.firstNondeterministicTick = tick;
                            _result.firstMismatchRunIndex = _currentRunIndex;
                            _result.expectedHash = previousHash;
                            _result.actualHash = hash;
                            
                            Debug.LogError($"[SinglePlayerValidation] NONDETERMINISM DETECTED at tick {tick}! " +
                                         $"Run {i + 1} hash: {previousHash}, Run {_currentRunIndex + 1} hash: {hash}");
                            
                            OnNondeterminismDetected?.Invoke(tick, _currentRunIndex, previousHash, hash);
                        }
                    }
                }
            }
        }
        
        private void CompleteValidation()
        {
            _currentState = ValidationState.Complete;
            
            _result.totalRuns = _config.numberOfRuns;
            _result.ticksSimulated = _config.ticksToSimulate;
            _result.isComplete = true;
            
            // Update validation settings to mark as complete
            if (_validationWorld != null && _validationWorld.IsCreated)
            {
                var entityManager = _validationWorld.EntityManager;
                var query = entityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
                if (!query.IsEmpty)
                {
                    var entity = query.GetSingletonEntity();
                    var settings = entityManager.GetComponentData<DeterminismValidationSettings>(entity);
                    settings.isValidationComplete = true;
                    settings.isValidationInProgress = false;
                    settings.nondeterminismDetected = _result.nondeterminismDetected;
                    settings.firstNondeterministicTick = _result.firstNondeterministicTick;
                    entityManager.SetComponentData(entity, settings);
                }
            }
            
            if (_result.nondeterminismDetected)
            {
                Debug.LogError($"[SinglePlayerValidation] VALIDATION FAILED - Nondeterminism detected at tick {_result.firstNondeterministicTick}");
            }
            else
            {
                Debug.Log($"[SinglePlayerValidation] VALIDATION PASSED - All {_config.numberOfRuns} runs produced identical hashes for {_config.ticksToSimulate} ticks.");
            }
            
            OnValidationComplete?.Invoke(_result);
            
            _currentState = ValidationState.Idle;
        }

        private void Update()
        {
            if (_currentState != ValidationState.Running)
                return;
                
            // Check if current run has completed
            if (_validationWorld == null || !_validationWorld.IsCreated)
                return;
                
            var entityManager = _validationWorld.EntityManager;
            var timeQuery = entityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
            
            if (timeQuery.IsEmpty)
                return;
                
            var simTime = timeQuery.GetSingleton<DeterministicSimulationTime>();
            
            if (simTime.currentSimulationTick >= _config.ticksToSimulate)
            {
                OnCurrentRunComplete();
            }
        }
        
        /// <summary>
        /// Get the current validation state.
        /// </summary>
        public ValidationState GetState() => _currentState;
        
        /// <summary>
        /// Get the current run index (0-based).
        /// </summary>
        public int GetCurrentRunIndex() => _currentRunIndex;
        
        /// <summary>
        /// Get hashes for a specific run.
        /// </summary>
        public Dictionary<int, ulong> GetHashesForRun(int runIndex)
        {
            return _hashesPerRun.ContainsKey(runIndex) ? _hashesPerRun[runIndex] : null;
        }
    }
    
    /// <summary>
    /// Configuration for validation.
    /// </summary>
    [Serializable]
    public struct ValidationConfig
    {
        /// <summary>
        /// Number of times to run the simulation.
        /// </summary>
        public int numberOfRuns;
        
        /// <summary>
        /// Number of ticks to simulate per run.
        /// </summary>
        public int ticksToSimulate;
        
        /// <summary>
        /// Random seed to use for all runs.
        /// </summary>
        public uint randomSeed;
        
        /// <summary>
        /// Create a default validation config.
        /// </summary>
        public static ValidationConfig Default => new ValidationConfig
        {
            numberOfRuns = 2,
            ticksToSimulate = 1000,
            randomSeed = 12345
        };
    }
    
    /// <summary>
    /// Result of a validation run.
    /// </summary>
    [Serializable]
    public struct ValidationResult
    {
        /// <summary>
        /// Whether the validation has completed.
        /// </summary>
        public bool isComplete;
        
        /// <summary>
        /// Whether nondeterminism was detected.
        /// </summary>
        public bool nondeterminismDetected;
        
        /// <summary>
        /// The first tick where nondeterminism was detected.
        /// </summary>
        public int firstNondeterministicTick;
        
        /// <summary>
        /// The run index where mismatch was first detected.
        /// </summary>
        public int firstMismatchRunIndex;
        
        /// <summary>
        /// The expected hash (from first run).
        /// </summary>
        public ulong expectedHash;
        
        /// <summary>
        /// The actual hash that differed.
        /// </summary>
        public ulong actualHash;
        
        /// <summary>
        /// Total number of runs performed.
        /// </summary>
        public int totalRuns;
        
        /// <summary>
        /// Number of ticks simulated per run.
        /// </summary>
        public int ticksSimulated;
    }
    
    /// <summary>
    /// Validation state enum.
    /// </summary>
    public enum ValidationState
    {
        /// <summary>
        /// No validation in progress.
        /// </summary>
        Idle,
        
        /// <summary>
        /// Preparing for next run.
        /// </summary>
        PreparingRun,
        
        /// <summary>
        /// Validation is running.
        /// </summary>
        Running,
        
        /// <summary>
        /// Validation has completed.
        /// </summary>
        Complete
    }
}

