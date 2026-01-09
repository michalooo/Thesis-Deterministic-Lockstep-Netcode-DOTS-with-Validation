using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Transforms;

namespace DeterministicLockstep
{
    /// <summary>
    /// System group that contains deterministic simulation systems.
    /// All systems that affect game state should be added to this group.
    /// Runs at a fixed tick rate for deterministic simulation.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation)]
    public partial class DeterministicSimulationSystemGroup : ComponentSystemGroup
    {
        /// <summary>
        /// Fixed delta time for each tick.
        /// </summary>
        private float _fixedDeltaTime = 1.0f / 60.0f;
        
        /// <summary>
        /// Maximum ticks to process per frame to prevent spiral of death.
        /// </summary>
        private const int MaxTicksPerFrame = 10;

        protected override void OnCreate()
        {
            base.OnCreate();
            RateManager = new ValidationRateManager(this);
            
            // Create buffer for tracking which component types to hash
            EntityManager.CreateSingletonBuffer<DeterministicComponent>();
            var deterministicComponentsBuffer = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            
            // Add default component types to track
            deterministicComponentsBuffer.Add(new DeterministicComponent
            {
                type = ComponentType.ReadOnly<LocalTransform>(),
            });
            deterministicComponentsBuffer.Add(new DeterministicComponent
            {
                type = ComponentType.ReadOnly<DeterministicEntityID>(),
            });
            
            // Create simulation time singleton
            EntityManager.CreateSingleton(new DeterministicSimulationTime
            {
                hashesForCurrentTick = new NativeList<ulong>(Allocator.Persistent),
                tickRate = 60,
                currentTick = 0,
                numTimesTickedThisFrame = 0,
                timeUntilNextTick = 0
            });
            
            // Create validation settings singleton
            EntityManager.CreateSingleton(new DeterminismValidationSettings
            {
                validationMode = DeterminismValidationMode.FullGame,
                hashComparisonMode = HashComparisonMode.PerTick,
                numberOfRuns = 2,
                currentRunIndex = 0,
                validationState = ValidationState.Idle,
                nondeterminismDetected = false,
                firstNondeterministicTick = -1,
                firstNondeterministicSystemIndex = -1,
                ticksToSimulate = 1000,
                randomSeed = 12345
            });
        }

        protected override void OnDestroy()
        {
            if (SystemAPI.TryGetSingletonRW<DeterministicSimulationTime>(out var simTime))
            {
                if (simTime.ValueRO.hashesForCurrentTick.IsCreated)
                {
                    simTime.ValueRW.hashesForCurrentTick.Dispose();
                }
            }
            base.OnDestroy();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.HasSingleton<DeterministicSimulationTime>())
                return;
                
            var simTime = SystemAPI.GetSingleton<DeterministicSimulationTime>();
            _fixedDeltaTime = 1.0f / simTime.tickRate;
            
            // Check if we need per-system hashing
            if (SystemAPI.HasSingleton<DeterministicSettings>())
            {
                var settings = SystemAPI.GetSingleton<DeterministicSettings>();
                if (settings.hashComparisonMode == HashComparisonMode.PerSystemPerTick)
                {
                    // Run with per-system hashing
                    while (RateManager.ShouldGroupUpdate(this))
                    {
                        UpdateAllSystemsWithHashing();
                    }
                    return;
                }
            }
            
            // Standard update
            base.OnUpdate();
        }

        /// <summary>
        /// Updates all systems in the group with hash computation after each system.
        /// Used for per-system validation to find which system causes nondeterminism.
        /// </summary>
        private void UpdateAllSystemsWithHashing()
        {
            var groupSystems = GetAllSystems();
            var hashSystem = World.GetExistingSystem<StateHashForValidationSystem>();
            
            if (hashSystem == SystemHandle.Null)
                return;
                
            // Compute initial hash (before any system runs)
            hashSystem.Update(World.Unmanaged);
            
            for (int i = 0; i < groupSystems.Length; i++)
            {
                var system = groupSystems[i];
                
                // Skip the hash system itself
                if (system == hashSystem)
                    continue;
                    
                try
                {
                    system.Update(World.Unmanaged);
                    
                    // Compute hash after this system
                    hashSystem.Update(World.Unmanaged);
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Error updating system at index {i}: {e.Message}");
                    throw;
                }
        
                if (World.QuitUpdate)
                    break;
            }
        }

        /// <summary>
        /// Rate manager for fixed-step deterministic simulation.
        /// </summary>
        public struct ValidationRateManager : IRateManager
        {
            private EntityQuery _simTimeQuery;
            private EntityQuery _settingsQuery;
            private EntityQuery _validationSettingsQuery;
            private float _fixedDeltaTime;

            public ValidationRateManager(ComponentSystemGroup group) : this()
            {
                _simTimeQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicSimulationTime));
                _settingsQuery = group.EntityManager.CreateEntityQuery(typeof(DeterministicSettings));
                _validationSettingsQuery = group.EntityManager.CreateEntityQuery(typeof(DeterminismValidationSettings));
                _fixedDeltaTime = 1.0f / 60.0f;
            }

            public bool ShouldGroupUpdate(ComponentSystemGroup group)
            {
                if (_simTimeQuery.IsEmpty)
                    return false;
                
                // Check validation state
                if (!_validationSettingsQuery.IsEmpty)
                {
                    var validationSettings = _validationSettingsQuery.GetSingleton<DeterminismValidationSettings>();
                    
                    // Only run if validation is active
                    if (validationSettings.validationState != ValidationState.Running)
                        return false;
                        
                    // Check if we've reached target tick count
                    var simTime = _simTimeQuery.GetSingleton<DeterministicSimulationTime>();
                    if (simTime.currentTick >= validationSettings.ticksToSimulate)
                        return false;
                }
                
                return ShouldTick(group);
            }
            
            private bool ShouldTick(ComponentSystemGroup group)
            {
                var simTime = _simTimeQuery.GetSingletonRW<DeterministicSimulationTime>();
                _fixedDeltaTime = 1.0f / simTime.ValueRO.tickRate;
                
                var deltaTime = (double)group.World.Time.DeltaTime;
                
                // Limit ticks per frame
                if (simTime.ValueRO.numTimesTickedThisFrame >= MaxTicksPerFrame)
                {
                    ResetFrameState(ref simTime.ValueRW, group);
                    return false;
                }
                
                // Accumulate time
                simTime.ValueRW.timeUntilNextTick -= deltaTime;
                
                // Check if it's time to tick
                if (simTime.ValueRO.timeUntilNextTick <= 0)
                {
                    simTime.ValueRW.currentTick++;
                    simTime.ValueRW.numTimesTickedThisFrame++;
                    simTime.ValueRW.timeUntilNextTick += _fixedDeltaTime;
                    
                    group.World.PushTime(new TimeData(_fixedDeltaTime, _fixedDeltaTime));
                    return true;
                }
                
                // Reset frame state if no tick
                ResetFrameState(ref simTime.ValueRW, group);
                return false;
            }
            
            private void ResetFrameState(ref DeterministicSimulationTime simTime, ComponentSystemGroup group)
            {
                // Pop any pushed time this frame
                for (int i = 0; i < simTime.numTimesTickedThisFrame; i++)
                {
                    group.World.PopTime();
                }
                simTime.numTimesTickedThisFrame = 0;
            }

            public float Timestep
            {
                get => _fixedDeltaTime;
                set => _fixedDeltaTime = value;
            }
        }
    }
}
