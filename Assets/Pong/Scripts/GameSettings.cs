using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PongGame
{
    public class GameSettings : MonoBehaviour 
    {
        [SerializeField] private int ballsToSpawn = 1000;
        [SerializeField] private int minBallSpeed = 2000;
        [SerializeField] private int maxBallSpeed = 5000;
        
        private int ballsSpawned = 0;
        
        public static GameSettings Instance { get; private set; }
        
        private void Awake() 
        { 
            if (Instance != null && Instance != this) 
            { 
                Destroy(this); 
            } 
            else 
            { 
                Instance = this; 
            } 
        }

        public int GetTotalBallsToSpawn()
        {
            return ballsToSpawn;
        }

        public void AddSpawnedBalls(int spawnedBalls)
        {
            ballsSpawned += spawnedBalls;
        }
        
        public int GetTotalBallsSpawned()
        {
            return ballsSpawned;
        }
        
        public int GetMinBallSpeed()
        {
            return minBallSpeed;
        }
        
        public int GetMaxBallSpeed()
        {
            return maxBallSpeed;
        }
    }
}