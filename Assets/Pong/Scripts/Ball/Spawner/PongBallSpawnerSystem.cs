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
        /// Seed for generating random numbers received from the server.
        /// </summary>
        private uint _randomSeedFromServer;
        private Random _random;

        /// <summary>
        /// Hack of allowing the second local client to spawn the last ball.
        /// The problem arises from the fact that both clients are sharing the same state so the actions are executed twice.
        /// TODO get rid of those problems with second local client
        /// </summary>
        private bool _boolForSecondLocalClientForSpawningLastBall;
        
        protected override void OnCreate()
        {
            RequireForUpdate<PongBallSpawner>();
            RequireForUpdate<PongInputs>();
            RequireForUpdate<DeterministicSimulationTime>();
            
            _boolForSecondLocalClientForSpawningLastBall = false;
        }

        protected override void OnStartRunning()
        { 
           _randomSeedFromServer = SystemAPI.GetSingleton<DeterministicSettings>().randomSeed;
           _random = new Random((int)_randomSeedFromServer);
        }

        protected override void OnUpdate()
        {
            if (GameSettings.Instance.GetTotalBallsSpawned() >= GameSettings.Instance.GetTotalBallsToSpawn())
            {
                if (World.Name == "ClientWorld1" && !_boolForSecondLocalClientForSpawningLastBall)
                {
                    _boolForSecondLocalClientForSpawningLastBall = true;
                }
                else return;
            }
            
            var ballPrefabEntity = SystemAPI.GetSingleton<PongBallSpawner>().Ball;
                
            var ballEntity = EntityManager.Instantiate(ballPrefabEntity);
            EntityManager.AddComponentData(ballEntity, new DeterministicEntityID { id = DeterministicLogger.Instance.GetDeterministicEntityID(World.Name) }); // For debugging purposes. The ID allows to sort the entities which are hashes which otherwise would be impossible due to nondeterminism of entities placement in chunks and nondeterminism of entity ID and Version on different devices (sorting by those is nondeterministic between 2 machines)
            EntityManager.SetComponentData(ballEntity, new LocalTransform
            {
                Position = new float3(0, 0, 13),
                Scale = 0.2f,
                Rotation = quaternion.identity
            });
            EntityManager.SetName(ballEntity, "Ball"); // For debugging purposes. The name will be visible in nondeterminism debug file
            
            // var ballDirection = _random.Next(0, 2);
            // var angleInDegrees = 0;
            // if (ballDirection == 0) angleInDegrees = _random.Next(0, 2) == 0 ? _random.Next(0, 70) : _random.Next(110, 180);
            // else angleInDegrees = _random.Next(0, 2) == 0 ? _random.Next(180, 250) : _random.Next(290, 360);
            
            // var angleInRadians = angleInDegrees * Mathf.Deg2Rad;
            // var ballFinalDirection = new float3(Mathf.Cos(angleInRadians), Mathf.Sin(angleInRadians), 0);

            var ballDirectionX = _random.Next(20, 50) * (_random.Next(0, 2) * 2 - 1);
            var ballDirectionY = _random.Next(30, 100) * (_random.Next(0, 2) * 2 - 1); 
            var ballFinalDirection = new float3(ballDirectionX, ballDirectionY, 0);
            
            ballFinalDirection = math.normalize(ballFinalDirection);
            
            var speed = _random.Next(GameSettings.Instance.GetMinBallSpeed(), GameSettings.Instance.GetMaxBallSpeed());
            EntityManager.SetComponentData(ballEntity, new BallVelocity { value = ballFinalDirection * speed });
                    
            if (World.Name == "ClientWorld") // To prevent local simulation for counting points twice (from both worlds)
            {
                GameSettings.Instance.AddSpawnedBalls(1);
            }
        }
    }
}