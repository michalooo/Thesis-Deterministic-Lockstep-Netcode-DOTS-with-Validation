using DeterministicLockstep;
using TMPro;
using Unity.Entities;
using Unity.Transforms;
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
            RequireForUpdate<DeterministicComponent>();
        }

        protected override void OnStartRunning()
        {
            var client = SystemAPI.GetSingletonBuffer<DeterministicComponent>();
            client.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<TextMeshProUGUI>(),
            });
            client.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<GameSettings>(),
            });
            client.Add(new DeterministicComponent
            {
                Type = ComponentType.ReadOnly<Velocity>(),
            });
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
                if (SceneManager.GetActiveScene().name == "PongGame")
                {
                    client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.ClientReady;
                }
                else gameAsyncLoad = SceneManager.LoadSceneAsync("PongGame");
            }
            else if (client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.RunDeterministicSimulation)
            {
                UISingleton.Instance.SetWaitingTextEnabled(false);
            }
            else if(client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.None)
            {
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Connect;
            }
            else if(client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.Desync)
            {
                UISingleton.Instance.SetDesyncMessageEnabled(true);
            }
            
            
            if (gameAsyncLoad != null && gameAsyncLoad.isDone)
            {
                gameAsyncLoad = null;
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.ClientReady;
            }
        }
    }
}