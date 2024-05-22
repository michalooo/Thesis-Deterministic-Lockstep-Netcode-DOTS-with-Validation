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
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    // [UpdateAfter(typeof(BallMovementSystem))]
    [BurstCompile]
    public partial struct BallBounceSystem : ISystem
    {
        private const float MinZ = -5f;
        private const float MaxZ = 5f;
        
        private EntityQuery ballsQuery;
        private EntityQuery playerQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Velocity> ballVelocities;
        private NativeArray<Entity> ballEntities;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }

        public void OnUpdate(ref SystemState state)
        {
            playerQuery = SystemAPI.QueryBuilder().WithAll<CommandTarget>().Build();
            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballVelocities = ballsQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var commandTargetData = playerQuery.ToComponentDataArray<CommandTarget>(Allocator.TempJob);
            var playersTransforms = new NativeArray<LocalToWorld>(commandTargetData.Length, Allocator.TempJob);

            for (int i = 0; i < commandTargetData.Length; i++)
            {
                playersTransforms[i] = SystemAPI.GetComponent<LocalToWorld>(commandTargetData[i].connectionCommandsTargetEntity);
            }
            
            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();
            
            var ballBounceJob = new BallBounceJob
            {
                ECB = ecb,
                ballVelocities = ballVelocities,
                localTransform = ballTransform,
                Entities = ballEntities,
                minZPos = MinZ,
                maxZPos = MaxZ,
                players = playersTransforms,
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
        
        public NativeArray<LocalToWorld> players;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Velocity velocity = ballVelocities[index];
            Entity entity = Entities[index];
            
            const float playerBoundaryOffsetX = 0.175f;
            const float playerBoundaryOffsetZ = 1.75f;
            
            var newVelocityValue = new float3(velocity.value);
            
            if (transform.Position.z < minZPos)
            {
                // Check if the velocity is in the direction of the wall
                if (velocity.value.z < 0)
                {
                    // Reflect the velocity about the normal vector of the wall
                    newVelocityValue = math.reflect(velocity.value, new float3(0, 0, 1));
                }
            }
            else if (transform.Position.z > maxZPos)
            {
                // Check if the velocity is in the direction of the wall
                if (velocity.value.z > 0)
                {
                    // Reflect the velocity about the normal vector of the wall
                    newVelocityValue = math.reflect(velocity.value, new float3(0, 0, -1));
                }
            }

            foreach (var player in players)
            {
                if (newVelocityValue.x < 0 && transform.Position.x < 0 && // Check if the velocity is in the direction of the player
                    transform.Position.x <= player.Position.x + playerBoundaryOffsetX && // Check if the ball is within the player's right boundary
                    transform.Position.x >= player.Position.x - playerBoundaryOffsetX) // Check if the ball is within the player's left boundary
                {
                    if(transform.Position.z <= player.Position.z + playerBoundaryOffsetZ && // Check if the ball is within the player's top boundary
                       transform.Position.z >= player.Position.z - playerBoundaryOffsetZ) // Check if the ball is within the player's bottom boundary
                    {
                        // Reflect the velocity about the normal vector of the left player
                        newVelocityValue = math.reflect(newVelocityValue, new float3(1, 0, 0));
                    }
                }
                else if (newVelocityValue.x > 0 && transform.Position.x > 0 && // Check if the velocity is in the direction of the player
                    transform.Position.x >= player.Position.x - playerBoundaryOffsetX && // Check if the ball is within the player's right boundary
                    transform.Position.x <= player.Position.x + playerBoundaryOffsetX) // Check if the ball is within the player's left boundary
                {
                    if(transform.Position.z <= player.Position.z + playerBoundaryOffsetZ && // Check if the ball is within the player's top boundary
                       transform.Position.z >= player.Position.z - playerBoundaryOffsetZ) // Check if the ball is within the player's bottom boundary
                    {
                        // Reflect the velocity about the normal vector of the left player
                        newVelocityValue = math.reflect(newVelocityValue, new float3(-1, 0, 0));
                    }
                }
            }

            var newVelocity = new Velocity
            {
                value = newVelocityValue
            };
           
            ECB.SetComponent(index , entity, newVelocity);
        }
    }
}