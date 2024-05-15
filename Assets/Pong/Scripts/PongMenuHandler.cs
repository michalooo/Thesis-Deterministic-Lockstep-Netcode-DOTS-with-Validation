using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PongGame
{
    public class PongMenuHandler : MonoBehaviour
    {
        public Toggle IsLocalMultiplayerSimulation;
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
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery deterministicSettingsQuery = entityManager.CreateEntityQuery(typeof(DeterministicSettings));

            Debug.Log($"[HostGame]");
            var server = CreateServerWorld("ServerWorld");
            var client = CreateClientWorld("ClientWorld");


            if (IsLocalMultiplayerSimulation.isOn)
            {
                var secondClient = CreateClientWorld($"ClientWorld1");
                if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                        out Entity deterministicSettings2))
                {
                    EntityManager anotherClientEntityManager = secondClient.EntityManager;
                    Entity anotherClientDeterministicSettings =
                        anotherClientEntityManager.CreateEntity(typeof(DeterministicSettings));
                    anotherClientEntityManager.SetComponentData(anotherClientDeterministicSettings,
                        entityManager.GetComponentData<DeterministicSettings>(deterministicSettings2));

                    anotherClientEntityManager.AddComponent<DeterministicClientConnect>(anotherClientDeterministicSettings);
                    anotherClientEntityManager.SetComponentEnabled<DeterministicClientConnect>(anotherClientDeterministicSettings,
                        true);
                    anotherClientEntityManager.AddComponent<DeterministicClientDisconnect>(anotherClientDeterministicSettings);
                    anotherClientEntityManager.SetComponentEnabled<DeterministicClientDisconnect>(
                        anotherClientDeterministicSettings, false);
                    anotherClientEntityManager.AddComponent<DeterministicClientSendData>(anotherClientDeterministicSettings);
                    anotherClientEntityManager.SetComponentEnabled<DeterministicClientSendData>(
                        anotherClientDeterministicSettings, false);
                }
                else
                {
                    Debug.LogError(
                        "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
                }
            }

            SceneManager.LoadScene("PongLoading");


            if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                    out Entity deterministicSettings))
            {
                // Create a new DeterministicSettings entity in the server world and copy the component data
                EntityManager serverEntityManager = server.EntityManager;
                Entity serverDeterministicSettings = serverEntityManager.CreateEntity(typeof(DeterministicSettings));
                serverEntityManager.SetComponentData(serverDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));

                serverEntityManager.AddComponent<DeterministicServerListen>(serverDeterministicSettings);
                serverEntityManager.SetComponentEnabled<DeterministicServerListen>(serverDeterministicSettings, true);
                serverEntityManager.AddComponent<DeterministicServerRunSimulation>(serverDeterministicSettings);
                serverEntityManager.SetComponentEnabled<DeterministicServerRunSimulation>(serverDeterministicSettings,
                    false);


                EntityManager clientEntityManager = client.EntityManager;
                Entity clientDeterministicSettings = clientEntityManager.CreateEntity(typeof(DeterministicSettings));
                clientEntityManager.SetComponentData(clientDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));

                clientEntityManager.AddComponent<DeterministicClientConnect>(clientDeterministicSettings);
                clientEntityManager.SetComponentEnabled<DeterministicClientConnect>(clientDeterministicSettings, true);
                clientEntityManager.AddComponent<DeterministicClientDisconnect>(clientDeterministicSettings);
                clientEntityManager.SetComponentEnabled<DeterministicClientDisconnect>(clientDeterministicSettings,
                    false);
                clientEntityManager.AddComponent<DeterministicClientSendData>(clientDeterministicSettings);
                clientEntityManager.SetComponentEnabled<DeterministicClientSendData>(clientDeterministicSettings,
                    false);
            }
            else
            {
                Debug.LogError(
                    "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
            }

            DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld ??= client;
        }

        /// <summary>
        /// Function to connect to a game
        /// </summary>
        public void ConnectToGame()
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery deterministicSettingsQuery = entityManager.CreateEntityQuery(typeof(DeterministicSettings));

            Debug.Log($"[ConnectToServer]");
            var client = CreateClientWorld("ClientWorld");

            if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                    out Entity deterministicSettings))
            {
                // Create a new DeterministicSettings entity in the server world and copy the component data
                EntityManager clientEntityManager = client.EntityManager;
                Entity clientDeterministicSettings = clientEntityManager.CreateEntity(typeof(DeterministicSettings));
                clientEntityManager.SetComponentData(clientDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));

                clientEntityManager.AddComponent<DeterministicClientConnect>(clientDeterministicSettings);
                clientEntityManager.SetComponentEnabled<DeterministicClientConnect>(clientDeterministicSettings, true);
                clientEntityManager.AddComponent<DeterministicClientDisconnect>(clientDeterministicSettings);
                clientEntityManager.SetComponentEnabled<DeterministicClientDisconnect>(clientDeterministicSettings,
                    false);
                clientEntityManager.AddComponent<DeterministicClientSendData>(clientDeterministicSettings);
                clientEntityManager.SetComponentEnabled<DeterministicClientSendData>(clientDeterministicSettings,
                    false);
            }
            else
            {
                Debug.LogError(
                    "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
            }

            DestroyLocalSimulationWorld();

            SceneManager.LoadScene("PongLoading");

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

            World.DefaultGameObjectInjectionWorld ??= world;

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