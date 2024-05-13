using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

[WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
public partial class GameLogicClientSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (SceneManager.GetActiveScene().name == "Game" && Input.GetKey(KeyCode.C) &&
            World.Name == "ClientWorld2") // Simulation of disconnection
        {
            Entity settings = SystemAPI.GetSingletonEntity<DeterministicSettings>();
            SystemAPI.SetComponentEnabled<DeterministicClientDisconnect>(settings, true);
        
            SceneManager.LoadScene("Loading");
        }
        
        foreach (var (settings, settingsEntity) in SystemAPI.Query<RefRW<DeterministicSettings>>().WithAll<DeterministicClientSendData>().WithEntityAccess())
        {
            if (SceneManager.GetActiveScene().name != "Game")
            {
                SceneManager.LoadScene("Game");
            }
        }
    }
}