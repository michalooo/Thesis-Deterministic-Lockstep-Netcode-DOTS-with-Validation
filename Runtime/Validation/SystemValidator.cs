using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Validates individual ECS systems for determinism.
    /// Supports snapshot-based and setup-function approaches.
    /// </summary>
    public class SystemValidator
    {
        /// <summary>
        /// Result of system validation.
        /// </summary>
        public struct ValidationResult
        {
            public bool isDeterministic;
            public int numberOfRuns;
            public int firstMismatchRun;
            public ulong expectedHash;
            public ulong actualHash;
            public string systemName;
            public List<ulong> hashesPerRun;
        }
        
        /// <summary>
        /// Delegate for setup functions.
        /// </summary>
        public delegate void SetupDelegate(EntityManager entityManager);
        
        /// <summary>
        /// Delegate for cleanup functions.
        /// </summary>
        public delegate void CleanupDelegate(EntityManager entityManager);

        /// <summary>
        /// Validate a system using snapshot approach.
        /// Captures current state, runs system N times, compares output hashes.
        /// </summary>
        /// <typeparam name="T">System type to validate.</typeparam>
        /// <param name="world">World containing the system.</param>
        /// <param name="numberOfRuns">Number of times to run the system.</param>
        /// <returns>Validation result.</returns>
        public static ValidationResult ValidateWithSnapshot<T>(World world, int numberOfRuns = 2) where T : unmanaged, ISystem
        {
            var result = new ValidationResult
            {
                systemName = typeof(T).Name,
                numberOfRuns = numberOfRuns,
                hashesPerRun = new List<ulong>(),
                isDeterministic = true,
                firstMismatchRun = -1
            };
            
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[SystemValidator] World is null or destroyed.");
                result.isDeterministic = false;
                return result;
            }
            
            var systemHandle = world.GetExistingSystem<T>();
            if (systemHandle == SystemHandle.Null)
            {
                Debug.LogError($"[SystemValidator] System {typeof(T).Name} not found.");
                result.isDeterministic = false;
                return result;
            }
            
            // Capture initial state
            using (var snapshot = new WorldStateSnapshot())
            {
                snapshot.Capture(world);
                
                for (int run = 0; run < numberOfRuns; run++)
                {
                    // Restore to initial state
                    if (run > 0)
                    {
                        snapshot.Restore(world);
                    }
                    
                    // Run the system
                    try
                    {
                        systemHandle.Update(world.Unmanaged);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[SystemValidator] Error running system: {e.Message}");
                        result.isDeterministic = false;
                        return result;
                    }
                    
                    // Compute hash after system execution
                    using (var afterSnapshot = new WorldStateSnapshot())
                    {
                        afterSnapshot.Capture(world);
                        var hash = afterSnapshot.ComputeHash();
                        result.hashesPerRun.Add(hash);
                        
                        // Compare with first run
                        if (run > 0 && hash != result.hashesPerRun[0])
                        {
                            result.isDeterministic = false;
                            if (result.firstMismatchRun == -1)
                            {
                                result.firstMismatchRun = run;
                                result.expectedHash = result.hashesPerRun[0];
                                result.actualHash = hash;
                            }
                        }
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Validate a system using setup function approach.
        /// Calls setup before each run to create identical initial state.
        /// </summary>
        /// <typeparam name="T">System type to validate.</typeparam>
        /// <param name="world">World containing the system.</param>
        /// <param name="setup">Setup function called before each run.</param>
        /// <param name="cleanup">Optional cleanup function called after each run.</param>
        /// <param name="numberOfRuns">Number of times to run the system.</param>
        /// <returns>Validation result.</returns>
        public static ValidationResult ValidateWithSetup<T>(
            World world, 
            SetupDelegate setup, 
            CleanupDelegate cleanup = null,
            int numberOfRuns = 2) where T : unmanaged, ISystem
        {
            var result = new ValidationResult
            {
                systemName = typeof(T).Name,
                numberOfRuns = numberOfRuns,
                hashesPerRun = new List<ulong>(),
                isDeterministic = true,
                firstMismatchRun = -1
            };
            
            if (world == null || !world.IsCreated)
            {
                Debug.LogError("[SystemValidator] World is null or destroyed.");
                result.isDeterministic = false;
                return result;
            }
            
            var systemHandle = world.GetExistingSystem<T>();
            if (systemHandle == SystemHandle.Null)
            {
                Debug.LogError($"[SystemValidator] System {typeof(T).Name} not found.");
                result.isDeterministic = false;
                return result;
            }
            
            var entityManager = world.EntityManager;
            
            for (int run = 0; run < numberOfRuns; run++)
            {
                // Call setup
                try
                {
                    setup(entityManager);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SystemValidator] Error in setup: {e.Message}");
                    result.isDeterministic = false;
                    return result;
                }
                
                // Run the system
                try
                {
                    systemHandle.Update(world.Unmanaged);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SystemValidator] Error running system: {e.Message}");
                    result.isDeterministic = false;
                    cleanup?.Invoke(entityManager);
                    return result;
                }
                
                // Compute hash after system execution
                using (var afterSnapshot = new WorldStateSnapshot())
                {
                    afterSnapshot.Capture(world);
                    var hash = afterSnapshot.ComputeHash();
                    result.hashesPerRun.Add(hash);
                    
                    // Compare with first run
                    if (run > 0 && hash != result.hashesPerRun[0])
                    {
                        result.isDeterministic = false;
                        if (result.firstMismatchRun == -1)
                        {
                            result.firstMismatchRun = run;
                            result.expectedHash = result.hashesPerRun[0];
                            result.actualHash = hash;
                        }
                    }
                }
                
                // Call cleanup
                cleanup?.Invoke(entityManager);
            }
            
            return result;
        }

        /// <summary>
        /// Validate multiple systems in sequence.
        /// </summary>
        public static List<ValidationResult> ValidateSystemGroup(
            World world,
            ComponentSystemGroup systemGroup,
            int numberOfRuns = 2)
        {
            var results = new List<ValidationResult>();
            
            if (world == null || !world.IsCreated || systemGroup == null)
            {
                Debug.LogError("[SystemValidator] Invalid world or system group.");
                return results;
            }
            
            var systems = systemGroup.GetAllSystems();
            
            using (var initialSnapshot = new WorldStateSnapshot())
            {
                initialSnapshot.Capture(world);
                
                foreach (var systemHandle in systems)
                {
                    // Get system name from SystemState
                    string systemName;
                    try
                    {
                        ref readonly var systemState = ref world.Unmanaged.ResolveSystemStateRef(systemHandle);
                        systemName = systemState.DebugName.ToString();
                        if (string.IsNullOrEmpty(systemName))
                            systemName = $"System_{systemHandle.GetHashCode()}";
                    }
                    catch
                    {
                        continue;
                    }
                    
                    var result = new ValidationResult
                    {
                        systemName = systemName,
                        numberOfRuns = numberOfRuns,
                        hashesPerRun = new List<ulong>(),
                        isDeterministic = true,
                        firstMismatchRun = -1
                    };
                    
                    for (int run = 0; run < numberOfRuns; run++)
                    {
                        // Restore to state before this system
                        if (run > 0)
                        {
                            initialSnapshot.Restore(world);
                        }
                        
                        // Run the system
                        try
                        {
                            systemHandle.Update(world.Unmanaged);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[SystemValidator] Error running {systemName}: {e.Message}");
                            result.isDeterministic = false;
                            break;
                        }
                        
                        // Compute hash
                        using (var afterSnapshot = new WorldStateSnapshot())
                        {
                            afterSnapshot.Capture(world);
                            var hash = afterSnapshot.ComputeHash();
                            result.hashesPerRun.Add(hash);
                            
                            if (run > 0 && hash != result.hashesPerRun[0])
                            {
                                result.isDeterministic = false;
                                if (result.firstMismatchRun == -1)
                                {
                                    result.firstMismatchRun = run;
                                    result.expectedHash = result.hashesPerRun[0];
                                    result.actualHash = hash;
                                }
                            }
                        }
                    }
                    
                    results.Add(result);
                    
                    // Update initial snapshot to include this system's changes
                    initialSnapshot.Capture(world);
                }
            }
            
            return results;
        }

        /// <summary>
        /// Print validation result to console.
        /// </summary>
        public static void LogResult(ValidationResult result)
        {
            if (result.isDeterministic)
            {
                Debug.Log($"[SystemValidator] ✓ {result.systemName} is DETERMINISTIC ({result.numberOfRuns} runs)");
            }
            else
            {
                Debug.LogError($"[SystemValidator] ✗ {result.systemName} is NONDETERMINISTIC\n" +
                             $"  First mismatch at run {result.firstMismatchRun + 1}\n" +
                             $"  Expected: {result.expectedHash:X16}\n" +
                             $"  Actual: {result.actualHash:X16}");
            }
        }
    }
}

