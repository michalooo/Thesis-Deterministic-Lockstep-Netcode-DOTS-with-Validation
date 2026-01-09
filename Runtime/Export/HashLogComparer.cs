using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Compares hash logs from different platforms or runs.
    /// Can be used with Yamato CI for cross-platform validation.
    /// </summary>
    public static class HashLogComparer
    {
        /// <summary>
        /// Compare two hash logs.
        /// </summary>
        public static HashLogComparisonResult Compare(HashLog log1, HashLog log2)
        {
            var result = new HashLogComparisonResult
            {
                log1Metadata = log1.metadata,
                log2Metadata = log2.metadata,
                differences = new List<HashDifference>()
            };
            
            // Compare final hashes
            result.finalHashesMatch = log1.finalHash == log2.finalHash;
            
            // Build lookup for log2 ticks
            var log2TickHashes = new Dictionary<int, ulong>();
            if (log2.perTickHashes != null)
            {
                foreach (var entry in log2.perTickHashes)
                {
                    log2TickHashes[entry.tick] = entry.hash;
                }
            }
            
            // Compare per-tick hashes
            int matchingTicks = 0;
            int firstDifferenceTick = -1;
            
            if (log1.perTickHashes != null)
            {
                foreach (var entry1 in log1.perTickHashes)
                {
                    if (log2TickHashes.TryGetValue(entry1.tick, out var hash2))
                    {
                        if (entry1.hash == hash2)
                        {
                            matchingTicks++;
                        }
                        else
                        {
                            if (firstDifferenceTick == -1)
                            {
                                firstDifferenceTick = entry1.tick;
                            }
                            
                            result.differences.Add(new HashDifference
                            {
                                tick = entry1.tick,
                                hash1 = entry1.hash,
                                hash2 = hash2,
                                type = DifferenceType.HashMismatch
                            });
                        }
                    }
                    else
                    {
                        result.differences.Add(new HashDifference
                        {
                            tick = entry1.tick,
                            hash1 = entry1.hash,
                            hash2 = 0,
                            type = DifferenceType.MissingInLog2
                        });
                    }
                }
            }
            
            // Check for ticks in log2 but not in log1
            var log1TickHashes = new HashSet<int>();
            if (log1.perTickHashes != null)
            {
                foreach (var entry in log1.perTickHashes)
                {
                    log1TickHashes.Add(entry.tick);
                }
            }
            
            if (log2.perTickHashes != null)
            {
                foreach (var entry2 in log2.perTickHashes)
                {
                    if (!log1TickHashes.Contains(entry2.tick))
                    {
                        result.differences.Add(new HashDifference
                        {
                            tick = entry2.tick,
                            hash1 = 0,
                            hash2 = entry2.hash,
                            type = DifferenceType.MissingInLog1
                        });
                    }
                }
            }
            
            result.matchingTickCount = matchingTicks;
            result.firstDifferenceTick = firstDifferenceTick;
            result.areDeterministic = result.differences.Count == 0 && result.finalHashesMatch;
            
            return result;
        }

        /// <summary>
        /// Compare two hash log files.
        /// </summary>
        public static HashLogComparisonResult CompareFiles(string path1, string path2)
        {
            var log1 = path1.EndsWith(".bin") 
                ? HashLogExporter.LoadFromBinary(path1) 
                : HashLogExporter.LoadFromJson(path1);
                
            var log2 = path2.EndsWith(".bin") 
                ? HashLogExporter.LoadFromBinary(path2) 
                : HashLogExporter.LoadFromJson(path2);
            
            return Compare(log1, log2);
        }

        /// <summary>
        /// Generate a human-readable comparison report.
        /// </summary>
        public static string GenerateReport(HashLogComparisonResult result)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=== HASH LOG COMPARISON REPORT ===");
            sb.AppendLine();
            
            sb.AppendLine("LOG 1:");
            sb.AppendLine($"  Platform: {result.log1Metadata.platform}");
            sb.AppendLine($"  Unity: {result.log1Metadata.unityVersion}");
            sb.AppendLine($"  Timestamp: {result.log1Metadata.timestamp}");
            sb.AppendLine($"  Ticks: {result.log1Metadata.tickCount}");
            sb.AppendLine();
            
            sb.AppendLine("LOG 2:");
            sb.AppendLine($"  Platform: {result.log2Metadata.platform}");
            sb.AppendLine($"  Unity: {result.log2Metadata.unityVersion}");
            sb.AppendLine($"  Timestamp: {result.log2Metadata.timestamp}");
            sb.AppendLine($"  Ticks: {result.log2Metadata.tickCount}");
            sb.AppendLine();
            
            sb.AppendLine("COMPARISON:");
            sb.AppendLine($"  Final hashes match: {(result.finalHashesMatch ? "YES" : "NO")}");
            sb.AppendLine($"  Matching ticks: {result.matchingTickCount}");
            sb.AppendLine($"  Differences: {result.differences.Count}");
            
            if (result.firstDifferenceTick >= 0)
            {
                sb.AppendLine($"  First difference at tick: {result.firstDifferenceTick}");
            }
            sb.AppendLine();
            
            if (result.areDeterministic)
            {
                sb.AppendLine("RESULT: DETERMINISTIC - Logs match!");
            }
            else
            {
                sb.AppendLine("RESULT: NONDETERMINISTIC - Logs differ!");
                sb.AppendLine();
                sb.AppendLine("DIFFERENCES:");
                
                int shown = 0;
                foreach (var diff in result.differences)
                {
                    if (shown >= 20)
                    {
                        sb.AppendLine($"  ... and {result.differences.Count - 20} more differences");
                        break;
                    }
                    
                    switch (diff.type)
                    {
                        case DifferenceType.HashMismatch:
                            sb.AppendLine($"  Tick {diff.tick}: MISMATCH");
                            sb.AppendLine($"    Log1: {diff.hash1:X16}");
                            sb.AppendLine($"    Log2: {diff.hash2:X16}");
                            break;
                        case DifferenceType.MissingInLog1:
                            sb.AppendLine($"  Tick {diff.tick}: Missing in Log1");
                            break;
                        case DifferenceType.MissingInLog2:
                            sb.AppendLine($"  Tick {diff.tick}: Missing in Log2");
                            break;
                    }
                    
                    shown++;
                }
            }
            
            return sb.ToString();
        }

        /// <summary>
        /// Export comparison report to file.
        /// </summary>
        public static string ExportReport(HashLogComparisonResult result, string outputPath = null)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                var directory = Path.Combine(Application.dataPath, "..", "ValidationLogs", "Comparisons");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                outputPath = Path.Combine(directory, $"comparison_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.txt");
            }
            
            var report = GenerateReport(result);
            File.WriteAllText(outputPath, report);
            
            Debug.Log($"[HashLogComparer] Report exported to: {outputPath}");
            return outputPath;
        }

        /// <summary>
        /// Compare and export - convenience method for CI.
        /// Returns exit code (0 = match, 1 = mismatch).
        /// </summary>
        public static int CompareAndReport(string path1, string path2, string reportPath = null)
        {
            var result = CompareFiles(path1, path2);
            ExportReport(result, reportPath);
            
            if (result.areDeterministic)
            {
                Debug.Log("[HashLogComparer] Comparison PASSED - logs are deterministic.");
                return 0;
            }
            else
            {
                Debug.LogError($"[HashLogComparer] Comparison FAILED - first difference at tick {result.firstDifferenceTick}");
                return 1;
            }
        }
    }
    
    /// <summary>
    /// Type of difference between logs.
    /// </summary>
    public enum DifferenceType
    {
        HashMismatch,
        MissingInLog1,
        MissingInLog2
    }
    
    /// <summary>
    /// A single difference between two logs.
    /// </summary>
    [Serializable]
    public struct HashDifference
    {
        public int tick;
        public ulong hash1;
        public ulong hash2;
        public DifferenceType type;
    }
    
    /// <summary>
    /// Result of comparing two hash logs.
    /// </summary>
    [Serializable]
    public struct HashLogComparisonResult
    {
        public HashLogMetadata log1Metadata;
        public HashLogMetadata log2Metadata;
        public bool areDeterministic;
        public bool finalHashesMatch;
        public int matchingTickCount;
        public int firstDifferenceTick;
        public List<HashDifference> differences;
    }
}

