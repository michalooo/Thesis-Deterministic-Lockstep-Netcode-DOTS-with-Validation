using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Interface for test scenarios that can be validated for determinism.
    /// Use this to create specific test cases for sparse or conditional system behavior.
    /// </summary>
    public interface ITestScenario
    {
        /// <summary>
        /// Name of the scenario for logging.
        /// </summary>
        string Name { get; }
        
        /// <summary>
        /// Set up the initial state for this scenario.
        /// Called before each validation run.
        /// </summary>
        /// <param name="entityManager">EntityManager to use for setup.</param>
        void Setup(EntityManager entityManager);
        
        /// <summary>
        /// Execute the scenario (run systems, advance time, etc.).
        /// </summary>
        /// <param name="world">World to execute in.</param>
        void Execute(World world);
        
        /// <summary>
        /// Clean up after the scenario.
        /// Called after each validation run.
        /// </summary>
        /// <param name="entityManager">EntityManager to use for cleanup.</param>
        void Cleanup(EntityManager entityManager);
    }
    
    /// <summary>
    /// Result of a scenario validation.
    /// </summary>
    public struct ScenarioValidationResult
    {
        public string scenarioName;
        public bool isDeterministic;
        public int numberOfRuns;
        public int firstMismatchRun;
        public ulong expectedHash;
        public ulong actualHash;
        public List<ulong> hashesPerRun;
        public string errorMessage;
    }
    
    /// <summary>
    /// Validates test scenarios for determinism.
    /// </summary>
    public static class ScenarioValidator
    {
        /// <summary>
        /// Validate a test scenario.
        /// </summary>
        /// <param name="world">World to run the scenario in.</param>
        /// <param name="scenario">The scenario to validate.</param>
        /// <param name="numberOfRuns">Number of times to run the scenario.</param>
        /// <returns>Validation result.</returns>
        public static ScenarioValidationResult Validate(World world, ITestScenario scenario, int numberOfRuns = 2)
        {
            var result = new ScenarioValidationResult
            {
                scenarioName = scenario.Name,
                numberOfRuns = numberOfRuns,
                hashesPerRun = new List<ulong>(),
                isDeterministic = true,
                firstMismatchRun = -1
            };
            
            if (world == null || !world.IsCreated)
            {
                result.isDeterministic = false;
                result.errorMessage = "World is null or destroyed.";
                return result;
            }
            
            var entityManager = world.EntityManager;
            
            for (int run = 0; run < numberOfRuns; run++)
            {
                // Setup
                try
                {
                    scenario.Setup(entityManager);
                }
                catch (Exception e)
                {
                    result.isDeterministic = false;
                    result.errorMessage = $"Setup failed: {e.Message}";
                    return result;
                }
                
                // Execute
                try
                {
                    scenario.Execute(world);
                }
                catch (Exception e)
                {
                    result.isDeterministic = false;
                    result.errorMessage = $"Execute failed: {e.Message}";
                    try { scenario.Cleanup(entityManager); } catch { }
                    return result;
                }
                
                // Compute hash
                using (var snapshot = new WorldStateSnapshot())
                {
                    snapshot.Capture(world);
                    var hash = snapshot.ComputeHash();
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
                
                // Cleanup
                try
                {
                    scenario.Cleanup(entityManager);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[ScenarioValidator] Cleanup warning: {e.Message}");
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Validate multiple scenarios.
        /// </summary>
        public static List<ScenarioValidationResult> ValidateAll(World world, IEnumerable<ITestScenario> scenarios, int numberOfRuns = 2)
        {
            var results = new List<ScenarioValidationResult>();
            
            foreach (var scenario in scenarios)
            {
                var result = Validate(world, scenario, numberOfRuns);
                results.Add(result);
            }
            
            return results;
        }
        
        /// <summary>
        /// Print validation result to console.
        /// </summary>
        public static void LogResult(ScenarioValidationResult result)
        {
            if (result.isDeterministic)
            {
                Debug.Log($"[ScenarioValidator] ✓ Scenario '{result.scenarioName}' is DETERMINISTIC ({result.numberOfRuns} runs)");
            }
            else
            {
                var message = $"[ScenarioValidator] ✗ Scenario '{result.scenarioName}' is NONDETERMINISTIC";
                if (!string.IsNullOrEmpty(result.errorMessage))
                {
                    message += $"\n  Error: {result.errorMessage}";
                }
                else
                {
                    message += $"\n  First mismatch at run {result.firstMismatchRun + 1}" +
                              $"\n  Expected: {result.expectedHash:X16}" +
                              $"\n  Actual: {result.actualHash:X16}";
                }
                Debug.LogError(message);
            }
        }
    }
    
    /// <summary>
    /// Base class for simple test scenarios that just run a system N times.
    /// </summary>
    public abstract class SystemTestScenario<TSystem> : ITestScenario where TSystem : unmanaged, ISystem
    {
        public abstract string Name { get; }
        
        /// <summary>
        /// Number of ticks to run the system.
        /// </summary>
        protected virtual int TickCount => 1;
        
        public abstract void Setup(EntityManager entityManager);
        
        public virtual void Execute(World world)
        {
            var systemHandle = world.GetExistingSystem<TSystem>();
            if (systemHandle == SystemHandle.Null)
            {
                throw new InvalidOperationException($"System {typeof(TSystem).Name} not found.");
            }
            
            for (int i = 0; i < TickCount; i++)
            {
                systemHandle.Update(world.Unmanaged);
            }
        }
        
        public abstract void Cleanup(EntityManager entityManager);
    }
    
    /// <summary>
    /// Scenario that runs a system group for N ticks.
    /// </summary>
    public abstract class SystemGroupTestScenario : ITestScenario
    {
        public abstract string Name { get; }
        
        /// <summary>
        /// Number of ticks to simulate.
        /// </summary>
        protected virtual int TickCount => 100;
        
        /// <summary>
        /// Get the system group to run.
        /// </summary>
        protected abstract ComponentSystemGroup GetSystemGroup(World world);
        
        public abstract void Setup(EntityManager entityManager);
        
        public virtual void Execute(World world)
        {
            var systemGroup = GetSystemGroup(world);
            if (systemGroup == null)
            {
                throw new InvalidOperationException("System group not found.");
            }
            
            for (int i = 0; i < TickCount; i++)
            {
                systemGroup.Update();
            }
        }
        
        public abstract void Cleanup(EntityManager entityManager);
    }
}

