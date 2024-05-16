using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CapsulesGame
{
    public class MenuHandler : MonoBehaviour
    {
        public Toggle Is2ClientSimulation;
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


            if (Is2ClientSimulation.isOn)
            {
                for (int i = 0; i < 2; i++)
                {
                    var anotherClient = CreateClientWorld($"ClientWorld{i + 1}");
                    if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                            out Entity deterministicSettings2))
                    {
                        EntityManager anotherClientEntityManager = anotherClient.EntityManager;
                        Entity anotherClientDeterministicSettings =
                            anotherClientEntityManager.CreateEntity(typeof(DeterministicSettings));
                        anotherClientEntityManager.SetComponentData(anotherClientDeterministicSettings,
                            entityManager.GetComponentData<DeterministicSettings>(deterministicSettings2));
                
                        Entity anotherClientAuthoringEntity = anotherClientEntityManager.CreateEntity(typeof(DeterministicClient), typeof(DeterministicClientConnect), typeof(DeterministicClientDisconnect), typeof(DeterministicClientSendData));
                        anotherClientEntityManager.SetComponentEnabled<DeterministicClientConnect>(anotherClientAuthoringEntity, true);
                        anotherClientEntityManager.SetComponentEnabled<DeterministicClientDisconnect>(anotherClientAuthoringEntity, false);
                        anotherClientEntityManager.SetComponentEnabled<DeterministicClientSendData>(anotherClientAuthoringEntity, false);
                    }
                    else
                    {
                        Debug.LogError(
                            "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
                    }
                }
            }

            SceneManager.LoadScene("CapsulesLoading");


            if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                    out Entity deterministicSettings))
            {
                // Create a new DeterministicSettings entity in the server world and copy the component data
                EntityManager serverEntityManager = server.EntityManager;
                Entity serverDeterministicSettings = serverEntityManager.CreateEntity(typeof(DeterministicSettings));
                serverEntityManager.SetComponentData(serverDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));
                
                Entity serverAuthoringEntity = serverEntityManager.CreateEntity(typeof(DeterministicServer), typeof(DeterministicServerListen), typeof(DeterministicServerRunSimulation));
                serverEntityManager.SetComponentEnabled<DeterministicServerListen>(serverAuthoringEntity, true);
                serverEntityManager.SetComponentEnabled<DeterministicServerRunSimulation>(serverAuthoringEntity, false);


                EntityManager clientEntityManager = client.EntityManager;
                Entity clientDeterministicSettings = clientEntityManager.CreateEntity(typeof(DeterministicSettings));
                clientEntityManager.SetComponentData(clientDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));
                
                Entity clientAuthoringEntity = clientEntityManager.CreateEntity(typeof(DeterministicClient), typeof(DeterministicClientConnect), typeof(DeterministicClientDisconnect), typeof(DeterministicClientSendData));
                clientEntityManager.SetComponentEnabled<DeterministicClientConnect>(clientAuthoringEntity, true);
                clientEntityManager.SetComponentEnabled<DeterministicClientDisconnect>(clientAuthoringEntity, false);
                clientEntityManager.SetComponentEnabled<DeterministicClientSendData>(clientAuthoringEntity, false);
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
            var client = CreateClientWorld("ClientWorld2");

            if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                    out Entity deterministicSettings))
            {
                // Create a new DeterministicSettings entity in the server world and copy the component data
                EntityManager clientEntityManager = client.EntityManager;
                Entity clientDeterministicSettings = clientEntityManager.CreateEntity(typeof(DeterministicSettings));
                clientEntityManager.SetComponentData(clientDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));
                
                Entity clientAuthoringEntity = clientEntityManager.CreateEntity(typeof(DeterministicClient), typeof(DeterministicClientConnect), typeof(DeterministicClientDisconnect), typeof(DeterministicClientSendData));
                clientEntityManager.SetComponentEnabled<DeterministicClientConnect>(clientAuthoringEntity, true);
                clientEntityManager.SetComponentEnabled<DeterministicClientDisconnect>(clientAuthoringEntity, false);
                clientEntityManager.SetComponentEnabled<DeterministicClientSendData>(clientAuthoringEntity, false);
            }
            else
            {
                Debug.LogError(
                    "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
            }

            DestroyLocalSimulationWorld();

            SceneManager.LoadScene("CapsulesLoading");

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