using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Validates entire game simulations for determinism by running N times and comparing hashes.
    /// </summary>
    public class GameValidator : MonoBehaviour
    {
        public static GameValidator Instance { get; private set; }
        
        /// <summary>
        /// Event fired when validation completes.
        /// </summary>
        public event Action<GameValidationResult> OnValidationComplete;
        
        /// <summary>
        /// Event fired when a run completes.
        /// </summary>
        public event Action<int, int> OnRunComplete; // currentRun, totalRuns
        
        /// <summary>
        /// Event fired when nondeterminism is detected.
        /// </summary>
        public event Action<int, int, ulong, ulong> OnNondeterminismDetected; // tick, runIndex, expectedHash, actualHash
        
        /// <summary>
        /// Hashes collected per run, per tick.
        /// </summary>
        private Dictionary<int, Dictionary<int, ulong>> _hashesPerRun = new Dictionary<int, Dictionary<int, ulong>>();
        
        private ValidationConfig _config;
        private int _currentRunIndex = 0;
        private bool _isValidating = false;
        private GameValidationResult _result;
        private World _world;

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
        /// Start validation with the given configuration.
        /// </summary>
        public void StartValidation(ValidationConfig config, World world = null)
        {
            if (_isValidating)
            {
                Debug.LogWarning("[GameValidator] Validation already in progress.");
                return;
            }
            
            _config = config;
            _world = world ?? World.DefaultGameObjectInjectionWorld;
            _currentRunIndex = 0;
            _hashesPerRun.Clear();
            _result = new GameValidationResult
            {
                numberOfRuns = config.numberOfRuns,
                ticksSimulated = config.ticksToSimulate,
                hashesPerRun = new Dictionary<int, Dictionary<int, ulong>>()
            };
            _isValidating = true;
            
            // Initialize logger
            if (DeterministicLogger.Instance != null)
            {
                DeterministicLogger.Instance.StartNewRun(0);
            }
            
            // Update ECS settings
            UpdateValidationSettings(ValidationState.Running);
            
            Debug.Log($"[GameValidator] Starting validation: {config.numberOfRuns} runs, {config.ticksToSimulate} ticks each.");
        }

        /// <summary>
        /// Stop current validation.
        /// </summary>
        public void StopValidation()
        {
            _isValidating = false;
            UpdateValidationSettings(ValidationState.Idle);
            Debug.Log("[GameValidator] Validation stopped.");
        }

        /// <summary>
        /// Record hash for current tick (called by hash system).
        /// </summary>
        public void RecordTickHash(int tick, ulong hash)
        {
            if (!_isValidating)
                return;
                
            if (!_hashesPerRun.ContainsKey(_currentRunIndex))
            {
                _hashesPerRun[_currentRunIndex] = new Dictionary<int, ulong>();
            }
            
            _hashesPerRun[_currentRunIndex][tick] = hash;
            
            // Compare with first run if not first run
            if (_currentRunIndex > 0 && _hashesPerRun.ContainsKey(0))
            {
                if (_hashesPerRun[0].TryGetValue(tick, out var expectedHash))
                {
                    if (hash != expectedHash && !_result.nondeterminismDetected)
                    {
                        _result.nondeterminismDetected = true;
                        _result.firstNondeterministicTick = tick;
                        _result.firstMismatchRunIndex = _currentRunIndex;
                        _result.expectedHash = expectedHash;
                        _result.actualHash = hash;
                        
                        Debug.LogError($"[GameValidator] NONDETERMINISM at tick {tick}! " +
                                     $"Run 1: {expectedHash:X16}, Run {_currentRunIndex + 1}: {hash:X16}");
                        
                        OnNondeterminismDetected?.Invoke(tick, _currentRunIndex, expectedHash, hash);
                    }
                }
            }
        }

        /// <summary>
        /// Called when current run's ticks are complete.
        /// </summary>
        public void OnRunTicksComplete()
        {
            if (!_isValidating)
                return;
                
            Debug.Log($"[GameValidator] Run {_currentRunIndex + 1}/{_config.numberOfRuns} complete.");
            
            // Copy hashes to result
            if (_hashesPerRun.ContainsKey(_currentRunIndex))
            {
                _result.hashesPerRun[_currentRunIndex] = new Dictionary<int, ulong>(_hashesPerRun[_currentRunIndex]);
            }
            
            OnRunComplete?.Invoke(_currentRunIndex + 1, _config.numberOfRuns);
            
            _currentRunIndex++;
            
            if (_currentRunIndex >= _config.numberOfRuns)
            {
                CompleteValidation();
            }
            else
            {
                // Start next run
                if (DeterministicLogger.Instance != null)
                {
                    DeterministicLogger.Instance.StartNewRun(_currentRunIndex);
                }
                
                // Reset simulation time for next run
                ResetSimulationTime();
            }
        }

        private void ResetSimulationTime()
        {
            if (_world == null || !_world.IsCreated)
                return;
                
            var entityManager = _world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
            
            if (!query.IsEmpty)
            {
                var entity = query.GetSingletonEntity();
                var simTime = entityManager.GetComponentData<DeterministicSimulationTime>(entity);
                simTime.currentTick = 0;
                simTime.numTimesTickedThisFrame = 0;
                simTime.timeUntilNextTick = 0;
                
                // Clear hashes
                if (simTime.hashesForCurrentTick.IsCreated)
                {
                    simTime.hashesForCurrentTick.Clear();
                }
                
                entityManager.SetComponentData(entity, simTime);
            }
        }

        private void CompleteValidation()
        {
            _isValidating = false;
            _result.isComplete = true;
            
            UpdateValidationSettings(_result.nondeterminismDetected ? ValidationState.Failed : ValidationState.Passed);
            
            // Export logs if configured
            if (_config.exportLogs && DeterministicLogger.Instance != null)
            {
                DeterministicLogger.Instance.ExportLogs();
                if (_config.numberOfRuns >= 2)
                {
                    DeterministicLogger.Instance.ExportComparisonReport(0, 1);
                }
            }
            
            if (_result.nondeterminismDetected)
            {
                Debug.LogError($"[GameValidator] VALIDATION FAILED - Nondeterminism at tick {_result.firstNondeterministicTick}");
            }
            else
            {
                Debug.Log($"[GameValidator] VALIDATION PASSED - All {_config.numberOfRuns} runs match for {_config.ticksToSimulate} ticks!");
            }
            
            OnValidationComplete?.Invoke(_result);
        }

        private void UpdateValidationSettings(ValidationState state)
        {
            if (_world == null || !_world.IsCreated)
                return;
                
            var entityManager = _world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
            
            if (!query.IsEmpty)
            {
                var entity = query.GetSingletonEntity();
                var settings = entityManager.GetComponentData<DeterminismValidationSettings>(entity);
                settings.validationState = state;
                settings.numberOfRuns = _config.numberOfRuns;
                settings.currentRunIndex = _currentRunIndex;
                settings.ticksToSimulate = _config.ticksToSimulate;
                settings.randomSeed = _config.randomSeed;
                settings.nondeterminismDetected = _result.nondeterminismDetected;
                settings.firstNondeterministicTick = _result.firstNondeterministicTick;
                entityManager.SetComponentData(entity, settings);
            }
        }

        private void Update()
        {
            if (!_isValidating || _world == null || !_world.IsCreated)
                return;
                
            // Check if current run is complete
            var entityManager = _world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
            
            if (query.IsEmpty)
                return;
                
            var simTime = query.GetSingleton<DeterministicSimulationTime>();
            
            if (simTime.currentTick >= _config.ticksToSimulate)
            {
                OnRunTicksComplete();
            }
        }

        /// <summary>
        /// Get current validation state.
        /// </summary>
        public bool IsValidating => _isValidating;
        
        /// <summary>
        /// Get current run index.
        /// </summary>
        public int CurrentRunIndex => _currentRunIndex;
    }
    
    /// <summary>
    /// Configuration for game validation.
    /// </summary>
    [Serializable]
    public struct ValidationConfig
    {
        public int numberOfRuns;
        public int ticksToSimulate;
        public uint randomSeed;
        public bool exportLogs;
        
        public static ValidationConfig Default => new ValidationConfig
        {
            numberOfRuns = 2,
            ticksToSimulate = 1000,
            randomSeed = 12345,
            exportLogs = true
        };
    }
    
    /// <summary>
    /// Result of game validation.
    /// </summary>
    [Serializable]
    public struct GameValidationResult
    {
        public bool isComplete;
        public bool nondeterminismDetected;
        public int firstNondeterministicTick;
        public int firstMismatchRunIndex;
        public ulong expectedHash;
        public ulong actualHash;
        public int numberOfRuns;
        public int ticksSimulated;
        public Dictionary<int, Dictionary<int, ulong>> hashesPerRun;
    }
}

