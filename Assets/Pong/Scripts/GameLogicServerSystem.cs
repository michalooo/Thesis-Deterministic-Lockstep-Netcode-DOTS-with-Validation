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
        private Entity _settings;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicSettings>();
        }

        protected override void OnStartRunning()
        {
            _settings = SystemAPI.GetSingletonEntity<DeterministicSettings>();
        }

        protected override void OnUpdate()
        {
            // option for the server to start the game
            if (SceneManager.GetActiveScene().name == "PongLoading" && Input.GetKey(KeyCode.Space))
            {
                SystemAPI.SetComponentEnabled<DeterministicServerListen>(_settings, false);
                SystemAPI.SetComponentEnabled<DeterministicServerRunSimulation>(_settings, true);
            }
        }
    }
}