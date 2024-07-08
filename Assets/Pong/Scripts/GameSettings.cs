using System;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// Settings for the Pong sample game.
    /// </summary>
    public class GameSettings : MonoBehaviour 
    {
        [Tooltip("The number of balls to spawn in the game.")]
        [SerializeField] private int ballsToSpawn = 1000;
        
        [Tooltip("The minimum speed of the ball.")]
        [SerializeField] private int minBallSpeed = 2000;
        
        [Tooltip("The maximum speed of the ball.")]
        [SerializeField] private int maxBallSpeed = 5000;
        
        /// <summary>
        /// Value indicating the leftmost screen position.
        /// </summary>
        public float LeftmostScreenPosition { get; private set; }
        
        /// <summary>
        /// Value indicating the rightmost screen position.
        /// </summary>
        public float RightmostScreenPosition { get; private set; }
        
        /// <summary>
        /// Value indicating the bottom screen position.
        /// </summary>
        public float BottomScreenPosition { get; private set; }
        
        /// <summary>
        /// Value indicating the top screen position.
        /// </summary>
        public float TopScreenPosition { get; private set; }
        
        /// <summary>
        /// Counter of how many balls were already spawned.
        /// </summary>
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

        private void Start()
        {
            Camera mainCamera = Camera.main;
            float halfHeight = mainCamera.orthographicSize;
            float halfWidth = mainCamera.aspect * halfHeight;

            LeftmostScreenPosition = -halfWidth;
            RightmostScreenPosition = halfWidth;
            BottomScreenPosition = -halfHeight;
            TopScreenPosition = halfHeight;
        }

        /// <summary>
        /// Function to get the total number of balls to spawn.
        /// </summary>
        /// <returns>Total number of balls to spawn in the span of the game</returns>
        public int GetTotalBallsToSpawn()
        {
            return ballsToSpawn;
        }

        /// <summary>
        /// Function that increments the number of balls spawned.
        /// </summary>
        /// <param name="spawnedBalls">How many balls were spawned</param>
        public void AddSpawnedBalls(int spawnedBalls)
        {
            ballsSpawned += spawnedBalls;
        }
        
        /// <summary>
        /// Function that returns the number of balls spawned.
        /// </summary>
        /// <returns>The number of spawned balls in the game</returns>
        public int GetTotalBallsSpawned()
        {
            return ballsSpawned;
        }
        
        /// <summary>
        /// Function that returns the minimum speed of the ball.
        /// </summary>
        /// <returns>Minimum speed of a ball in the game</returns>
        public int GetMinBallSpeed()
        {
            return minBallSpeed;
        }
        
        /// <summary>
        /// Function that returns the maximum speed of the ball.
        /// </summary>
        /// <returns>Maximum speed of a ball in the game</returns>
        public int GetMaxBallSpeed()
        {
            return maxBallSpeed;
        }
    }
}