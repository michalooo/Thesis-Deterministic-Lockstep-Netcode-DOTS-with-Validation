using DeterministicLockstep;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(GameStateUpdateSystemGroup))]
    [BurstCompile]
    public partial struct BallBounceSystem : ISystem
    {
        private const float MinZ = -5f;
        private const float MaxZ = 5f;
        
        private EntityQuery ballsQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Velocity> ballVelocities;
        private NativeArray<Entity> ballEntities;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }

        public void OnUpdate(ref SystemState state)
        {
            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballVelocities = ballsQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var ballBounceJob = new BallBounceJob
            {
                ECB = ecb,
                ballVelocities = ballVelocities,
                localTransform = ballTransform,
                Entities = ballEntities,
                minZPos = MinZ,
                maxZPos = MaxZ
            };
            
            JobHandle ballBounceHandle = ballBounceJob.Schedule(ballTransform.Length,1);
            ballBounceHandle.Complete();
            
            ballTransform.Dispose();
            ballVelocities.Dispose();
            ballEntities.Dispose();
        }
    }
    
    [BurstCompile]
    public struct BallBounceJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public NativeArray<Entity> Entities;
        public NativeArray<LocalTransform> localTransform;
        public NativeArray<Velocity> ballVelocities;

        public float minZPos;
        public float maxZPos;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Velocity velocity = ballVelocities[index];
            Entity entity = Entities[index];

            var newVelocityValue = velocity.value;
            var newPositionValue = transform.Position;
            if (transform.Position.z < minZPos + 0.01f)
            {
                // Reflect the velocity about the normal vector of the wall
                newVelocityValue = math.reflect(velocity.value, new float3(0, 0, 1));
                newPositionValue.z = minZPos + 0.1f; // Add a small offset to the new position
            }
            else if (transform.Position.z > maxZPos - 0.01f)
            {
                // Reflect the velocity about the normal vector of the wall
                newVelocityValue = math.reflect(velocity.value, new float3(0, 0, -1));
                newPositionValue.z = maxZPos - 0.1f; // Add a small offset to the new position
            }

            var newVelocity = new Velocity
            {
                value = newVelocityValue
            };
            var newTransform = new LocalTransform
            {
                Position = newPositionValue,
                Rotation = transform.Rotation,
                Scale = transform.Scale
            };

            ECB.SetComponent(index , entity, newTransform);
            ECB.SetComponent(index , entity, newVelocity);
        }
    }
}