using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PongGame
{
    public class PongMenuHandler : MonoBehaviour
    {
        public InputField Address;
        public InputField Port;

        /// <summary>
        /// Function to quit the game 
        /// </summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
            Application.Quit();
        }

        /// <summary>
        /// Function to start the game as a host
        /// </summary>
        public void HostGame()
        {
            Debug.Log($"[HostGame]");
            CreateServerWorld("ServerWorld");
            var client = CreateClientWorld("ClientWorld");

            SceneManager.LoadScene("Loading");

            DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld ??= client;
        }

        /// <summary>
        /// Function to connect to a game
        /// </summary>
        public void ConnectToGame()
        {
            Debug.Log($"[ConnectToServer]");
            var client = CreateClientWorld("ClientWorld2");
            DestroyLocalSimulationWorld();

            SceneManager.LoadScene("Loading");

            World.DefaultGameObjectInjectionWorld ??= client;
        }

        /// <summary>
        /// Function that creates server world
        /// </summary>
        /// <param name="name">Server world name</param>
        /// <returns>Created server world</returns>
        public static World CreateServerWorld(string name)
        {
#if UNITY_CLIENT && !UNITY_SERVER && !UNITY_EDITOR
            throw new PlatformNotSupportedException("This executable was built using a 'client-only' build target. Thus, cannot create a server world. In your ProjectSettings, change your 'Client Build Target' to `ClientAndServer` to support creating client-hosted servers.");
#else

            var world = new World(name, WorldFlags.GameServer);

            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ServerSimulation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
#endif
        }

        /// <summary>
        /// Function that creates client world
        /// </summary>
        /// <param name="name">Client world name</param>
        /// <returns>Created client world</returns>
        public static World CreateClientWorld(string name)
        {
#if UNITY_SERVER && !UNITY_EDITOR
            throw new PlatformNotSupportedException("This executable was built using a 'server-only' build target (likely DGS). Thus, cannot create client worlds.");
#else
            var world = new World(name, WorldFlags.GameClient);

            var systems =
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation |
                                                         WorldSystemFilterFlags.Presentation);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            if (World.DefaultGameObjectInjectionWorld == null)
                World.DefaultGameObjectInjectionWorld = world;

            return world;
#endif
        }

        /// <summary>
        /// Function that destroys local simulation world
        /// </summary>
        protected void DestroyLocalSimulationWorld()
        {
            foreach (var world in World.All)
            {
                if (world.Flags == WorldFlags.Game)
                {
                    world.Dispose();
                    break;
                }
            }
        }
    }
}