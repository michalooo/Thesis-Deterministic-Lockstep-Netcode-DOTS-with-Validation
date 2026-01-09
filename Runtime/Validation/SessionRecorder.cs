using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Records game sessions for replay-based validation.
    /// Captures initial state and per-tick hashes.
    /// </summary>
    public class SessionRecorder : MonoBehaviour
    {
        public static SessionRecorder Instance { get; private set; }
        
        private bool _isRecording = false;
        private RecordedSession _currentSession;
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
        /// Start recording a new session.
        /// </summary>
        public void StartRecording(World world = null, uint randomSeed = 0)
        {
            if (_isRecording)
            {
                Debug.LogWarning("[SessionRecorder] Already recording.");
                return;
            }
            
            _world = world ?? World.DefaultGameObjectInjectionWorld;
            
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogError("[SessionRecorder] Cannot start recording - no valid world.");
                return;
            }
            
            _currentSession = new RecordedSession
            {
                metadata = new SessionMetadata
                {
                    recordedAt = DateTime.Now.ToString("o"),
                    platform = Application.platform.ToString(),
                    unityVersion = Application.unityVersion,
                    randomSeed = randomSeed
                },
                tickHashes = new List<TickRecord>()
            };
            
            // Capture initial state
            using (var snapshot = new WorldStateSnapshot())
            {
                snapshot.Capture(_world);
                _currentSession.initialStateHash = snapshot.ComputeHash();
            }
            
            _isRecording = true;
            Debug.Log("[SessionRecorder] Recording started.");
        }

        /// <summary>
        /// Record data for current tick.
        /// </summary>
        public void RecordTick(int tick, ulong hash)
        {
            if (!_isRecording)
                return;
                
            _currentSession.tickHashes.Add(new TickRecord
            {
                tick = tick,
                hash = hash
            });
        }

        /// <summary>
        /// Stop recording and return the session.
        /// </summary>
        public RecordedSession StopRecording()
        {
            if (!_isRecording)
            {
                Debug.LogWarning("[SessionRecorder] Not recording.");
                return default;
            }
            
            _isRecording = false;
            _currentSession.metadata.totalTicks = _currentSession.tickHashes.Count;
            
            // Compute final state hash
            if (_world != null && _world.IsCreated)
            {
                using (var snapshot = new WorldStateSnapshot())
                {
                    snapshot.Capture(_world);
                    _currentSession.finalStateHash = snapshot.ComputeHash();
                }
            }
            
            Debug.Log($"[SessionRecorder] Recording stopped. {_currentSession.tickHashes.Count} ticks recorded.");
            
            return _currentSession;
        }

        /// <summary>
        /// Save session to file.
        /// </summary>
        public void SaveSession(RecordedSession session, string filename = null)
        {
            var directory = Path.Combine(Application.dataPath, "..", "ValidationLogs", "Sessions");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            filename ??= $"session_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.json";
            var path = Path.Combine(directory, filename);
            
            var json = JsonUtility.ToJson(session, true);
            File.WriteAllText(path, json);
            
            Debug.Log($"[SessionRecorder] Session saved to: {path}");
        }

        /// <summary>
        /// Load session from file.
        /// </summary>
        public static RecordedSession LoadSession(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[SessionRecorder] Session file not found: {path}");
                return default;
            }
            
            var json = File.ReadAllText(path);
            return JsonUtility.FromJson<RecordedSession>(json);
        }

        /// <summary>
        /// Whether currently recording.
        /// </summary>
        public bool IsRecording => _isRecording;
    }
    
    /// <summary>
    /// Metadata for a recorded session.
    /// </summary>
    [Serializable]
    public struct SessionMetadata
    {
        public string recordedAt;
        public string platform;
        public string unityVersion;
        public uint randomSeed;
        public int totalTicks;
    }
    
    /// <summary>
    /// Record for a single tick.
    /// </summary>
    [Serializable]
    public struct TickRecord
    {
        public int tick;
        public ulong hash;
    }
    
    /// <summary>
    /// A recorded game session.
    /// </summary>
    [Serializable]
    public struct RecordedSession
    {
        public SessionMetadata metadata;
        public ulong initialStateHash;
        public ulong finalStateHash;
        public List<TickRecord> tickHashes;
    }
}

