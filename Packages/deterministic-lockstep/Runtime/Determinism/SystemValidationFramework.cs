using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Framework for validating individual ECS systems for determinism.
    /// Allows running a specific system multiple times and comparing state hashes before/after.
    /// </summary>
    public class SystemValidationFramework : MonoBehaviour
    {
        /// <summary>
        /// Singleton instance for easy access.
        /// </summary>
        public static SystemValidationFramework Instance { get; private set; }
        
        /// <summary>
        /// Event fired when system validation completes.
        /// </summary>
        public event Action<SystemValidationResult> OnSystemValidationComplete;
        
        /// <summary>
        /// List of systems to validate.
        /// </summary>
        private List<SystemHandle> _systemsToValidate = new List<SystemHandle>();
        
        /// <summary>
        /// Results of system validations.
        /// </summary>
        private Dictionary<SystemHandle, SystemValidationResult> _validationResults = 
            new Dictionary<SystemHandle, SystemValidationResult>();
        
        /// <summary>
        /// Current validation state.
        /// </summary>
        private SystemValidationState _currentState = SystemValidationState.Idle;
        
        /// <summary>
        /// Current system being validated.
        /// </summary>
        private int _currentSystemIndex = 0;
        
        /// <summary>
        /// Current run index for the current system.
        /// </summary>
        private int _currentRunIndex = 0;
        
        /// <summary>
        /// Configuration for validation.
        /// </summary>
        private SystemValidationConfig _config;
        
        /// <summary>
        /// Hashes before/after system execution for each run.
        /// </summary>
        private List<(ulong beforeHash, ulong afterHash)> _runHashes = new List<(ulong, ulong)>();
        
        /// <summary>
        /// Reference to the world being validated.
        /// </summary>
        private World _validationWorld;

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
        /// Add a system to the validation queue.
        /// </summary>
        /// <typeparam name="T">The system type to validate.</typeparam>
        public void AddSystemToValidate<T>() where T : unmanaged, ISystem
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[SystemValidation] No world found.");
                return;
            }
            
            var systemHandle = world.GetExistingSystem<T>();
            if (systemHandle != SystemHandle.Null)
            {
                _systemsToValidate.Add(systemHandle);
                Debug.Log($"[SystemValidation] Added system {typeof(T).Name} to validation queue.");
            }
            else
            {
                Debug.LogWarning($"[SystemValidation] System {typeof(T).Name} not found in world.");
            }
        }
        
        /// <summary>
        /// Add a system to the validation queue by SystemHandle.
        /// </summary>
        public void AddSystemToValidate(SystemHandle systemHandle)
        {
            if (systemHandle != SystemHandle.Null)
            {
                _systemsToValidate.Add(systemHandle);
            }
        }
        
        /// <summary>
        /// Clear all systems from validation queue.
        /// </summary>
        public void ClearValidationQueue()
        {
            _systemsToValidate.Clear();
            _validationResults.Clear();
        }
        
        /// <summary>
        /// Start validating the queued systems.
        /// </summary>
        /// <param name="config">Configuration for validation.</param>
        public void StartValidation(SystemValidationConfig config)
        {
            if (_currentState != SystemValidationState.Idle)
            {
                Debug.LogWarning("[SystemValidation] Validation already in progress.");
                return;
            }
            
            if (_systemsToValidate.Count == 0)
            {
                Debug.LogWarning("[SystemValidation] No systems to validate. Add systems first.");
                return;
            }
            
            _config = config;
            _currentSystemIndex = 0;
            _currentRunIndex = 0;
            _validationResults.Clear();
            _runHashes.Clear();
            
            _validationWorld = World.DefaultGameObjectInjectionWorld;
            
            if (_validationWorld == null)
            {
                Debug.LogError("[SystemValidation] No world found for validation.");
                return;
            }
            
            // Update validation settings
            UpdateValidationSettings();
            
            _currentState = SystemValidationState.Running;
            Debug.Log($"[SystemValidation] Starting validation of {_systemsToValidate.Count} systems...");
        }
        
        /// <summary>
        /// Stop current validation.
        /// </summary>
        public void StopValidation()
        {
            _currentState = SystemValidationState.Idle;
            _runHashes.Clear();
            Debug.Log("[SystemValidation] Validation stopped.");
        }
        
        private void UpdateValidationSettings()
        {
            var entityManager = _validationWorld.EntityManager;
            
            // Update DeterministicSettings
            var settingsQuery = entityManager.CreateEntityQuery(typeof(DeterministicSettings));
            if (!settingsQuery.IsEmpty)
            {
                var entity = settingsQuery.GetSingletonEntity();
                var settings = entityManager.GetComponentData<DeterministicSettings>(entity);
                settings.validationMode = DeterminismValidationMode.SystemLevel;
                entityManager.SetComponentData(entity, settings);
            }
            
            // Update DeterminismValidationSettings
            var validationQuery = entityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
            if (!validationQuery.IsEmpty)
            {
                var entity = validationQuery.GetSingletonEntity();
                var validationSettings = entityManager.GetComponentData<DeterminismValidationSettings>(entity);
                validationSettings.validationMode = DeterminismValidationMode.SystemLevel;
                validationSettings.isValidationInProgress = true;
                validationSettings.isValidationComplete = false;
                entityManager.SetComponentData(entity, validationSettings);
            }
        }

        private void Update()
        {
            if (_currentState != SystemValidationState.Running)
                return;
                
            ValidateCurrentSystem();
        }
        
        private void ValidateCurrentSystem()
        {
            if (_currentSystemIndex >= _systemsToValidate.Count)
            {
                CompleteAllValidations();
                return;
            }
            
            var systemHandle = _systemsToValidate[_currentSystemIndex];
            
            // Compute hash before system execution
            var beforeHash = ComputeCurrentStateHash();
            
            // Execute the system
            try
            {
                systemHandle.Update(_validationWorld.Unmanaged);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SystemValidation] Error executing system: {e.Message}");
                _currentState = SystemValidationState.Idle;
                return;
            }
            
            // Compute hash after system execution
            var afterHash = ComputeCurrentStateHash();
            
            _runHashes.Add((beforeHash, afterHash));
            _currentRunIndex++;
            
            // Check if we've completed all runs for this system
            if (_currentRunIndex >= _config.numberOfRuns)
            {
                CompleteSystemValidation(systemHandle);
                _currentSystemIndex++;
                _currentRunIndex = 0;
                _runHashes.Clear();
            }
        }
        
        private ulong ComputeCurrentStateHash()
        {
            if (_validationWorld == null || !_validationWorld.IsCreated)
                return 0;
                
            var entityManager = _validationWorld.EntityManager;
            
            // Get the deterministic component buffer
            var bufferQuery = entityManager.CreateEntityQuery(typeof(DeterministicComponent));
            if (bufferQuery.IsEmpty)
                return 0;
                
            var bufferEntity = bufferQuery.GetSingletonEntity();
            var buffer = entityManager.GetBuffer<DeterministicComponent>(bufferEntity);
            
            ulong hash = 0;
            
            // Build query for entities with DeterministicEntityID
            var entityQuery = entityManager.CreateEntityQuery(typeof(DeterministicEntityID));
            var entities = entityQuery.ToEntityArray(Allocator.Temp);
            
            foreach (var entity in entities)
            {
                // Hash the entity's DeterministicEntityID
                var entityId = entityManager.GetComponentData<DeterministicEntityID>(entity);
                hash = TypeHash.CombineFNV1A64(hash, (ulong)entityId.id);
                
                // Hash each deterministic component type on this entity
                for (int i = 0; i < buffer.Length; i++)
                {
                    var componentType = buffer[i].type;
                    if (entityManager.HasComponent(entity, componentType))
                    {
                        // Get component hash using raw bytes
                        var typeIndex = componentType.TypeIndex;
                        var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                        
                        if (typeInfo.TypeSize > 0)
                        {
                            unsafe
                            {
                                var ptr = entityManager.GetComponentDataRawRO(entity, typeIndex);
                                for (int b = 0; b < typeInfo.TypeSize; b++)
                                {
                                    hash = TypeHash.CombineFNV1A64(hash, ((byte*)ptr)[b]);
                                }
                            }
                        }
                    }
                }
            }
            
            entities.Dispose();
            return hash;
        }
        
        private void CompleteSystemValidation(SystemHandle systemHandle)
        {
            var result = new SystemValidationResult
            {
                systemHandle = systemHandle,
                totalRuns = _config.numberOfRuns,
                isDeterministic = true
            };
            
            // Compare all runs
            if (_runHashes.Count > 1)
            {
                var firstRun = _runHashes[0];
                
                for (int i = 1; i < _runHashes.Count; i++)
                {
                    var currentRun = _runHashes[i];
                    
                    // Compare before hashes (state should be same before system runs)
                    if (currentRun.beforeHash != firstRun.beforeHash)
                    {
                        result.isDeterministic = false;
                        result.firstMismatchRun = i;
                        result.mismatchType = MismatchType.BeforeExecution;
                        result.expectedHash = firstRun.beforeHash;
                        result.actualHash = currentRun.beforeHash;
                        break;
                    }
                    
                    // Compare after hashes (state should be same after system runs)
                    if (currentRun.afterHash != firstRun.afterHash)
                    {
                        result.isDeterministic = false;
                        result.firstMismatchRun = i;
                        result.mismatchType = MismatchType.AfterExecution;
                        result.expectedHash = firstRun.afterHash;
                        result.actualHash = currentRun.afterHash;
                        break;
                    }
                }
            }
            
            _validationResults[systemHandle] = result;
            
            var systemType = _validationWorld.Unmanaged.GetTypeOfSystem(systemHandle);
            var systemName = systemType?.Name ?? "Unknown";
            
            if (result.isDeterministic)
            {
                Debug.Log($"[SystemValidation] ✓ System '{systemName}' is DETERMINISTIC");
            }
            else
            {
                Debug.LogError($"[SystemValidation] ✗ System '{systemName}' is NONDETERMINISTIC! " +
                             $"Mismatch at run {result.firstMismatchRun + 1}, type: {result.mismatchType}");
            }
            
            OnSystemValidationComplete?.Invoke(result);
        }
        
        private void CompleteAllValidations()
        {
            _currentState = SystemValidationState.Idle;
            
            // Update validation settings
            if (_validationWorld != null && _validationWorld.IsCreated)
            {
                var entityManager = _validationWorld.EntityManager;
                var validationQuery = entityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
                if (!validationQuery.IsEmpty)
                {
                    var entity = validationQuery.GetSingletonEntity();
                    var settings = entityManager.GetComponentData<DeterminismValidationSettings>(entity);
                    settings.isValidationComplete = true;
                    settings.isValidationInProgress = false;
                    entityManager.SetComponentData(entity, settings);
                }
            }
            
            // Generate summary
            int deterministicCount = 0;
            int nondeterministicCount = 0;
            
            foreach (var result in _validationResults.Values)
            {
                if (result.isDeterministic)
                    deterministicCount++;
                else
                    nondeterministicCount++;
            }
            
            Debug.Log($"[SystemValidation] === VALIDATION COMPLETE ===");
            Debug.Log($"[SystemValidation] Deterministic systems: {deterministicCount}");
            Debug.Log($"[SystemValidation] Nondeterministic systems: {nondeterministicCount}");
            
            if (nondeterministicCount > 0)
            {
                Debug.LogWarning("[SystemValidation] Some systems are nondeterministic. Review the logs above.");
            }
            else
            {
                Debug.Log("[SystemValidation] All validated systems are deterministic!");
            }
        }
        
        /// <summary>
        /// Get the current validation state.
        /// </summary>
        public SystemValidationState GetState() => _currentState;
        
        /// <summary>
        /// Get results for a specific system.
        /// </summary>
        public bool TryGetResult(SystemHandle systemHandle, out SystemValidationResult result)
        {
            return _validationResults.TryGetValue(systemHandle, out result);
        }
        
        /// <summary>
        /// Get all validation results.
        /// </summary>
        public IReadOnlyDictionary<SystemHandle, SystemValidationResult> GetAllResults()
        {
            return _validationResults;
        }
        
        /// <summary>
        /// Validate a single system immediately and return the result.
        /// </summary>
        public SystemValidationResult ValidateSystemImmediate<T>(int numberOfRuns = 2) where T : unmanaged, ISystem
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                return new SystemValidationResult { isDeterministic = false };
            }
            
            var systemHandle = world.GetExistingSystem<T>();
            if (systemHandle == SystemHandle.Null)
            {
                Debug.LogWarning($"[SystemValidation] System {typeof(T).Name} not found.");
                return new SystemValidationResult { isDeterministic = false };
            }
            
            _validationWorld = world;
            _runHashes.Clear();
            
            for (int run = 0; run < numberOfRuns; run++)
            {
                var beforeHash = ComputeCurrentStateHash();
                
                try
                {
                    systemHandle.Update(world.Unmanaged);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SystemValidation] Error: {e.Message}");
                    return new SystemValidationResult { isDeterministic = false };
                }
                
                var afterHash = ComputeCurrentStateHash();
                _runHashes.Add((beforeHash, afterHash));
            }
            
            // Create result
            var result = new SystemValidationResult
            {
                systemHandle = systemHandle,
                totalRuns = numberOfRuns,
                isDeterministic = true
            };
            
            if (_runHashes.Count > 1)
            {
                var firstRun = _runHashes[0];
                for (int i = 1; i < _runHashes.Count; i++)
                {
                    var currentRun = _runHashes[i];
                    if (currentRun.afterHash != firstRun.afterHash)
                    {
                        result.isDeterministic = false;
                        result.firstMismatchRun = i;
                        result.mismatchType = MismatchType.AfterExecution;
                        result.expectedHash = firstRun.afterHash;
                        result.actualHash = currentRun.afterHash;
                        break;
                    }
                }
            }
            
            _runHashes.Clear();
            return result;
        }
    }
    
    /// <summary>
    /// Configuration for system validation.
    /// </summary>
    [Serializable]
    public struct SystemValidationConfig
    {
        /// <summary>
        /// Number of times to run each system for comparison.
        /// </summary>
        public int numberOfRuns;
        
        /// <summary>
        /// Whether to reset state between runs.
        /// </summary>
        public bool resetStateBetweenRuns;
        
        /// <summary>
        /// Default configuration.
        /// </summary>
        public static SystemValidationConfig Default => new SystemValidationConfig
        {
            numberOfRuns = 2,
            resetStateBetweenRuns = false
        };
    }
    
    /// <summary>
    /// Result of validating a single system.
    /// </summary>
    [Serializable]
    public struct SystemValidationResult
    {
        /// <summary>
        /// The system that was validated.
        /// </summary>
        public SystemHandle systemHandle;
        
        /// <summary>
        /// Whether the system is deterministic.
        /// </summary>
        public bool isDeterministic;
        
        /// <summary>
        /// Total number of runs performed.
        /// </summary>
        public int totalRuns;
        
        /// <summary>
        /// The run where mismatch was first detected (0 = first run, so mismatch is at run index 1+).
        /// </summary>
        public int firstMismatchRun;
        
        /// <summary>
        /// Type of mismatch detected.
        /// </summary>
        public MismatchType mismatchType;
        
        /// <summary>
        /// Expected hash (from first run).
        /// </summary>
        public ulong expectedHash;
        
        /// <summary>
        /// Actual hash that differed.
        /// </summary>
        public ulong actualHash;
    }
    
    /// <summary>
    /// Type of hash mismatch.
    /// </summary>
    public enum MismatchType
    {
        /// <summary>
        /// No mismatch.
        /// </summary>
        None,
        
        /// <summary>
        /// State differed before system execution.
        /// </summary>
        BeforeExecution,
        
        /// <summary>
        /// State differed after system execution.
        /// </summary>
        AfterExecution
    }
    
    /// <summary>
    /// State of system validation.
    /// </summary>
    public enum SystemValidationState
    {
        /// <summary>
        /// No validation in progress.
        /// </summary>
        Idle,
        
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

