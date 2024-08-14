using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System that moves the ball in the game world.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [UpdateAfter(typeof(PongBallBounceSystem))]
    public partial struct BallMovementSystem : ISystem
    {
        private EntityQuery _ballsQuery;
        private NativeArray<LocalTransform> _ballsTransform;
        private NativeArray<BallVelocity> _ballsVelocity;
        private NativeArray<Entity> _ballsEntity;
        
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            _ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, BallVelocity>().Build();
            _ballsTransform = _ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            _ballsVelocity = _ballsQuery.ToComponentDataArray<BallVelocity>(Allocator.TempJob);
            _ballsEntity = _ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            var mainCamera = Camera.main;
            float screenWidth = Screen.width;
            var worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenWidth, 0, mainCamera.nearClipPlane));
            
            var ballMovementJob = new BallMovementJob
            {
                ecb = ecb.AsParallelWriter(),
                ballsVelocity = _ballsVelocity,
                ballsTransform = _ballsTransform,
                ballsEntity = _ballsEntity,
                worldPosition = worldPosition,
                deltaTime = deltaTime,
                interpolationSpeed = 0.2f
            };
            
            var ballMovementJobHandle = ballMovementJob.Schedule(_ballsTransform.Length,1);
            ballMovementJobHandle.Complete();
            ecb.Playback(state.EntityManager);
            
            ecb.Dispose();
            _ballsTransform.Dispose();
            _ballsVelocity.Dispose();
            _ballsEntity.Dispose();
        }
    }
    
    /// <summary>
    /// Job that moves the ball in the game world on per ball basis.
    /// </summary>
    public struct BallMovementJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        
        public NativeArray<Entity> ballsEntity;
        public NativeArray<LocalTransform> ballsTransform;
        public NativeArray<BallVelocity> ballsVelocity;
        
        public Vector3 worldPosition;
        public float deltaTime;
        public float interpolationSpeed;
        
        public void Execute(int index)
        {
            var ballTransform = ballsTransform[index];
            var ballVelocity = ballsVelocity[index];
            var ballEntity = ballsEntity[index];
            
            if (ballTransform.Position.x < -worldPosition.x || ballTransform.Position.x > worldPosition.x) return; // ball is already outside the border
            
            var newPosition = ballTransform.Position + deltaTime * ballVelocity.value;
          
            var interpolatedPositionX = Mathf.Lerp(ballTransform.Position.x, newPosition.x, interpolationSpeed * deltaTime); // Do I need deltaTime here?
            var interpolatedPositionY = Mathf.Lerp(ballTransform.Position.y, newPosition.y, interpolationSpeed * deltaTime);
            var interpolatedPositionZ = Mathf.Lerp(ballTransform.Position.z, newPosition.z, interpolationSpeed * deltaTime);
            var interpolatedPosition = new float3(interpolatedPositionX, interpolatedPositionY, interpolatedPositionZ);
            
            var newTransform = new LocalTransform
            {
                Position = interpolatedPosition,
                Rotation = ballTransform.Rotation,
                Scale = ballTransform.Scale
            };
        
            ecb.SetComponent(index , ballEntity, newTransform);
        }
    }
}