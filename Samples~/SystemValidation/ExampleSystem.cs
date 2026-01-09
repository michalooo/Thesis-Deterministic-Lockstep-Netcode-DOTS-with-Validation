using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DeterministicLockstep.Samples
{
    /// <summary>
    /// Example component for the sample system.
    /// </summary>
    public struct Velocity : IComponentData
    {
        public float3 Value;
    }
    
    /// <summary>
    /// Example deterministic system that moves entities.
    /// This system is deterministic because:
    /// - Uses fixed delta time (from simulation)
    /// - No random operations
    /// - No unordered collections
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct ExampleMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            foreach (var (transform, velocity) in 
                SystemAPI.Query<RefRW<LocalTransform>, RefRO<Velocity>>())
            {
                transform.ValueRW.Position += velocity.ValueRO.Value * deltaTime;
            }
        }
    }
    
    /// <summary>
    /// Example NONDETERMINISTIC system for comparison.
    /// DO NOT USE THIS IN PRODUCTION - it demonstrates what NOT to do.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct ExampleNondeterministicSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // BAD: Using System.Random without seed
            var random = new System.Random();
            
            foreach (var transform in SystemAPI.Query<RefRW<LocalTransform>>())
            {
                // BAD: Random movement will be different each run
                transform.ValueRW.Position.x += (float)random.NextDouble();
            }
        }
    }
}

