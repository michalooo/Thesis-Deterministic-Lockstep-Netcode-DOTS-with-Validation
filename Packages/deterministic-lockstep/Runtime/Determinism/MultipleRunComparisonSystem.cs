using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// System that handles hash collection and comparison for single-player validation.
    /// Collects hashes each tick and stores them for comparison across multiple runs.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(StateHashForValidationSystem))]
    [UpdateBefore(typeof(PlayerInputSendSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.Default)]
    public partial struct MultipleRunComparisonSystem : ISystem
    {
        /// <summary>
        /// Stored hashes for the current run, keyed by tick.
        /// </summary>
        private NativeHashMap<int, ulong> _currentRunHashes;
        
        /// <summary>
        /// Whether the hash storage has been initialized.
        /// </summary>
        private bool _initialized;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicSettings>();
            state.RequireForUpdate<DeterministicSimulationTime>();
            state.RequireForUpdate<DeterminismValidationSettings>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var settings = SystemAPI.GetSingleton<DeterministicSettings>();
            
            // Only run in single-player validation mode
            if (settings.validationMode != DeterminismValidationMode.SinglePlayer &&
                settings.validationMode != DeterminismValidationMode.SystemLevel)
            {
                return;
            }
            
            var validationSettings = SystemAPI.GetSingleton<DeterminismValidationSettings>();
            
            // Skip if validation is not in progress
            if (!validationSettings.isValidationInProgress || validationSettings.isValidationComplete)
            {
                return;
            }
            
            // Initialize hash storage if needed
            if (!_initialized || !_currentRunHashes.IsCreated)
            {
                _currentRunHashes = new NativeHashMap<int, ulong>(validationSettings.ticksToSimulate + 1, Allocator.Persistent);
                _initialized = true;
            }
            
            var simTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
            var currentTick = simTime.currentSimulationTick;
            
            // Get the hash computed by StateHashForValidationSystem
            if (simTime.hashesForTheCurrentTick.Length > 0)
            {
                var currentHash = simTime.hashesForTheCurrentTick[simTime.hashesForTheCurrentTick.Length - 1];
                
                // Store the hash
                if (!_currentRunHashes.ContainsKey(currentTick))
                {
                    _currentRunHashes.Add(currentTick, currentHash);
                }
                else
                {
                    _currentRunHashes[currentTick] = currentHash;
                }
                
                // Report to the validation manager if it exists
                if (SinglePlayerValidationManager.Instance != null)
                {
                    SinglePlayerValidationManager.Instance.RecordHash(currentTick, currentHash);
                }
            }
        }
        
        public void OnDestroy(ref SystemState state)
        {
            if (_currentRunHashes.IsCreated)
            {
                _currentRunHashes.Dispose();
            }
        }
        
        /// <summary>
        /// Clear stored hashes for a new run.
        /// </summary>
        public void ClearHashes()
        {
            if (_currentRunHashes.IsCreated)
            {
                _currentRunHashes.Clear();
            }
        }
        
        /// <summary>
        /// Get the hash for a specific tick.
        /// </summary>
        public bool TryGetHash(int tick, out ulong hash)
        {
            hash = 0;
            if (_currentRunHashes.IsCreated && _currentRunHashes.ContainsKey(tick))
            {
                hash = _currentRunHashes[tick];
                return true;
            }
            return false;
        }
    }
    
    /// <summary>
    /// Utility class for comparing and analyzing hash results from multiple runs.
    /// </summary>
    public static class HashComparisonUtility
    {
        /// <summary>
        /// Compare hashes between two runs and find differences.
        /// </summary>
        /// <param name="run1Hashes">Hashes from run 1, keyed by tick.</param>
        /// <param name="run2Hashes">Hashes from run 2, keyed by tick.</param>
        /// <returns>List of ticks where hashes differ.</returns>
        public static List<HashDifference> CompareRuns(
            Dictionary<int, ulong> run1Hashes, 
            Dictionary<int, ulong> run2Hashes)
        {
            var differences = new List<HashDifference>();
            
            // Get all unique ticks from both runs
            var allTicks = new HashSet<int>();
            foreach (var tick in run1Hashes.Keys) allTicks.Add(tick);
            foreach (var tick in run2Hashes.Keys) allTicks.Add(tick);
            
            foreach (var tick in allTicks)
            {
                var hasRun1 = run1Hashes.TryGetValue(tick, out var hash1);
                var hasRun2 = run2Hashes.TryGetValue(tick, out var hash2);
                
                if (!hasRun1 || !hasRun2)
                {
                    differences.Add(new HashDifference
                    {
                        tick = tick,
                        run1Hash = hasRun1 ? hash1 : 0,
                        run2Hash = hasRun2 ? hash2 : 0,
                        isMissing = true,
                        missingInRun1 = !hasRun1,
                        missingInRun2 = !hasRun2
                    });
                }
                else if (hash1 != hash2)
                {
                    differences.Add(new HashDifference
                    {
                        tick = tick,
                        run1Hash = hash1,
                        run2Hash = hash2,
                        isMissing = false
                    });
                }
            }
            
            differences.Sort((a, b) => a.tick.CompareTo(b.tick));
            return differences;
        }
        
        /// <summary>
        /// Generate a comparison report.
        /// </summary>
        public static string GenerateReport(
            List<HashDifference> differences, 
            int totalTicks,
            int run1Index,
            int run2Index)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== DETERMINISM VALIDATION REPORT ===");
            sb.AppendLine($"Comparing Run {run1Index + 1} vs Run {run2Index + 1}");
            sb.AppendLine($"Total ticks simulated: {totalTicks}");
            sb.AppendLine($"Differences found: {differences.Count}");
            sb.AppendLine();
            
            if (differences.Count == 0)
            {
                sb.AppendLine("✓ All hashes match - simulation is deterministic!");
            }
            else
            {
                sb.AppendLine("✗ NONDETERMINISM DETECTED");
                sb.AppendLine();
                sb.AppendLine("Differences:");
                sb.AppendLine("-".PadRight(80, '-'));
                
                var maxToShow = Mathf.Min(differences.Count, 50);
                for (int i = 0; i < maxToShow; i++)
                {
                    var diff = differences[i];
                    if (diff.isMissing)
                    {
                        if (diff.missingInRun1)
                            sb.AppendLine($"Tick {diff.tick}: Missing in Run {run1Index + 1}");
                        else
                            sb.AppendLine($"Tick {diff.tick}: Missing in Run {run2Index + 1}");
                    }
                    else
                    {
                        sb.AppendLine($"Tick {diff.tick}: Run {run1Index + 1} = {diff.run1Hash:X16}, Run {run2Index + 1} = {diff.run2Hash:X16}");
                    }
                }
                
                if (differences.Count > maxToShow)
                {
                    sb.AppendLine($"... and {differences.Count - maxToShow} more differences");
                }
                
                sb.AppendLine("-".PadRight(80, '-'));
                sb.AppendLine();
                sb.AppendLine($"First difference at tick: {differences[0].tick}");
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// Save a comparison report to file.
        /// </summary>
        public static void SaveReport(string report, string filename = null)
        {
            if (string.IsNullOrEmpty(filename))
            {
                filename = $"ValidationReport_{System.DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt";
            }
            
            var directory = Path.Combine(Application.dataPath, "..", "NonDeterminismLogs");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            var path = Path.Combine(directory, filename);
            File.WriteAllText(path, report);
            Debug.Log($"[HashComparisonUtility] Report saved to: {path}");
        }
    }
    
    /// <summary>
    /// Represents a difference in hashes between runs.
    /// </summary>
    public struct HashDifference
    {
        /// <summary>
        /// The tick where the difference occurred.
        /// </summary>
        public int tick;
        
        /// <summary>
        /// Hash from run 1.
        /// </summary>
        public ulong run1Hash;
        
        /// <summary>
        /// Hash from run 2.
        /// </summary>
        public ulong run2Hash;
        
        /// <summary>
        /// Whether this difference is due to a missing hash.
        /// </summary>
        public bool isMissing;
        
        /// <summary>
        /// Whether the hash is missing in run 1.
        /// </summary>
        public bool missingInRun1;
        
        /// <summary>
        /// Whether the hash is missing in run 2.
        /// </summary>
        public bool missingInRun2;
    }
}

