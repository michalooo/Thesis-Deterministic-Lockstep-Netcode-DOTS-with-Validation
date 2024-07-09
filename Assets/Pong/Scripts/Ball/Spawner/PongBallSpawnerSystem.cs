using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using DeterministicLockstep;
using Unity.Mathematics;
using Random = System.Random;

namespace PongGame
{
    /// <summary>
    /// System used to spawn balls in the game.
    /// It will spawn one ball at a time with a random direction and speed.
    /// The balls will be spawned in the middle of the screen every set amount of time.
    /// </summary>
    [UpdateInGroup(typeof(DeterministicSimulationSystemGroup), OrderFirst = true)]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class PongBallSpawnerSystem : SystemBase
    {
        /// <summary>
        /// Seed for generating random numbers.
        /// </summary>
        private uint randomSeedFromServer;
        private Random random;

        /// <summary>
        /// Hack of allowing the second local client to spawn the last ball.
        /// The problem arises from the fact that both clients are sharing the same state so the actions are executed twice.
        /// </summary>
        private bool boolForSecondLocalClientForSpawningLastBall;
        
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
            RequireForUpdate<PongInputs>();
            RequireForUpdate<DeterministicSimulationTime>();
            
            boolForSecondLocalClientForSpawningLastBall = false;
        }

        protected override void OnStartRunning()
        { 
            randomSeedFromServer = SystemAPI.GetSingleton<DeterministicSettings>().randomSeed;
           random = new Random((int)randomSeedFromServer); // in theory it will allow users to predict the random numbers
        }

        protected override void OnUpdate()
        {
            if (GameSettings.Instance.GetTotalBallsSpawned() >= GameSettings.Instance.GetTotalBallsToSpawn())
            {
                if (World.Name == "ClientWorld1" && !boolForSecondLocalClientForSpawningLastBall)
                {
                    boolForSecondLocalClientForSpawningLastBall = true;
                }
                else return;
            }
            
            var ballPrefab = SystemAPI.GetSingleton<PongBallSpawner>().Ball;
                
            var ballEntity = EntityManager.Instantiate(ballPrefab);
            EntityManager.AddComponentData(ballEntity, new DeterministicEntityID { ID = DeterministicLogger.Instance.GetDeterministicEntityID(World.Name) });
            EntityManager.SetComponentData(ballEntity, new LocalTransform
            {
                Position = new float3(0, 0, 13),
                Scale = 0.2f,
                Rotation = quaternion.identity
            });
            EntityManager.SetName(ballEntity, "Ball");
                    
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
            EntityManager.SetComponentData(ballEntity, new Velocity { value = direction * speed });
                    
            if (World.Name == "ClientWorld") // To prevent local simulation for counting points twice (from both worlds)
            {
                GameSettings.Instance.AddSpawnedBalls(1);
            }
        }
    }
}