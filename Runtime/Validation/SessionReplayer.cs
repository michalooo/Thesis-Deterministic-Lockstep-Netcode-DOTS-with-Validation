using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace DeterministicLockstep
{
    /// <summary>
    /// Replays recorded sessions and compares hashes for validation.
    /// </summary>
    public class SessionReplayer : MonoBehaviour
    {
        public static SessionReplayer Instance { get; private set; }
        
        /// <summary>
        /// Event fired when replay validation completes.
        /// </summary>
        public event Action<ReplayValidationResult> OnReplayComplete;
        
        /// <summary>
        /// Event fired when hash mismatch detected during replay.
        /// </summary>
        public event Action<int, ulong, ulong> OnHashMismatch; // tick, expected, actual
        
        private bool _isReplaying = false;
        private RecordedSession _sessionToReplay;
        private ReplayValidationResult _result;
        private World _world;
        private int _currentReplayTick = 0;
        private Dictionary<int, ulong> _replayHashes = new Dictionary<int, ulong>();

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
        /// Start replaying a recorded session.
        /// </summary>
        public void StartReplay(RecordedSession session, World world = null)
        {
            if (_isReplaying)
            {
                Debug.LogWarning("[SessionReplayer] Already replaying.");
                return;
            }
            
            _world = world ?? World.DefaultGameObjectInjectionWorld;
            
            if (_world == null || !_world.IsCreated)
            {
                Debug.LogError("[SessionReplayer] Cannot start replay - no valid world.");
                return;
            }
            
            _sessionToReplay = session;
            _currentReplayTick = 0;
            _replayHashes.Clear();
            _result = new ReplayValidationResult
            {
                originalSession = session,
                matchesOriginal = true,
                mismatchedTicks = new List<TickMismatch>()
            };
            
            // Set random seed
            UpdateValidationSettings(session.metadata.randomSeed);
            
            _isReplaying = true;
            Debug.Log($"[SessionReplayer] Starting replay of session with {session.tickHashes.Count} ticks.");
        }

        /// <summary>
        /// Stop current replay.
        /// </summary>
        public void StopReplay()
        {
            _isReplaying = false;
            Debug.Log("[SessionReplayer] Replay stopped.");
        }

        /// <summary>
        /// Record hash during replay (called by hash system).
        /// </summary>
        public void RecordReplayHash(int tick, ulong hash)
        {
            if (!_isReplaying)
                return;
                
            _replayHashes[tick] = hash;
            
            // Find expected hash from original session
            ulong? expectedHash = null;
            foreach (var record in _sessionToReplay.tickHashes)
            {
                if (record.tick == tick)
                {
                    expectedHash = record.hash;
                    break;
                }
            }
            
            if (expectedHash.HasValue && hash != expectedHash.Value)
            {
                _result.matchesOriginal = false;
                _result.mismatchedTicks.Add(new TickMismatch
                {
                    tick = tick,
                    expectedHash = expectedHash.Value,
                    actualHash = hash
                });
                
                if (_result.firstMismatchTick == 0)
                {
                    _result.firstMismatchTick = tick;
                }
                
                OnHashMismatch?.Invoke(tick, expectedHash.Value, hash);
            }
            
            _currentReplayTick = tick;
        }

        /// <summary>
        /// Called when replay is complete.
        /// </summary>
        public void OnReplayTicksComplete()
        {
            if (!_isReplaying)
                return;
                
            _isReplaying = false;
            _result.isComplete = true;
            _result.totalTicksReplayed = _replayHashes.Count;
            
            // Compute final state hash
            if (_world != null && _world.IsCreated)
            {
                using (var snapshot = new WorldStateSnapshot())
                {
                    snapshot.Capture(_world);
                    _result.replayFinalStateHash = snapshot.ComputeHash();
                }
            }
            
            if (_result.matchesOriginal)
            {
                Debug.Log($"[SessionReplayer] REPLAY MATCHES ORIGINAL - {_result.totalTicksReplayed} ticks validated.");
            }
            else
            {
                Debug.LogError($"[SessionReplayer] REPLAY MISMATCH - {_result.mismatchedTicks.Count} ticks differ, first at tick {_result.firstMismatchTick}.");
            }
            
            OnReplayComplete?.Invoke(_result);
        }

        private void UpdateValidationSettings(uint randomSeed)
        {
            if (_world == null || !_world.IsCreated)
                return;
                
            var entityManager = _world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
            
            if (!query.IsEmpty)
            {
                var entity = query.GetSingletonEntity();
                var settings = entityManager.GetComponentData<DeterminismValidationSettings>(entity);
                settings.randomSeed = randomSeed;
                settings.validationState = ValidationState.Running;
                settings.ticksToSimulate = _sessionToReplay.tickHashes.Count;
                entityManager.SetComponentData(entity, settings);
            }
        }

        private void Update()
        {
            if (!_isReplaying || _world == null || !_world.IsCreated)
                return;
                
            // Check if replay is complete
            var entityManager = _world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
            
            if (query.IsEmpty)
                return;
                
            var simTime = query.GetSingleton<DeterministicSimulationTime>();
            
            if (simTime.currentTick >= _sessionToReplay.tickHashes.Count)
            {
                OnReplayTicksComplete();
            }
        }

        /// <summary>
        /// Whether currently replaying.
        /// </summary>
        public bool IsReplaying => _isReplaying;
        
        /// <summary>
        /// Current replay tick.
        /// </summary>
        public int CurrentTick => _currentReplayTick;

        /// <summary>
        /// Compare two recorded sessions.
        /// </summary>
        public static SessionComparisonResult CompareSessions(RecordedSession session1, RecordedSession session2)
        {
            var result = new SessionComparisonResult
            {
                session1Metadata = session1.metadata,
                session2Metadata = session2.metadata,
                matchingTicks = 0,
                mismatchedTicks = new List<TickMismatch>()
            };
            
            // Build lookup for session2
            var session2Hashes = new Dictionary<int, ulong>();
            foreach (var record in session2.tickHashes)
            {
                session2Hashes[record.tick] = record.hash;
            }
            
            // Compare
            foreach (var record1 in session1.tickHashes)
            {
                if (session2Hashes.TryGetValue(record1.tick, out var hash2))
                {
                    if (record1.hash == hash2)
                    {
                        result.matchingTicks++;
                    }
                    else
                    {
                        result.mismatchedTicks.Add(new TickMismatch
                        {
                            tick = record1.tick,
                            expectedHash = record1.hash,
                            actualHash = hash2
                        });
                    }
                }
            }
            
            result.areDeterministic = result.mismatchedTicks.Count == 0;
            
            return result;
        }
    }
    
    /// <summary>
    /// Result of replay validation.
    /// </summary>
    [Serializable]
    public struct ReplayValidationResult
    {
        public bool isComplete;
        public bool matchesOriginal;
        public int firstMismatchTick;
        public int totalTicksReplayed;
        public ulong replayFinalStateHash;
        public RecordedSession originalSession;
        public List<TickMismatch> mismatchedTicks;
    }
    
    /// <summary>
    /// Information about a tick where hashes mismatched.
    /// </summary>
    [Serializable]
    public struct TickMismatch
    {
        public int tick;
        public ulong expectedHash;
        public ulong actualHash;
    }
    
    /// <summary>
    /// Result of comparing two sessions.
    /// </summary>
    [Serializable]
    public struct SessionComparisonResult
    {
        public SessionMetadata session1Metadata;
        public SessionMetadata session2Metadata;
        public bool areDeterministic;
        public int matchingTicks;
        public List<TickMismatch> mismatchedTicks;
    }
}

