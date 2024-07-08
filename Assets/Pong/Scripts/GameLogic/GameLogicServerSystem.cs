using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PongGame
{
    /// <summary>
    /// System responsible to modify server behaviour based on user input.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial struct GameLogicServerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicServerComponent>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var server = SystemAPI.GetSingletonRW<DeterministicServerComponent>();
            if (SceneManager.GetActiveScene().name == "PongGame" && Input.GetKey(KeyCode.Q)) // Simulation of disconnection 
            {
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.Disconnect;
            }
            else if (SceneManager.GetActiveScene().name == "PongGame" && Input.GetKey(KeyCode.Space)) // Starting the simulation
            {
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.RunDeterministicSimulation;
            }
            else if (SceneManager.GetActiveScene().name == "PongGame" && SystemAPI.GetSingleton<DeterministicServerComponent>().deterministicServerWorkingMode == DeterministicServerWorkingMode.None)
            {
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.ListenForConnections;
            }
        }
    }
}