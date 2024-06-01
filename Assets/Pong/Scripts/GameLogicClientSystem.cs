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
        private AsyncOperation gameAsyncLoad = null;
        
        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicClientComponent>();
        }
        
        protected override void OnUpdate()
        {
            var client = SystemAPI.GetSingletonRW<DeterministicClientComponent>();
            if ((SceneManager.GetActiveScene().name == "PongGame" || SceneManager.GetActiveScene().name == "PongLoading") && (Input.GetKey(KeyCode.Q) || client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect)) //Simulation of disconnection 
            {
                Debug.Log("Client requested to disconnect");
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                
                foreach (var world in World.All)
                {
                    if (world.Flags is not (WorldFlags.GameServer or WorldFlags.GameClient))
                    {
                        Debug.Log(world);
                        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
                        World.DefaultGameObjectInjectionWorld = world;
                        break;
                    }
                }

                SceneManager.LoadSceneAsync("PongMenu");
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.PrepareGame)
            {
                gameAsyncLoad = SceneManager.LoadSceneAsync("PongGame");
            }
            else if (SceneManager.GetActiveScene().name == "PongLoading" && SystemAPI.GetSingleton<DeterministicClientComponent>().deterministicClientWorkingMode == DeterministicClientWorkingMode.None)
            {
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Connect;
            }

            if (gameAsyncLoad != null && gameAsyncLoad.isDone)
            {
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.SendData;
                gameAsyncLoad = null;
            }
        }
    }
}