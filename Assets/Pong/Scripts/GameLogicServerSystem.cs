using DeterministicLockstep;
using Unity.Entities;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial struct GameLogicServerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DeterministicServerComponent>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var server = SystemAPI.GetSingletonRW<DeterministicServerComponent>();
            if (SceneManager.GetActiveScene().name == "PongGame" && Input.GetKey(KeyCode.Q)) //Simulation of disconnection 
            {
                server.ValueRW.deterministicServerWorkingMode = DeterministicServerWorkingMode.Disconnect;
            }
            else if (SceneManager.GetActiveScene().name == "PongGame" && Input.GetKey(KeyCode.Space))
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