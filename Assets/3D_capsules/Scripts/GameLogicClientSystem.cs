using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CapsulesGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class GameLogicClientSystem : SystemBase
    {
        private Entity _client;

        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicClient>();
        }

        protected override void OnStartRunning()
        {
            _client = SystemAPI.GetSingletonEntity<DeterministicClient>();
        }

        protected override void OnUpdate()
        {
            if (SceneManager.GetActiveScene().name == "CapsulesGame" && Input.GetKey(KeyCode.C) &&
                World.Name == "ClientWorld2") // Simulation of disconnection
            {
                SystemAPI.SetComponentEnabled<DeterministicClientDisconnect>(_client, true);

                SceneManager.LoadScene("CapsulesGame");
            }

            if (SceneManager.GetActiveScene().name == "CapsulesLoading" &&
                SystemAPI.IsComponentEnabled<DeterministicClientSendData>(_client))
            {
                SceneManager.LoadScene("CapsulesGame");
            }
        }
    }
}