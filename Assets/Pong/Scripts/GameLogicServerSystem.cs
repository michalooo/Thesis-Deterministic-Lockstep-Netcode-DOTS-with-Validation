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
            var server = SystemAPI.GetSingletonRW<DeterministicServerComponent>();
            if ((SceneManager.GetActiveScene().name == "PongGame" || SceneManager.GetActiveScene().name == "PongLoading") && Input.GetKey(KeyCode.Q)) //Simulation of disconnection 
            {
                Debug.Log("Server is disconnecting all clients");
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.Disconnect;
            }
            // option for the server to start the game
            if (SceneManager.GetActiveScene().name == "PongLoading" && Input.GetKey(KeyCode.Space))
            {
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.RunDeterministicSimulation;
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.None)
            {
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.ListenForConnections;
            }
        }
    }
}