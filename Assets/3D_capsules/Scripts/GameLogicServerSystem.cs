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
        private Entity _server;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicServer>();
        }

        protected override void OnStartRunning()
        {
            _server = SystemAPI.GetSingletonEntity<DeterministicServer>();
        }

        protected override void OnUpdate()
        {
            // option for the server to start the game
            if (SceneManager.GetActiveScene().name == "CapsulesLoading" && Input.GetKey(KeyCode.Space))
            {
                SystemAPI.SetComponentEnabled<DeterministicServerListen>(_server, false);
                SystemAPI.SetComponentEnabled<DeterministicServerRunSimulation>(_server, true);
            }
        }
    }
}