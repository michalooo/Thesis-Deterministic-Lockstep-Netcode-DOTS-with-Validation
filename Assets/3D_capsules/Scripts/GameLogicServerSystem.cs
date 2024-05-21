using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CapsulesGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class GameLogicServerSystem : SystemBase
    {
        private DeterministicServerComponent _server;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicServerComponent>();
        }

        protected override void OnStartRunning()
        {
            _server = SystemAPI.GetSingleton<DeterministicServerComponent>();
        }

        protected override void OnUpdate()
        {
            // option for the server to start the game
            if (SceneManager.GetActiveScene().name == "CapsulesLoading" && Input.GetKey(KeyCode.Space))
            {
                _server.deterministicServerWorkingMode = DeterministicServerWorkingMode.RunDeterministicSimulation;
            }
            else if (SceneManager.GetActiveScene().name == "CapsulesLoading" && _server.deterministicServerWorkingMode == DeterministicServerWorkingMode.None)
            {
                _server.deterministicServerWorkingMode = DeterministicServerWorkingMode.ListenForConnections;
            }
        }
    }
}