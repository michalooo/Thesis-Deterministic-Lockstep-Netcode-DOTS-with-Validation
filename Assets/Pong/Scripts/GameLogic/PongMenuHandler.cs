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
        
        [Tooltip("Toggle to enable local multiplayer simulation. If enabled, a second client will be created on the same machine allowing for local testing or local game with second player.")]
        public Toggle isLocalMultiplayerSimulation;
        
        [Tooltip("Toggle to enable replay from file. If enabled the game will replay the game from a server input recording and game settings files placed in NonDeterminsmLogs folder")]
        public Toggle isReplayFromFile;
        
        [Tooltip("Input field for the game port. This port will be used to host the game and clients can connect to it.")]
        public InputField gamePort;
        
        [Tooltip("Target frame rate of the game. This will be used to set the simulation tick rate.")]
        public InputField frameRate;
        
        [Tooltip("Forced input latency used in lockstep netcode model. This will define how many frames of delay the player will see as default. The bigger the value the more delay the player will see but also the probability of lag will decrease since there will be a bigger time window for the server to receive the input.")]
        public InputField forcedInputLatency;
        
        [Tooltip("Hash calculation option. This will be used to set the hash calculation option.")]
        public TMP_Dropdown hashOption;
        
        // Client options
        
        [Tooltip("Host address. Client will try to connect to this address when starting the game. Only valid if client is not a host")]
        public InputField hostAddress;
        
        [Tooltip("Host port. Client will try to connect to this port when starting the game. Only valid if client is not a host")]
        public InputField hostPort;
        
        private void Start() // This is solely done because of the "quit" functionality (we want to reset the state of the game)
        {
            var serverClientWorldsList = new List<World>();
            foreach (var world in World.All)
            {
                if (world.Flags is WorldFlags.GameServer or WorldFlags.GameClient)
                {
                    serverClientWorldsList.Add(world);
                }
            }
            
            foreach (var world in serverClientWorldsList)
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
            var serverWorld = CreateServerWorld("ServerWorld");
            var clientWorld = CreateClientWorld("ClientWorld");
            
            var serverWorldEntityManager = serverWorld.EntityManager;
            var clientWorldEntityManager = clientWorld.EntityManager;
            
            serverWorldEntityManager.CreateSingleton(new DeterministicSettings // TODO: make this from the package side so user doesn't need to do this
            {
                serverAddress = LOCAL_SERVER_ADDRESS,
                serverPort = int.Parse(gamePort.text),
                hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                ticksOfForcedInputLatency = int.Parse(forcedInputLatency.text),
                simulationTickRate = int.Parse(frameRate.text),
                allowedConnectionsPerGame = 2, // TODO: hardcoded
                isReplayFromFile = isReplayFromFile.isOn
            });
            
            clientWorldEntityManager.CreateSingleton(new DeterministicSettings
            {
                serverAddress = LOCAL_SERVER_ADDRESS,
                serverPort = int.Parse(gamePort.text),
                hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                ticksOfForcedInputLatency = int.Parse(forcedInputLatency.text),
                simulationTickRate = int.Parse(frameRate.text),
                allowedConnectionsPerGame = 2, // TODO: hardcoded
                isReplayFromFile = isReplayFromFile.isOn
            });
            
            if (isLocalMultiplayerSimulation.isOn)
            {
                var secondLocalClientWorld = CreateClientWorld($"ClientWorld1");
                var secondLocalClientWorldEntityManager = secondLocalClientWorld.EntityManager;
                secondLocalClientWorldEntityManager.CreateSingleton(new DeterministicSettings
                {
                    serverAddress = LOCAL_SERVER_ADDRESS,
                    serverPort = int.Parse(gamePort.text),
                    hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                    ticksOfForcedInputLatency = int.Parse(forcedInputLatency.text),
                    simulationTickRate = int.Parse(frameRate.text),
                    allowedConnectionsPerGame = 2, // TODO: hardcoded
                    isReplayFromFile = isReplayFromFile.isOn
                });
            }
            
            SceneManager.LoadScene("PongGame");
            World.DefaultGameObjectInjectionWorld = clientWorld;
        }

        public void ConnectToGame()
        {
            var clientWorld = CreateClientWorld("ClientWorld");
            var clientWorldEntityManager = clientWorld.EntityManager;
            
            clientWorldEntityManager.CreateSingleton(new DeterministicSettings
            {
                serverPort = int.Parse(gamePort.text),
                hashCalculationOption = (DeterminismHashCalculationOption) hashOption.value,
                ticksOfForcedInputLatency = int.Parse(forcedInputLatency.text),
                simulationTickRate = int.Parse(frameRate.text),
                allowedConnectionsPerGame = 2, // TODO: hardcoded
                isReplayFromFile = isReplayFromFile.isOn,
                serverAddress = hostAddress.text,
            });
            
            SceneManager.LoadScene("PongGame");
            World.DefaultGameObjectInjectionWorld = clientWorld;
        }

        private static World CreateServerWorld(string worldName)
        {
            var serverWorld = new World(worldName, WorldFlags.GameServer);

            var serverWorldSystems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(serverWorld, serverWorldSystems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(serverWorld);

            World.DefaultGameObjectInjectionWorld ??= serverWorld;

            return serverWorld;
        }
        
        private static World CreateClientWorld(string worldName)
        {
            var clientWorld = new World(worldName, WorldFlags.GameClient);

            var clientWorldSystems =
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation |
                                                         WorldSystemFilterFlags.Presentation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(clientWorld, clientWorldSystems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(clientWorld);

            World.DefaultGameObjectInjectionWorld = clientWorld;

            return clientWorld;
        }
    }
}