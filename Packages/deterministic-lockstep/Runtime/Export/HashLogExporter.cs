using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Exports hash logs in a standard JSON format for cross-platform comparison.
    /// </summary>
    public static class HashLogExporter
    {
        /// <summary>
        /// Export hashes to JSON file.
        /// </summary>
        public static string ExportToJson(HashLog log, string outputPath = null)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                var directory = Path.Combine(Application.dataPath, "..", "ValidationLogs", "HashLogs");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                outputPath = Path.Combine(directory, $"hashlog_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.json");
            }
            
            var json = JsonUtility.ToJson(log, true);
            File.WriteAllText(outputPath, json);
            
            Debug.Log($"[HashLogExporter] Exported to: {outputPath}");
            return outputPath;
        }

        /// <summary>
        /// Export hashes to binary file (more compact).
        /// </summary>
        public static string ExportToBinary(HashLog log, string outputPath = null)
        {
            if (string.IsNullOrEmpty(outputPath))
            {
                var directory = Path.Combine(Application.dataPath, "..", "ValidationLogs", "HashLogs");
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                outputPath = Path.Combine(directory, $"hashlog_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.bin");
            }
            
            using (var stream = new FileStream(outputPath, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                // Write metadata
                writer.Write(log.metadata.platform ?? "");
                writer.Write(log.metadata.unityVersion ?? "");
                writer.Write(log.metadata.timestamp ?? "");
                writer.Write(log.metadata.tickCount);
                writer.Write(log.metadata.randomSeed);
                
                // Write final hash
                writer.Write(log.finalHash);
                
                // Write per-tick hashes
                writer.Write(log.perTickHashes?.Count ?? 0);
                if (log.perTickHashes != null)
                {
                    foreach (var tickHash in log.perTickHashes)
                    {
                        writer.Write(tickHash.tick);
                        writer.Write(tickHash.hash);
                    }
                }
                
                // Write per-system hashes
                writer.Write(log.perSystemHashes?.Count ?? 0);
                if (log.perSystemHashes != null)
                {
                    foreach (var systemEntry in log.perSystemHashes)
                    {
                        writer.Write(systemEntry.systemName ?? "");
                        writer.Write(systemEntry.hashes?.Count ?? 0);
                        if (systemEntry.hashes != null)
                        {
                            foreach (var hash in systemEntry.hashes)
                            {
                                writer.Write(hash);
                            }
                        }
                    }
                }
            }
            
            Debug.Log($"[HashLogExporter] Exported binary to: {outputPath}");
            return outputPath;
        }

        /// <summary>
        /// Load hash log from JSON file.
        /// </summary>
        public static HashLog LoadFromJson(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[HashLogExporter] File not found: {path}");
                return default;
            }
            
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<HashLog>(json);
        }

        /// <summary>
        /// Load hash log from binary file.
        /// </summary>
        public static HashLog LoadFromBinary(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[HashLogExporter] File not found: {path}");
                return default;
            }
            
            var log = new HashLog();
            
            using (var stream = new FileStream(path, FileMode.Open))
            using (var reader = new BinaryReader(stream))
            {
                // Read metadata
                log.metadata = new HashLogMetadata
                {
                    platform = reader.ReadString(),
                    unityVersion = reader.ReadString(),
                    timestamp = reader.ReadString(),
                    tickCount = reader.ReadInt32(),
                    randomSeed = reader.ReadUInt32()
                };
                
                // Read final hash
                log.finalHash = reader.ReadUInt64();
                
                // Read per-tick hashes
                int tickCount = reader.ReadInt32();
                log.perTickHashes = new List<TickHashEntry>(tickCount);
                for (int i = 0; i < tickCount; i++)
                {
                    log.perTickHashes.Add(new TickHashEntry
                    {
                        tick = reader.ReadInt32(),
                        hash = reader.ReadUInt64()
                    });
                }
                
                // Read per-system hashes
                int systemCount = reader.ReadInt32();
                log.perSystemHashes = new List<SystemHashEntry>(systemCount);
                for (int i = 0; i < systemCount; i++)
                {
                    var entry = new SystemHashEntry
                    {
                        systemName = reader.ReadString(),
                        hashes = new List<ulong>()
                    };
                    int hashCount = reader.ReadInt32();
                    for (int j = 0; j < hashCount; j++)
                    {
                        entry.hashes.Add(reader.ReadUInt64());
                    }
                    log.perSystemHashes.Add(entry);
                }
            }
            
            return log;
        }

        /// <summary>
        /// Create a hash log from validation result.
        /// </summary>
        public static HashLog CreateFromValidationResult(GameValidationResult result, int runIndex = 0)
        {
            var log = new HashLog
            {
                metadata = new HashLogMetadata
                {
                    platform = Application.platform.ToString(),
                    unityVersion = Application.unityVersion,
                    timestamp = DateTime.Now.ToString("o"),
                    tickCount = result.ticksSimulated
                },
                perTickHashes = new List<TickHashEntry>()
            };
            
            if (result.hashesPerRun != null && result.hashesPerRun.ContainsKey(runIndex))
            {
                var runHashes = result.hashesPerRun[runIndex];
                foreach (var kvp in runHashes)
                {
                    log.perTickHashes.Add(new TickHashEntry
                    {
                        tick = kvp.Key,
                        hash = kvp.Value
                    });
                }
                
                // Set final hash as last tick's hash
                if (log.perTickHashes.Count > 0)
                {
                    log.perTickHashes.Sort((a, b) => a.tick.CompareTo(b.tick));
                    log.finalHash = log.perTickHashes[log.perTickHashes.Count - 1].hash;
                }
            }
            
            return log;
        }

        /// <summary>
        /// Create a hash log from recorded session.
        /// </summary>
        public static HashLog CreateFromSession(RecordedSession session)
        {
            var log = new HashLog
            {
                metadata = new HashLogMetadata
                {
                    platform = session.metadata.platform,
                    unityVersion = session.metadata.unityVersion,
                    timestamp = session.metadata.recordedAt,
                    tickCount = session.metadata.totalTicks,
                    randomSeed = session.metadata.randomSeed
                },
                finalHash = session.finalStateHash,
                perTickHashes = new List<TickHashEntry>()
            };
            
            foreach (var record in session.tickHashes)
            {
                log.perTickHashes.Add(new TickHashEntry
                {
                    tick = record.tick,
                    hash = record.hash
                });
            }
            
            return log;
        }
    }
    
    /// <summary>
    /// Metadata for a hash log.
    /// </summary>
    [Serializable]
    public struct HashLogMetadata
    {
        public string platform;
        public string unityVersion;
        public string timestamp;
        public int tickCount;
        public uint randomSeed;
    }
    
    /// <summary>
    /// Hash entry for a single tick.
    /// </summary>
    [Serializable]
    public struct TickHashEntry
    {
        public int tick;
        public ulong hash;
    }
    
    /// <summary>
    /// Hash entries for a system.
    /// </summary>
    [Serializable]
    public struct SystemHashEntry
    {
        public string systemName;
        public List<ulong> hashes;
    }
    
    /// <summary>
    /// Complete hash log for a validation run.
    /// </summary>
    [Serializable]
    public struct HashLog
    {
        public HashLogMetadata metadata;
        public ulong finalHash;
        public List<TickHashEntry> perTickHashes;
        public List<SystemHashEntry> perSystemHashes;
    }
}

