using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class GameLogicClientSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicClientComponent>();
        }

        protected override void OnUpdate()
        {
            if (SceneManager.GetActiveScene().name == "PongGame" && Input.GetKey(KeyCode.C) &&
                World.Name == "ClientWorld1") // Simulation of disconnection
            {
                var client = SystemAPI.GetSingleton<DeterministicClientComponent>();
                client.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                SystemAPI.SetSingleton(client);

                SceneManager.LoadScene("PongLoading");
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.SendData)
            {
                SceneManager.LoadScene("PongGame");
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None)
            {
                var client = SystemAPI.GetSingleton<DeterministicClientComponent>();
                client.deterministicClientWorkingMode = DeterministicClientWorkingMode.Connect;
                SystemAPI.SetSingleton(client);
            }
        }
    }
}