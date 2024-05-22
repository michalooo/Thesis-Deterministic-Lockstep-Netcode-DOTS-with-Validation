using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
using Unity.Mathematics;
using Random = System.Random;

namespace PongGame
{
    /// <summary>
    /// System used to spawn the player prefab for the connections that are not spawned yet
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongBallSpawnerSystem : SystemBase
    {
        private int totalBallsSpawned = 0;
        private int totalBallsToSpawn = 600;
        
        private int minSpeed = 10;
        private int maxSpeed = 50;
        
        private float timeBetweenSpawning = 0.005f;
        private float timeSinceLastSpawn = 0f;

        private uint randomSeedFromServer;
        private Random random;
        
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
            RequireForUpdate<PongInputs>();
            RequireForUpdate<DeterministicTime>();
        }

        protected override void OnStartRunning()
        {
            randomSeedFromServer = SystemAPI.GetSingleton<DeterministicClientComponent>().randomSeed;
            random = new Random((int)randomSeedFromServer); // in theory it will allow users to predict the random numbers
        }

        protected override void OnUpdate()
        {
            if (totalBallsSpawned < totalBallsToSpawn)
            {
                var prefab = SystemAPI.GetSingleton<PongBallSpawner>().Ball;
                
                if (timeSinceLastSpawn >= timeBetweenSpawning)
                {
                    var ball = EntityManager.Instantiate(prefab);
                    EntityManager.SetComponentData(ball, new LocalTransform
                    {
                        Position = new float3(0, 0, 0),
                        Scale = 0.4f,
                        Rotation = quaternion.identity
                    });
                    
                    // Generate a random angle in degrees
                    var angleInDegrees = random.Next(0, 360);

                    // Convert the angle to radians
                    var angleInRadians = angleInDegrees * Mathf.Deg2Rad;

                    // Generate a direction vector from the angle
                    var direction = new float3(Mathf.Cos(angleInRadians), 0, Mathf.Sin(angleInRadians));
                    direction = math.normalize(direction);

                    // Generate a random speed
                    var speed = random.Next(minSpeed, maxSpeed);

                    // Set the velocity of the ball
                    EntityManager.SetComponentData(ball, new Velocity { value = direction * speed });
                    
                    totalBallsSpawned++;
                    timeSinceLastSpawn = 0f;
                }
                else
                {
                    timeSinceLastSpawn += SystemAPI.Time.DeltaTime;
                }
            }
        }
    }
}