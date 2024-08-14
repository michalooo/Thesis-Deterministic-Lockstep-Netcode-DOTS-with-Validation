using TMPro;
using UnityEngine;

namespace PongGame
{
    /// <summary>
    /// Class that manages the UI elements of the game.
    /// </summary>
    public class GameManagerSingleton : MonoBehaviour 
    {
        [SerializeField] private TextMeshProUGUI leftPlayerScoreText;
        [SerializeField] private TextMeshProUGUI rightPlayerScoreText;
        [SerializeField] private TextMeshProUGUI gameWaitingStatusText;
        [SerializeField] private TextMeshProUGUI gameResultText;
        [SerializeField] private GameObject desyncMessage;
        
        public static GameManagerSingleton Instance { get; private set; }
        
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
            
            gameResultText.gameObject.SetActive(false);
            desyncMessage.SetActive(false);
        }
        
        /// <summary>
        /// Function to set the desync message enabled or disabled.
        /// </summary>
        /// <param name="enable">Value to use in SetActive call</param>
        public void SetDesyncMessageEnabled(bool enable)
        {
            desyncMessage.SetActive(enable);
        }

        /// <summary>
        /// Function that answer if the left player is currently winning.
        /// </summary>
        /// <returns>Bool signalling if left player is winning</returns>
        public bool IsLeftPlayerWinning()
        {
            return int.Parse(leftPlayerScoreText.text) > int.Parse(rightPlayerScoreText.text);
        }

        /// <summary>
        /// Function to get the total score of both players.
        /// </summary>
        /// <returns>Combined total score of both players</returns>
        public int GetTotalScore()
        {
            return int.Parse(leftPlayerScoreText.text) + int.Parse(rightPlayerScoreText.text);
        }

        /// <summary>
        /// Function to set the game result text.
        /// </summary>
        public void SetGameResult()
        {
            if (IsLeftPlayerWinning())
            {
                gameResultText.text = "Left player won!";
            }
            else
            {
                gameResultText.text = "Right player won!";
            }
            gameResultText.gameObject.SetActive(true);
        }

        /// <summary>
        /// Function to add score to the left player.
        /// </summary>
        /// <param name="score">Score to add</param>
        public void AddLeftScore(int score)
        {
            leftPlayerScoreText.text = (int.Parse(leftPlayerScoreText.text) + score).ToString();
        }
        
        /// <summary>
        /// Function to add score to the right player.
        /// </summary>
        /// <param name="score">Score to add</param>
        public void AddRightScore(int score)
        {
            rightPlayerScoreText.text = (int.Parse(rightPlayerScoreText.text) + score).ToString();
        }
        
        /// <summary>
        /// Function to set the game status text.
        /// </summary>
        /// <param name="enable">Value to use in SetActive call</param>
        public void SetWaitingTextEnabled(bool enable)
        {
            gameWaitingStatusText.gameObject.SetActive(enable);
        }
        
    }
}