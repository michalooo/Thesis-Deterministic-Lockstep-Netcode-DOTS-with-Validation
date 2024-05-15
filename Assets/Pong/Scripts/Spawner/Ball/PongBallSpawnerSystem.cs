using Unity.Collections;
using Unity.Entities;
using Unity.Networking.Transport;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
using Unity.Mathematics;

namespace PongGame
{
    /// <summary>
    /// System used to spawn the player prefab for the connections that are not spawned yet
    /// </summary>
    [UpdateBefore(typeof(DeterministicSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongBallSpawnerSystem : SystemBase
    {
        private int totalBallsSpawned = 0;
        private int totalBallsToSpawn = 500;
        
        private float timeBetweenSpawning = 0.05f;
        private float timeSinceLastSpawn = 0f;
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
            RequireForUpdate<PongInputs>();
        }

        protected override void OnUpdate()
        {
            if (totalBallsSpawned < totalBallsToSpawn)
            {
                var prefab = SystemAPI.GetSingleton<PongBallSpawner>().Ball;
                var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
                
                if (timeSinceLastSpawn >= timeBetweenSpawning)
                {
                    Debug.Log($"Spawning ball: " + totalBallsSpawned);
                    var ball = commandBuffer.Instantiate(prefab);
                    commandBuffer.SetComponent(ball, new LocalTransform
                    {
                        Position = new float3(0, 0, 0),
                        Scale = 0.4f,
                        Rotation = quaternion.identity
                    });
                    totalBallsSpawned++;
                    timeSinceLastSpawn = 0f;
                }
                else
                {
                    timeSinceLastSpawn += SystemAPI.Time.DeltaTime;
                }

                commandBuffer.Playback(EntityManager);
            }
        }
    }
}