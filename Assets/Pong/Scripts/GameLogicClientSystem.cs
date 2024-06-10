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
            if (SceneManager.GetActiveScene().name == "PongGame" && (Input.GetKey(KeyCode.Q) || client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.Disconnect)) //Simulation of disconnection 
            {
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Disconnect;
                
                foreach (var world in World.All)
                {
                    if (world.Flags is not (WorldFlags.GameServer or WorldFlags.GameClient))
                    {
                        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);
                        World.DefaultGameObjectInjectionWorld = world;
                        break;
                    }
                }

                SceneManager.LoadSceneAsync("PongMenu");
            }
            else if (client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.LoadingGame && gameAsyncLoad == null)
            {
                gameAsyncLoad = SceneManager.LoadSceneAsync("PongGame");
            }
            else if (client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.RunDeterministicSimulation)
            {
                UISingleton.Instance.SetWaitingTextEnabled(false);
            }
            else if(client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.None)
            {
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Connect;
            }
            
            
            if (gameAsyncLoad != null && gameAsyncLoad.isDone)
            {
                gameAsyncLoad = null;
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.ClientReady;
            }
        }
    }
}