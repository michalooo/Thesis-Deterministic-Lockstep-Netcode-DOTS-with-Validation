using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System that is responsible for bouncing the balls off the walls and players.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [UpdateAfter(typeof(PongBallDestructionSystem))]
    public partial class PongBallBounceSystem : SystemBase
    {
        private EntityQuery _ballsQuery;
        private EntityQuery _playersQuery;
        private NativeArray<LocalTransform> _ballsTransforms;
        private NativeArray<BallVelocity> _ballsVelocities;
        private NativeArray<Entity> _ballsEntities;
        
        private uint _randomSeedGeneratedOnServer;
        private Unity.Mathematics.Random _random;
        private NativeArray<GhostOwner> _ghostOwnerData;
        private NativeArray<LocalToWorld> _playersTransforms;
        
        protected override void OnStartRunning()
        { 
            _randomSeedGeneratedOnServer = SystemAPI.GetSingleton<DeterministicSettings>().randomSeed;
            _random = new Unity.Mathematics.Random(_randomSeedGeneratedOnServer);
        }
        
        protected override void OnUpdate()
        {
            _playersQuery = SystemAPI.QueryBuilder().WithAll<GhostOwner>().Build();
            _ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, BallVelocity>().Build();
            
            _ballsTransforms = _ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            _ballsVelocities = _ballsQuery.ToComponentDataArray<BallVelocity>(Allocator.TempJob);
            _ballsEntities = _ballsQuery.ToEntityArray(Allocator.TempJob);
            
            _ghostOwnerData = _playersQuery.ToComponentDataArray<GhostOwner>(Allocator.TempJob);
            _playersTransforms = new NativeArray<LocalToWorld>(_ghostOwnerData.Length, Allocator.TempJob);

            for (var i = 0; i < _ghostOwnerData.Length; i++)
            {
                _playersTransforms[i] = SystemAPI.GetComponent<LocalToWorld>(_ghostOwnerData[i].connectionCommandsTargetEntity);
            }
            
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            var mainCamera = Camera.main;
            float screenWidth = Screen.width;
            if (mainCamera != null)
            {
                var worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenWidth, 0, mainCamera.nearClipPlane));
            
                var ballBounceJob = new BallBounceJob
                {
                    ecb = ecb.AsParallelWriter(),
                    ballsVelocities = _ballsVelocities,
                    ballsTransforms = _ballsTransforms,
                    ballsEntities = _ballsEntities,
                    worldPosition = worldPosition,
                    bottomScreenPosition = GameSettings.Instance.BottomScreenPosition,
                    topScreenPosition = GameSettings.Instance.TopScreenPosition,
                    playersTransforms = _playersTransforms,
                    random = _random
                };
            
                JobHandle ballBounceHandle = ballBounceJob.Schedule(_ballsTransforms.Length,1);
                ballBounceHandle.Complete();
            }

            ecb.Playback(EntityManager);
            
            ecb.Dispose();
            _ghostOwnerData.Dispose();
            _playersTransforms.Dispose();
            _ballsTransforms.Dispose();
            _ballsVelocities.Dispose();
            _ballsEntities.Dispose();
        }
    }
    
    /// <summary>
    /// Parallel job that calculates new ball velocity if the ball bounces off the walls or players.
    /// </summary>
    public struct BallBounceJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        
        public NativeArray<Entity> ballsEntities;
        public NativeArray<LocalTransform> ballsTransforms;
        public NativeArray<BallVelocity> ballsVelocities;

        public Vector3 worldPosition;
        public float bottomScreenPosition;
        public float topScreenPosition;
        
        public Unity.Mathematics.Random random;
        
        public NativeArray<LocalToWorld> playersTransforms;
    
        public void Execute(int index)
        {
            var ballTransform = ballsTransforms[index];
            var ballVelocity = ballsVelocities[index];
            var ballEntity = ballsEntities[index];
            
            const float playerPrefabBoundaryOffsetX = 0.1f; // TODO This value should not be hardcoded
            const float playerPrefabBoundaryOffsetY = 1f; // TODO This value should not be hardcoded

            if (ballTransform.Position.x < -worldPosition.x || ballTransform.Position.x > worldPosition.x) return; // Ball is already outside of the screen (crossed left or right wall)
            
            var newBallVelocityValue = new float3(ballVelocity.value);
            
            if (ballTransform.Position.y < bottomScreenPosition) // Ball crossed bottom wall
            {
                // Check if the velocity is in the direction of the wall (there may be a case that the ball bounced back already but is still in the wall)
                if (ballVelocity.value.y < 0)
                {
                    // Reflect the velocity about the normal vector of the wall
                    newBallVelocityValue = math.reflect(ballVelocity.value, new float3(0, 1, 0));
                }
            }
            else if (ballTransform.Position.y > topScreenPosition) // Ball crossed top wall
            {
                // Check if the velocity is in the direction of the wall
                if (ballVelocity.value.y > 0)
                {
                    // Reflect the velocity about the normal vector of the wall
                    newBallVelocityValue = math.reflect(ballVelocity.value, new float3(0, -1, 0));
                }
            }
            
            foreach (var player in playersTransforms) // check if ball touched any of players pods. TODO: This should be optimized
            {
                if (math.distance(ballTransform.Position.x, player.Position.x) <= playerPrefabBoundaryOffsetX && 
                    math.distance(ballTransform.Position.y, player.Position.y) <= playerPrefabBoundaryOffsetY) // Check if the ball is within the player boundary
                {
                    if(ballVelocity.value.x < 0 && player.Position.x < 0) newBallVelocityValue = math.reflect(newBallVelocityValue, new float3(1, 0, 0)); // Left player front part
                    else if(ballVelocity.value.x > 0 && player.Position.x > 0) newBallVelocityValue = math.reflect(newBallVelocityValue, new float3(-1, 0, 0)); // Right player front part
                }
            }
            
            ecb.SetComponent(index, ballEntity, new BallVelocity { value = newBallVelocityValue });
        }
    }
}