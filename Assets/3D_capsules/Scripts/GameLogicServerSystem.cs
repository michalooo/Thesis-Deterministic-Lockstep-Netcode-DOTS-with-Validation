using DeterministicLockstep;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;

[WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
public partial class GameLogicServerSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // option for the server to start the game
        if (SceneManager.GetActiveScene().name == "Loading" && Input.GetKey(KeyCode.Space))
        {
            Entity settings = SystemAPI.GetSingletonEntity<DeterministicSettings>();
            SystemAPI.SetComponentEnabled<DeterministicServerListen>(settings, false);
            SystemAPI.SetComponentEnabled<DeterministicServerRunSimulation>(settings, true);
        }
    }
}