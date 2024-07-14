using DeterministicLockstep;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System that destroys the ball when it goes out of the screen and counts the points for the players.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct PongBallDestructionSystem : ISystem
    {
        private EntityQuery ballsQuery;
        private NativeArray<LocalTransform> ballTransform;
        private NativeArray<Entity> ballEntities;
        
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PongBallSpawner>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, Velocity>().Build();
            ballTransform = ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            ballEntities = ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var leftPointsQueue = new NativeQueue<int>(Allocator.TempJob);
            var rightPointsQueue = new NativeQueue<int>(Allocator.TempJob);
            
            Camera cam = Camera.main;
            float targetXPosition = Screen.width;
            Vector3 worldPosition = cam.ScreenToWorldPoint(new Vector3(targetXPosition, 0, cam.nearClipPlane));
           
            var ballDestructionJob = new BallDestructionJob
            {
                ECB = ecb.AsParallelWriter(),
                localTransform = ballTransform,
                ballEntities = ballEntities,
                worldPosition = worldPosition,
                leftCounter = leftPointsQueue.AsParallelWriter(),
                rightCounter = rightPointsQueue.AsParallelWriter()
            };
            
            JobHandle ballDestructionHandle = ballDestructionJob.Schedule(ballTransform.Length,1);
            ballDestructionHandle.Complete();
            ecb.Playback(state.EntityManager);

            if (state.World.Name == "ClientWorld" && (rightPointsQueue.Count != 0 || leftPointsQueue.Count != 0)) // To prevent local simulation for counting points twice (from both worlds)
            {
                GameManagerSingleton.Instance.AddRightScore(rightPointsQueue.Count);
                GameManagerSingleton.Instance.AddLeftScore(leftPointsQueue.Count);
            }

            if (rightPointsQueue.Count != 0 || leftPointsQueue.Count != 0)
            {
                if (GameManagerSingleton.Instance.GetTotalScore() == GameSettings.Instance.GetTotalBallsToSpawn())
                {
                    GameManagerSingleton.Instance.SetGameResult();
                    var client = SystemAPI.GetSingletonRW<DeterministicClientComponent>();
                    client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.GameFinished;
                }
            }
            
            ecb.Dispose();
            rightPointsQueue.Dispose();
            leftPointsQueue.Dispose();
            ballTransform.Dispose();
            ballEntities.Dispose();
        }
    }
    
    /// <summary>
    /// Job that destroys the ball when it goes out of the screen and counts the points for the players.
    /// This job runs on per ball basis.
    /// </summary>
    [BurstCompile]
    public struct BallDestructionJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ECB;
        
        public NativeArray<Entity> ballEntities;
        public NativeArray<LocalTransform> localTransform;

        public Vector3 worldPosition;
        public NativeQueue<int>.ParallelWriter leftCounter;
        public NativeQueue<int>.ParallelWriter rightCounter;
    
        public void Execute(int index)
        {
            LocalTransform transform = localTransform[index];
            Entity entity = ballEntities[index];

            if (transform.Position.x < -worldPosition.x)
            {
                rightCounter.Enqueue(1);
                ECB.DestroyEntity(index, entity);
            }
            else if (transform.Position.x > worldPosition.x)
            {
                leftCounter.Enqueue(1);
                ECB.DestroyEntity(index, entity);
            }
        }
    }
}