using System;
using System.Collections.Generic;
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
                Debug.Log("Destroying world: " + world.Name);
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
            Debug.Log(World.DefaultGameObjectInjectionWorld);
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
                    Entity anotherClientDeterministicSettingsEntity =
                        anotherClientEntityManager.CreateEntity(typeof(DeterministicSettings));
                    
                    var anotherClientSettings = entityManager.GetComponentData<DeterministicSettings>(deterministicSettings2);
                    anotherClientSettings._serverAddress = Address.text;
                    anotherClientSettings._serverPort = ushort.Parse(Port.text);
                    anotherClientEntityManager.SetComponentData(anotherClientDeterministicSettingsEntity,
                        anotherClientSettings);
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
                EntityManager serverEntityManager = server.EntityManager;
                Entity serverDeterministicSettings = serverEntityManager.CreateEntity(typeof(DeterministicSettings));
                serverEntityManager.SetComponentData(serverDeterministicSettings,
                    entityManager.GetComponentData<DeterministicSettings>(deterministicSettings));


                EntityManager clientEntityManager = client.EntityManager;
                Entity clientDeterministicSettingsEntity = clientEntityManager.CreateEntity(typeof(DeterministicSettings));
                
                var clientSettings = entityManager.GetComponentData<DeterministicSettings>(deterministicSettings);
                clientSettings._serverAddress = Address.text;
                clientSettings._serverPort = ushort.Parse(Port.text);
                clientEntityManager.SetComponentData(clientDeterministicSettingsEntity,
                    clientSettings);
            }
            else
            {
                Debug.LogError(
                    "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
            }

            // DestroyLocalSimulationWorld();
            World.DefaultGameObjectInjectionWorld = client;
        }

        public void ConnectToGame()
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery deterministicSettingsQuery = entityManager.CreateEntityQuery(typeof(DeterministicSettings));

            Debug.Log($"[ConnectToServer]");
            var client = CreateClientWorld("ClientWorld");

            if (deterministicSettingsQuery.TryGetSingletonEntity<DeterministicSettings>(
                    out Entity deterministicSettings))
            {
                EntityManager clientEntityManager = client.EntityManager;
                Entity clientDeterministicSettingsEntity = clientEntityManager.CreateEntity(typeof(DeterministicSettings));
                
                var clientSettings = entityManager.GetComponentData<DeterministicSettings>(deterministicSettings);
                clientSettings._serverAddress = Address.text;
                clientSettings._serverPort = ushort.Parse(Port.text);
                clientEntityManager.SetComponentData(clientDeterministicSettingsEntity,
                    clientSettings);
            }
            else
            {
                Debug.LogError(
                    "No DeterministicSettings entity found in the world. Make sure you have a DeterministicSettings entity in your world.");
            }

            SceneManager.LoadScene("PongLoading");

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