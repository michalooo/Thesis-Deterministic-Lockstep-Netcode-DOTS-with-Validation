using DeterministicLockstep;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PongGame
{
    /// <summary>
    /// System responsible to modify lockstep client behaviour based on user input and actions.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateBefore(typeof(InputGatherSystem))]
    public partial class GameLogicClientSystem : SystemBase
    {
        private AsyncOperation _gameAsyncLoad = null;
        
        protected override void OnCreate()
        {
            RequireForUpdate<DeterministicClientComponent>();
            RequireForUpdate<DeterministicComponent>();
        }

        protected override void OnStartRunning()
        {
            var client = SystemAPI.GetSingletonBuffer<DeterministicComponent>(); // Add all components that should be deterministic
            client.Add(new DeterministicComponent
            {
                type = ComponentType.ReadOnly<TextMeshProUGUI>(),
            });
            client.Add(new DeterministicComponent
            {
                type = ComponentType.ReadOnly<GameSettings>(),
            });
            client.Add(new DeterministicComponent
            {
                type = ComponentType.ReadOnly<BallVelocity>(),
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
            else if (client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.LoadingGame && _gameAsyncLoad == null)
            {
                if (SceneManager.GetActiveScene().name == "PongGame")
                {
                    client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.ClientReady;
                }
                else _gameAsyncLoad = SceneManager.LoadSceneAsync("PongGame");
            }
            else if (client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.RunDeterministicSimulation)
            {
                GameManagerSingleton.Instance.SetWaitingTextEnabled(false);
            }
            else if(client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.None)
            {
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.Connect;
            }
            else if(client.ValueRO.deterministicClientWorkingMode == DeterministicClientWorkingMode.Desync)
            {
                GameManagerSingleton.Instance.SetDesyncMessageEnabled(true);
            }
            
            
            if (_gameAsyncLoad != null && _gameAsyncLoad.isDone)
            {
                _gameAsyncLoad = null;
                client.ValueRW.deterministicClientWorkingMode = DeterministicClientWorkingMode.ClientReady;
            }
        }
    }
}