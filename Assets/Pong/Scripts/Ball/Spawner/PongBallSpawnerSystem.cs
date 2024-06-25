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
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongBallSpawnerSystem : SystemBase
    {
        private uint randomSeedFromServer;
        private Random random;

        private bool specialBoolforLocalClient;
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
            RequireForUpdate<PongInputs>();
            RequireForUpdate<DeterministicTime>();
            
            specialBoolforLocalClient = false;
        }

        protected override void OnStartRunning()
        { randomSeedFromServer = SystemAPI.GetSingleton<DeterministicSettings>().randomSeed;
           random = new Random((int)randomSeedFromServer); // in theory it will allow users to predict the random numbers
        }

        protected override void OnUpdate()
        {
            if (GameSettings.Instance.GetTotalBallsSpawned() >= GameSettings.Instance.GetTotalBallsToSpawn())
            {
                if (World.Name == "ClientWorld1" && !specialBoolforLocalClient)
                {
                    specialBoolforLocalClient = true;
                }
                else return;
            }
            
            var prefab = SystemAPI.GetSingleton<PongBallSpawner>().Ball;
                
            var ball = EntityManager.Instantiate(prefab);
            EntityManager.SetComponentData(ball, new LocalTransform
            {
                Position = new float3(0, 0, 13),
                Scale = 0.2f,
                Rotation = quaternion.identity
            });
                    
            // Generate a random angle in degrees
            var directionChoice = random.Next(0, 2);

            var angleInDegrees = 0;
            if (directionChoice == 0) {
                // Generate an angle for the left direction
                var rangeChoice = random.Next(0, 2);
                angleInDegrees = rangeChoice == 0 ? random.Next(0, 70) : random.Next(110, 180);
            }
            else {
                // Generate an angle for the right direction
                var rangeChoice = random.Next(0, 2);
                angleInDegrees = rangeChoice == 0 ? random.Next(180, 250) : random.Next(290, 360);
            }


            // Convert the angle to radians
            var angleInRadians = angleInDegrees * Mathf.Deg2Rad;

            // Generate a direction vector from the angle
            var direction = new float3(Mathf.Cos(angleInRadians), Mathf.Sin(angleInRadians), 0);
            direction = math.normalize(direction);

            // Generate a random speed
            var speed = random.Next(GameSettings.Instance.GetMinBallSpeed(), GameSettings.Instance.GetMaxBallSpeed());

            // Set the velocity of the ball
            EntityManager.SetComponentData(ball, new Velocity { value = direction * speed });
                    
            if (World.Name == "ClientWorld") // To prevent local simulation for counting points twice (from both worlds)
            {
                GameSettings.Instance.AddSpawnedBalls(1);
            }
        }
    }
}