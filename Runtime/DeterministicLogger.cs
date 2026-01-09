using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Logger for determinism validation. Collects hash logs per tick for debugging nondeterminism.
    /// </summary>
    public class DeterministicLogger : MonoBehaviour
    {
        public static DeterministicLogger Instance { get; private set; }
        
        /// <summary>
        /// Hash logs per run, per tick.
        /// </summary>
        private Dictionary<int, Dictionary<int, List<string>>> _hashLogsPerRun = new Dictionary<int, Dictionary<int, List<string>>>();
        
        /// <summary>
        /// Current run index for logging.
        /// </summary>
        private int _currentRunIndex = 0;
        
        /// <summary>
        /// Directory for output files.
        /// </summary>
        private string _outputDirectory;
        
        /// <summary>
        /// Counter for deterministic entity IDs.
        /// </summary>
        private int _entityIdCounter = 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Create output directory
            _outputDirectory = Path.Combine(Application.dataPath, "..", "ValidationLogs",
                $"{DateTime.Now:yyyy_MM_dd_HH_mm_ss}");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Get the next deterministic entity ID.
        /// </summary>
        public int GetNextEntityId()
        {
            return _entityIdCounter++;
        }
        
        /// <summary>
        /// Reset entity ID counter (call at start of each run).
        /// </summary>
        public void ResetEntityIdCounter()
        {
            _entityIdCounter = 0;
        }

        /// <summary>
        /// Start a new validation run.
        /// </summary>
        public void StartNewRun(int runIndex)
        {
            _currentRunIndex = runIndex;
            if (!_hashLogsPerRun.ContainsKey(runIndex))
            {
                _hashLogsPerRun[runIndex] = new Dictionary<int, List<string>>();
            }
            ResetEntityIdCounter();
        }

        /// <summary>
        /// Add a log entry for the current tick.
        /// </summary>
        public void AddToHashLog(string worldName, int tick, string message)
        {
            if (!_hashLogsPerRun.ContainsKey(_currentRunIndex))
            {
                _hashLogsPerRun[_currentRunIndex] = new Dictionary<int, List<string>>();
            }
            
            var runLogs = _hashLogsPerRun[_currentRunIndex];
            if (!runLogs.ContainsKey(tick))
            {
                runLogs[tick] = new List<string>();
            }
            
            runLogs[tick].Add($"[{worldName}] {message}");
        }

        /// <summary>
        /// Get hash logs for a specific run.
        /// </summary>
        public Dictionary<int, List<string>> GetLogsForRun(int runIndex)
        {
            return _hashLogsPerRun.TryGetValue(runIndex, out var logs) ? logs : null;
        }

        /// <summary>
        /// Clear all logs.
        /// </summary>
        public void ClearAllLogs()
        {
            _hashLogsPerRun.Clear();
            _currentRunIndex = 0;
        }

        /// <summary>
        /// Export logs to file.
        /// </summary>
        public void ExportLogs(string filename = null)
        {
            if (_hashLogsPerRun.Count == 0)
            {
                Debug.Log("[DeterministicLogger] No logs to export.");
                return;
            }
            
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
            
            filename ??= "validation_log.txt";
            var path = Path.Combine(_outputDirectory, filename);
            
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("=== DETERMINISM VALIDATION LOG ===");
                writer.WriteLine($"Generated: {DateTime.Now}");
                writer.WriteLine($"Total runs: {_hashLogsPerRun.Count}");
                writer.WriteLine();
                
                foreach (var runEntry in _hashLogsPerRun)
                {
                    writer.WriteLine($"--- RUN {runEntry.Key + 1} ---");
                    var sortedTicks = new List<int>(runEntry.Value.Keys);
                    sortedTicks.Sort();
                    
                    foreach (var tick in sortedTicks)
                    {
                        writer.WriteLine($"Tick {tick}:");
                        foreach (var logLine in runEntry.Value[tick])
                        {
                            writer.WriteLine($"  {logLine}");
                        }
                    }
                    writer.WriteLine();
                }
            }
            
            Debug.Log($"[DeterministicLogger] Logs exported to: {path}");
        }

        /// <summary>
        /// Export comparison report between two runs.
        /// </summary>
        public void ExportComparisonReport(int run1Index, int run2Index, string filename = null)
        {
            if (!_hashLogsPerRun.ContainsKey(run1Index) || !_hashLogsPerRun.ContainsKey(run2Index))
            {
                Debug.LogError($"[DeterministicLogger] Cannot compare runs {run1Index} and {run2Index} - logs not found.");
                return;
            }
            
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
            
            filename ??= $"comparison_run{run1Index + 1}_vs_run{run2Index + 1}.txt";
            var path = Path.Combine(_outputDirectory, filename);
            
            var run1Logs = _hashLogsPerRun[run1Index];
            var run2Logs = _hashLogsPerRun[run2Index];
            
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("=== DETERMINISM COMPARISON REPORT ===");
                writer.WriteLine($"Comparing Run {run1Index + 1} vs Run {run2Index + 1}");
                writer.WriteLine($"Generated: {DateTime.Now}");
                writer.WriteLine();
                
                // Find all ticks
                var allTicks = new HashSet<int>();
                foreach (var tick in run1Logs.Keys) allTicks.Add(tick);
                foreach (var tick in run2Logs.Keys) allTicks.Add(tick);
                
                var sortedTicks = new List<int>(allTicks);
                sortedTicks.Sort();
                
                int firstDifferenceTick = -1;
                
                foreach (var tick in sortedTicks)
                {
                    var hasRun1 = run1Logs.TryGetValue(tick, out var logs1);
                    var hasRun2 = run2Logs.TryGetValue(tick, out var logs2);
                    
                    if (!hasRun1 || !hasRun2)
                    {
                        if (firstDifferenceTick == -1) firstDifferenceTick = tick;
                        writer.WriteLine($"Tick {tick}: MISSING in Run {(hasRun1 ? run2Index + 1 : run1Index + 1)}");
                        continue;
                    }
                    
                    // Compare logs
                    var isDifferent = false;
                    if (logs1.Count != logs2.Count)
                    {
                        isDifferent = true;
                    }
                    else
                    {
                        for (int i = 0; i < logs1.Count; i++)
                        {
                            if (logs1[i] != logs2[i])
                            {
                                isDifferent = true;
                                break;
                            }
                        }
                    }
                    
                    if (isDifferent)
                    {
                        if (firstDifferenceTick == -1) firstDifferenceTick = tick;
                        writer.WriteLine($"Tick {tick}: DIFFERENT");
                        writer.WriteLine("  Run 1:");
                        foreach (var line in logs1) writer.WriteLine($"    {line}");
                        writer.WriteLine("  Run 2:");
                        foreach (var line in logs2) writer.WriteLine($"    {line}");
                    }
                }
                
                writer.WriteLine();
                if (firstDifferenceTick == -1)
                {
                    writer.WriteLine("RESULT: All ticks match - runs are deterministic!");
                }
                else
                {
                    writer.WriteLine($"RESULT: First difference at tick {firstDifferenceTick}");
                }
            }
            
            Debug.Log($"[DeterministicLogger] Comparison report exported to: {path}");
        }

        /// <summary>
        /// Log validation settings to file.
        /// </summary>
        public void LogSettings(DeterministicSettings settings)
        {
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
            
            var path = Path.Combine(_outputDirectory, "settings.json");
            var json = JsonUtility.ToJson(settings, true);
            File.WriteAllText(path, json);
            
            Debug.Log($"[DeterministicLogger] Settings exported to: {path}");
        }

        /// <summary>
        /// Log system info to file.
        /// </summary>
        public void LogSystemInfo()
        {
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
            
            var path = Path.Combine(_outputDirectory, "system_info.txt");
            
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("=== SYSTEM INFO ===");
                writer.WriteLine($"Operating System: {SystemInfo.operatingSystem}");
                writer.WriteLine($"Processor: {SystemInfo.processorType} ({SystemInfo.processorCount} cores)");
                writer.WriteLine($"GPU: {SystemInfo.graphicsDeviceName}");
                writer.WriteLine($"VRAM: {SystemInfo.graphicsMemorySize} MB");
                writer.WriteLine($"RAM: {SystemInfo.systemMemorySize} MB");
                writer.WriteLine($"Unity Version: {Application.unityVersion}");
                writer.WriteLine($"Platform: {Application.platform}");
            }
            
            Debug.Log($"[DeterministicLogger] System info exported to: {path}");
        }
    }
}
