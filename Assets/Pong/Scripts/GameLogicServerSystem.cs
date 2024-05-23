using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class GameLogicServerSystem : SystemBase
    {

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicServerComponent>();
        }

        protected override void OnUpdate()
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