using System.Collections;
using DeterministicLockstep;
using Unity.Burst;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace PongGame
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(UserSystemGroup))]
    public partial class GameLogicClientSystem : SystemBase
    {
        private AsyncOperation asyncLoad = null;
        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicClientComponent>();
        }
        
        protected override void OnUpdate()
        {
            var client = SystemAPI.GetSingleton<DeterministicClientComponent>();
            if (SceneManager.GetActiveScene().name == "PongGame" && Input.GetKey(KeyCode.C) &&
                World.Name == "ClientWorld1") // Simulation of disconnection
            {
                client = SystemAPI.GetSingleton<DeterministicClientComponent>();
                client.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                SystemAPI.SetSingleton(client);

                SceneManager.LoadScene("PongLoading");
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.PrepareGame)
            {
                asyncLoad = SceneManager.LoadSceneAsync("PongGame");
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None)
            {
                client = SystemAPI.GetSingleton<DeterministicClientComponent>();
                client.deterministicClientWorkingMode = DeterministicClientWorkingMode.Connect;
                SystemAPI.SetSingleton(client);
            }
            
            if (asyncLoad == null || !asyncLoad.isDone) return;
            
            client.deterministicClientWorkingMode = DeterministicClientWorkingMode.SendData;
            SystemAPI.SetSingleton(client);
            asyncLoad = null;
        }
    }
}