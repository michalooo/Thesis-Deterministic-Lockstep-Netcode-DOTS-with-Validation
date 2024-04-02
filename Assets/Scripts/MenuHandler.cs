using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuHandler : MonoBehaviour
{

    [SerializeField] private Toggle Is2ClientSimulation;
    
    public void QuitGame()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
        Application.Quit();
    }

    public void HostGame()
    {
        Debug.Log($"[HostGame]");
        var server = CreateServerWorld("ServerWorld");
        var client = CreateClientWorld("ClientWorld");
        
        if(Is2ClientSimulation.isOn)
        {
            var client2 = CreateClientWorld("ClientWorld2");
        }
        
        SceneManager.LoadScene("Loading");
        
        DestroyLocalSimulationWorld();
        World.DefaultGameObjectInjectionWorld ??= client;
    }

    public void ConnectToGame()
    {
        Debug.Log($"[ConnectToServer]");
        var client = CreateClientWorld("ClientWorld");
        DestroyLocalSimulationWorld();
        
        SceneManager.LoadScene("Loading");
        
        World.DefaultGameObjectInjectionWorld ??= client;
    }
    
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
    
    public static World CreateClientWorld(string name)
    {
#if UNITY_SERVER && !UNITY_EDITOR
            throw new PlatformNotSupportedException("This executable was built using a 'server-only' build target (likely DGS). Thus, cannot create client worlds.");
#else
        var world = new World(name, WorldFlags.GameClient);

        var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.Presentation);
        DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
        ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

        if (World.DefaultGameObjectInjectionWorld == null)
            World.DefaultGameObjectInjectionWorld = world;
        
        return  world;
#endif
    }
    
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
