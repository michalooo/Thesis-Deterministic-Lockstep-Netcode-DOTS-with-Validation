using DeterministicLockstep;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;
using Random = System.Random;

namespace PongGame
{
    /// <summary>
    /// System that bounces the ball off the walls and the players.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [UpdateAfter(typeof(PongBallDestructionSystem))]
    [BurstCompile]
    public partial class BallBounceSystem : SystemBase
    {
        private EntityQuery ballsQuery;
        private EntityQuery playerQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Velocity> ballVelocities;
        private NativeArray<Entity> ballEntities;
        
        /// <summary>
        /// Seed for generating random numbers.
        /// </summary>
        private uint randomSeedFromServer;
        private Unity.Mathematics.Random random;
        
        [BurstCompile]
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
        }
        
        protected override void OnStartRunning()
        { 
            randomSeedFromServer = SystemAPI.GetSingleton<DeterministicSettings>().randomSeed;
            random = new Unity.Mathematics.Random(randomSeedFromServer);
        }
        
        protected override void OnUpdate()
        {
            playerQuery = SystemAPI.QueryBuilder().WithAll<GhostOwner>().Build();
            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballVelocities = ballsQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var ghostOwnerData = playerQuery.ToComponentDataArray<GhostOwner>(Allocator.TempJob);
            var playersTransforms = new NativeArray<LocalToWorld>(ghostOwnerData.Length, Allocator.TempJob);

            for (int i = 0; i < ghostOwnerData.Length; i++)
            {
                playersTransforms[i] = SystemAPI.GetComponent<LocalToWorld>(ghostOwnerData[i].connectionCommandsTargetEntity);
            }
            
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            Camera cam = Camera.main;
            float targetXPosition = Screen.width;
            Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(targetXPosition, 0, cam.nearClipPlane));
            
            var ballBounceJob = new BallBounceJob
            {
                ECB = ecb.AsParallelWriter(),
                ballVelocities = ballVelocities,
                localTransform = ballTransform,
                ballEntities = ballEntities,
                worldPosition = worldPosition,
                minYPos = GameSettings.Instance.BottomScreenPosition,
                maxYPos = GameSettings.Instance.TopScreenPosition,
                players = playersTransforms,
                random = random
            };
            
            JobHandle ballBounceHandle = ballBounceJob.Schedule(ballTransform.Length,1);
            ballBounceHandle.Complete();
            ecb.Playback(EntityManager);
            
            ecb.Dispose();
            ballTransform.Dispose();
            ballVelocities.Dispose();
            ballEntities.Dispose();
        }
    }
    
    /// <summary>
    /// Parallel job that bounces the ball off the walls and the players.
    /// </summary>
    [BurstCompile]
    public struct BallBounceJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public NativeArray<Entity> ballEntities;
        public NativeArray<LocalTransform> localTransform;
        public NativeArray<Velocity> ballVelocities;

        public Vector3 worldPosition;
        public float minYPos;
        public float maxYPos;
        
        public Unity.Mathematics.Random random;
        
        public NativeArray<LocalToWorld> players;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Velocity velocity = ballVelocities[index];
            Entity entity = ballEntities[index];
            
            const float playerBoundaryOffsetX = 0.1f;
            const float playerBoundaryOffsetY = 1f;

            if (transform.Position.x < -worldPosition.x || transform.Position.x > worldPosition.x) return;
            
            var newVelocityValue = new float3(velocity.value);
            
            if (transform.Position.y < minYPos)
            {
                // Check if the velocity is in the direction of the wall
                if (velocity.value.y < 0)
                {
                    // Reflect the velocity about the normal vector of the wall
                    // newVelocityValue = math.reflect(velocity.value, new float3(0, random.NextFloat(0.5f, 1f), 0));
                    newVelocityValue = math.reflect(velocity.value, new float3(0, 1, 0));
                }
            }
            else if (transform.Position.y > maxYPos)
            {
                // Check if the velocity is in the direction of the wall
                if (velocity.value.y > 0)
                {
                    // Reflect the velocity about the normal vector of the wall
                    // newVelocityValue = math.reflect(velocity.value, new float3(0, random.NextFloat(-0.5f, -1f), 0));
                    newVelocityValue = math.reflect(velocity.value, new float3(0, -1, 0));
                }
            }

            foreach (var player in players)
            {
                if (newVelocityValue.x < 0 && transform.Position.x < 0 && // Check if the velocity is in the direction of the player
                    transform.Position.x <= player.Position.x + playerBoundaryOffsetX && // Check if the ball is within the player's right boundary
                    transform.Position.x >= player.Position.x - playerBoundaryOffsetX) // Check if the ball is within the player's left boundary
                {
                    if(transform.Position.y <= player.Position.y + playerBoundaryOffsetY && // Check if the ball is within the player's top boundary
                       transform.Position.y >= player.Position.y - playerBoundaryOffsetY) // Check if the ball is within the player's bottom boundary
                    {
                        // Reflect the velocity about the normal vector of the left player
                        newVelocityValue = math.reflect(newVelocityValue, new float3(1, 0, 0));
                    }
                }
                else if (newVelocityValue.x > 0 && transform.Position.x > 0 && // Check if the velocity is in the direction of the player
                    transform.Position.x >= player.Position.x - playerBoundaryOffsetX && // Check if the ball is within the player's right boundary
                    transform.Position.x <= player.Position.x + playerBoundaryOffsetX) // Check if the ball is within the player's left boundary
                {
                    if(transform.Position.y <= player.Position.y + playerBoundaryOffsetY && // Check if the ball is within the player's top boundary
                       transform.Position.y >= player.Position.y - playerBoundaryOffsetY) // Check if the ball is within the player's bottom boundary
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