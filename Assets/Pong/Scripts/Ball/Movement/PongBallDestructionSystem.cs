using DeterministicLockstep;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// System that is responsible for destroying the ball when it goes out of the screen and counts the points for the players.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    public partial struct PongBallDestructionSystem : ISystem
    {
        private EntityQuery _ballsQuery;
        private NativeArray<LocalTransform> _ballsTransform;
        private NativeArray<Entity> _ballsEntity;

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.TempJob);

            _ballsQuery = SystemAPI.QueryBuilder().WithAll<LocalTransform, BallVelocity>().Build();
            _ballsTransform = _ballsQuery.ToComponentDataArray<LocalTransform>(Allocator.TempJob);
            _ballsEntity = _ballsQuery.ToEntityArray(Allocator.TempJob);
            
            var leftPointsCounterForCurrentTick = new NativeQueue<int>(Allocator.TempJob);
            var rightPointsCounterForCurrentTick = new NativeQueue<int>(Allocator.TempJob);
            
            var mainCamera = Camera.main;
            float screenWidth = Screen.width;
            if (mainCamera != null)
            {
                var worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenWidth, 0, mainCamera.nearClipPlane));
           
                var ballDestructionJob = new BallDestructionJob
                {
                    ecb = ecb.AsParallelWriter(),
                    ballsTransform = _ballsTransform,
                    ballsEntity = _ballsEntity,
                    worldPosition = worldPosition,
                    leftPointsCounterForCurrentTick = leftPointsCounterForCurrentTick.AsParallelWriter(),
                    rightPointsCounterForCurrentTick = rightPointsCounterForCurrentTick.AsParallelWriter()
                };
            
                var ballDestructionJobHandle = ballDestructionJob.Schedule(_ballsTransform.Length,1);
                ballDestructionJobHandle.Complete();
            }

            ecb.Playback(state.EntityManager);

            if (state.World.Name == "ClientWorld") // To prevent local simulation for counting points twice (from both worlds) since GameManagerSingleton will already affect both client worlds
            {
                GameManagerSingleton.Instance.AddRightScore(rightPointsCounterForCurrentTick.Count);
                GameManagerSingleton.Instance.AddLeftScore(leftPointsCounterForCurrentTick.Count);
            }

            if (GameManagerSingleton.Instance.GetTotalScore() == GameSettings.Instance.GetTotalBallsToSpawn())
            {
                GameManagerSingleton.Instance.SetGameResult();
                var client = SystemAPI.GetSingletonRW<DeterministicClientComponent>();
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.GameFinished;
            }
            
            _ballsTransform.Dispose();
            _ballsEntity.Dispose();
            leftPointsCounterForCurrentTick.Dispose();
            rightPointsCounterForCurrentTick.Dispose();
            ecb.Dispose();
        }
    }
    
    /// <summary>
    /// Parallel job that destroys the ball when it goes out of the screen (left or right) and adds the points for the players.
    /// </summary>
    public struct BallDestructionJob : IJobParallelFor
    {
        public EntityCommandBuffer.ParallelWriter ecb;
        
        public NativeArray<Entity> ballsEntity;
        public NativeArray<LocalTransform> ballsTransform;

        public Vector3 worldPosition;
        public NativeQueue<int>.ParallelWriter leftPointsCounterForCurrentTick;
        public NativeQueue<int>.ParallelWriter rightPointsCounterForCurrentTick;
    
        public void Execute(int index)
        {
            var ballTransform = ballsTransform[index];
            var ballEntity = ballsEntity[index];

            if (ballTransform.Position.x < -worldPosition.x)
            {
                rightPointsCounterForCurrentTick.Enqueue(1);
                ecb.DestroyEntity(index, ballEntity);
            }
            else if (ballTransform.Position.x > worldPosition.x)
            {
                leftPointsCounterForCurrentTick.Enqueue(1);
                ecb.DestroyEntity(index, ballEntity);
            }
        }
    }
}