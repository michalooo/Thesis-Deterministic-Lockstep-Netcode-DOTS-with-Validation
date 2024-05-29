using DeterministicLockstep;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    [BurstCompile]
    public partial struct GameLogicServerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicServerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // option for the server to start the game
            if (SceneManager.GetActiveScene().name == "PongLoading" && Input.GetKey(KeyCode.Space))
            {
                var server = SystemAPI.GetSingleton<DeterministicServerComponent>();
                server.deterministicServerWorkingMode = DeterministicServerWorkingMode.RunDeterministicSimulation;
                SystemAPI.SetSingleton(server);
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.None)
            {
                var server = SystemAPI.GetSingleton<DeterministicServerComponent>();
                server.deterministicServerWorkingMode = DeterministicServerWorkingMode.ListenForConnections;
                SystemAPI.SetSingleton(server);
            }
        }
    }
}