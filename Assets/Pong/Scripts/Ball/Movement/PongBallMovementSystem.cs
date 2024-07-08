using DeterministicLockstep;
using Unity.Burst;
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
    [UpdateAfter(typeof(BallBounceSystem))]
    public partial struct BallMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }

        private EntityQuery ballsQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Velocity> ballVelocities;
        private NativeArray<Entity> ballEntities;
        
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            
            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballVelocities = ballsQuery.ToComponentDataArray<Velocity>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            Camera cam = Camera.main;
            float targetXPosition = Screen.width;
            Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(targetXPosition, 0, cam.nearClipPlane));
            
            var ballMovementJob = new BallMovementJob
            {
                ECB = ecb.AsParallelWriter(),
                ballVelocities = ballVelocities,
                localTransform = ballTransform,
                ballEntitiesToMove = ballEntities,
                worldPosition = worldPosition,
                deltaTime = deltaTime
            };
            
            JobHandle ballMovementHandle = ballMovementJob.Schedule(ballTransform.Length,1);
            ballMovementHandle.Complete();
            ecb.Playback(state.EntityManager);
            
            ecb.Dispose();
            ballTransform.Dispose();
            ballVelocities.Dispose();
            ballEntities.Dispose();
        }
    }
    
    /// <summary>
    /// Job that moves the ball in the game world on per ball basis.
    /// </summary>
    [BurstCompile]
    public struct BallMovementJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public NativeArray<Entity> ballEntitiesToMove;
        public NativeArray<LocalTransform> localTransform;
        public NativeArray<Velocity> ballVelocities;
        
        public Vector3 worldPosition;
        public float deltaTime;
        private float interpolationSpeed; // New field for interpolation speed
        
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Velocity velocity = ballVelocities[index];
            Entity entity = ballEntitiesToMove[index];
            interpolationSpeed = 0.2f;
            
            if (transform.Position.x < -worldPosition.x || transform.Position.x > worldPosition.x) return;
            
            var newPosition = transform.Position + deltaTime * velocity.value;
          
            var interpolatedPositionX = Mathf.Lerp(transform.Position.x, newPosition.x, interpolationSpeed * deltaTime);
            var interpolatedPositionY = Mathf.Lerp(transform.Position.y, newPosition.y, interpolationSpeed * deltaTime);
            var interpolatedPositionZ = Mathf.Lerp(transform.Position.z, newPosition.z, interpolationSpeed * deltaTime);
            var interpolatedPosition = new float3(interpolatedPositionX, interpolatedPositionY, interpolatedPositionZ);
            
            var newTransform = new LocalTransform
            {
                Position = interpolatedPosition,
                Rotation = transform.Rotation,
                Scale = transform.Scale
            };
        
            ECB.SetComponent(index , entity, newTransform);
        }
    }
}