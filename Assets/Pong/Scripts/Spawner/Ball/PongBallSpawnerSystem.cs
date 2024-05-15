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
    [UpdateInGroup(typeof(GameStateUpdateSystemGroup))]
    [UpdateAfter(typeof(PongPlayerSpawnerSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongBallSpawnerSystem : SystemBase
    {
        private int totalBallsSpawned = 0;
        private int totalBallsToSpawn = 600;
        
        private float minSpeed = 0.5f;
        private float maxSpeed = 5f;
        
        private float timeBetweenSpawning = 0.01f;
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
                    var ball = commandBuffer.Instantiate(prefab);
                    commandBuffer.SetComponent(ball, new LocalTransform
                    {
                        Position = new float3(0, 0, 0),
                        Scale = 0.4f,
                        Rotation = quaternion.identity
                    });
                    
                    // Generate a random angle in degrees
                    var angleInDegrees = UnityEngine.Random.Range(0f, 360f);

                    // Convert the angle to radians
                    var angleInRadians = angleInDegrees * Mathf.Deg2Rad;

                    // Generate a direction vector from the angle
                    var direction = new float3(Mathf.Cos(angleInRadians), 0, Mathf.Sin(angleInRadians));
                    direction = math.normalize(direction);

                    // Generate a random speed
                    var speed = UnityEngine.Random.Range(minSpeed, maxSpeed);

                    // Set the velocity of the ball
                    commandBuffer.SetComponent(ball, new Velocity { value = direction * speed });
                    
                    totalBallsSpawned++;
                    timeSinceLastSpawn = 0f;
                }
                else
                {
                    timeSinceLastSpawn += SystemAPI.Time.DeltaTime;
                }

                commandBuffer.Playback(EntityManager);
                commandBuffer.Dispose();
            }
        }
    }
}