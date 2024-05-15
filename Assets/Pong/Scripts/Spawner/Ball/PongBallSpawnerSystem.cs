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
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
            RequireForUpdate<PongInputs>();
        }

        protected override void OnUpdate()
        {
            var prefab = SystemAPI.GetSingleton<PongBallSpawner>().Ball;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            
            // I want to spawn a ball every x seconds.
            // I need a system for controlling ball speed etc
            
            for(int i=0; i<1; i++)
            {
                Debug.Log($"Spawning ball");
                // var ball = EntityManager.Instantiate(prefab);
                // EntityManager.SetComponentData(ball,LocalTransform.FromPosition(new float3(0, 0, 0)));
            }
        }
    }
}