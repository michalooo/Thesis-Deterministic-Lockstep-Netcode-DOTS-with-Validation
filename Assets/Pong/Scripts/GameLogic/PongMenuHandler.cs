using System.Collections.Generic;
using DeterministicLockstep;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PongGame
{
    /// <summary>
    /// Class responsible for handling the menu options for the Pong game.
    /// </summary>
    public class PongMenuHandler : MonoBehaviour
    {
        /// <summary>
        /// Address of the local machine.
        /// </summary>
        private const string LOCAL_SERVER_ADDRESS = "127.0.0.1";
        
        // Host options
        
        [Tooltip("Toggle to enable local multiplayer simulation. If enabled, a second client will be created.")]
        public Toggle IsLocalMultiplayerSimulation;
        
        [Tooltip("Toggle to enable replay from file.")]
        public Toggle IsReplayFromFile;
        
        [Tooltip("Input field for the game port. This port will be used to host the game.")]
        public InputField GamePort;
        
        [Tooltip("Input field for the frame rate of the game. This will be used to set the simulation tick rate.")]
        public InputField FrameRate;
        
        [Tooltip("Input field for the forced input latency. This will be used to simulate network latency.")]
        public InputField ForcedInputLatency;
        
        [Tooltip("Input field for the hash calculation option. This will be used to set the hash calculation option.")]
        public TMP_Dropdown hashOption;
        
        // Client options
        
        [Tooltip("Input field for the host address. This address will be used to connect to the game.")]
        public InputField HostAddress;
        
        [Tooltip("Input field for the host port. This port will be used to connect to the game.")]
        public InputField HostPort;
        
       

        private void Start()
        {
            List<World> worlds = new List<World>();
            foreach (var world in World.All)
            {
                if (world.Flags is WorldFlags.GameServer or WorldFlags.GameClient)
                {
                    worlds.Add(world);
                }
            }
            
            foreach (var world in worlds)
            {
                world.Dispose();
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }
        
        public void HostGame()
        {
            var server = CreateServerWorld("ServerWorld");
            var client = CreateClientWorld("ClientWorld");
            
            EntityManager serverEntityManager = server.EntityManager;
            EntityManager clientEntityManager = client.EntityManager;
            
            serverEntityManager.CreateSingleton(new DeterministicSettings
            {
                _serverAddress = LOCAL_SERVER_ADDRESS,
                _serverPort = int.Parse(GamePort.text),
                hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                ticksOfForcedInputLatency = int.Parse(ForcedInputLatency.text),
                simulationTickRate = int.Parse(FrameRate.text),
                allowedConnectionsPerGame = 2,
                isReplayFromFile = IsReplayFromFile.isOn
            });
            
            clientEntityManager.CreateSingleton(new DeterministicSettings
            {
                _serverAddress = LOCAL_SERVER_ADDRESS,
                _serverPort = int.Parse(GamePort.text),
                hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                ticksOfForcedInputLatency = int.Parse(ForcedInputLatency.text),
                simulationTickRate = int.Parse(FrameRate.text),
                allowedConnectionsPerGame = 2,
                isReplayFromFile = IsReplayFromFile.isOn
            });
            
            if (IsLocalMultiplayerSimulation.isOn)
            {
                var secondClient = CreateClientWorld($"ClientWorld1");
                EntityManager secondClientEntityManager = secondClient.EntityManager;
                secondClientEntityManager.CreateSingleton(new DeterministicSettings
                {
                    _serverAddress = LOCAL_SERVER_ADDRESS,
                    _serverPort = int.Parse(GamePort.text),
                    hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                    ticksOfForcedInputLatency = int.Parse(ForcedInputLatency.text),
                    simulationTickRate = int.Parse(FrameRate.text),
                    allowedConnectionsPerGame = 2,
                    isReplayFromFile = IsReplayFromFile.isOn
                });
            }
            

            SceneManager.LoadScene("PongGame");
            World.DefaultGameObjectInjectionWorld = client;
        }

        public void ConnectToGame()
        {
            var client = CreateClientWorld("ClientWorld");
            EntityManager clientEntityManager = client.EntityManager;
            
            clientEntityManager.CreateSingleton(new DeterministicSettings
            {
                _serverPort = int.Parse(GamePort.text),
                hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                ticksOfForcedInputLatency = int.Parse(ForcedInputLatency.text),
                simulationTickRate = int.Parse(FrameRate.text),
                allowedConnectionsPerGame = 2,
                isReplayFromFile = IsReplayFromFile.isOn,
                _serverAddress = HostAddress.text,
            });
            
            SceneManager.LoadScene("PongGame");
            World.DefaultGameObjectInjectionWorld = client;
        }

        private static World CreateServerWorld(string name)
        {
            var world = new World(name, WorldFlags.GameServer);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
        }
        
        private static World CreateClientWorld(string name)
        {
            var world = new World(name, WorldFlags.GameClient);

            var systems =
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation |
                                                         WorldSystemFilterFlags.Presentation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            World.DefaultGameObjectInjectionWorld = world;

            return world;
        }
    }
}