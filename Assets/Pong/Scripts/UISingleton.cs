using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace PongGame
{
    public class UISingleton : MonoBehaviour 
    {
        [SerializeField] private TextMeshProUGUI scoreLeft;
        [SerializeField] private TextMeshProUGUI scoreRight;
        [SerializeField] private TextMeshProUGUI gameStatusText;
        [SerializeField] private TextMeshProUGUI gameResultText;
        
        public static UISingleton Instance { get; private set; }
        
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
        }

        public bool IsLeftPlayerWinning()
        {
            return int.Parse(scoreLeft.text) > int.Parse(scoreRight.text);
        }
        
        public bool IsRightPlayerWinning()
        {
            return int.Parse(scoreLeft.text) < int.Parse(scoreRight.text);
        }
        
        public bool IsDraw()
        {
            return int.Parse(scoreLeft.text) == int.Parse(scoreRight.text);
        }

        public int GetTotalScore()
        {
            return int.Parse(scoreLeft.text) + int.Parse(scoreRight.text);
        }

        public void SetGameResult(bool leftPlayerWon)
        {
            if (leftPlayerWon)
            {
                gameResultText.text = "Left player won!";
            }
            else
            {
                gameResultText.text = "Right player won!";
            }
            gameResultText.gameObject.SetActive(true);
        }

        public void AddLeftScore(int score)
        {
            scoreLeft.text = (int.Parse(scoreLeft.text) + score).ToString();
        }
        
        public void AddRightScore(int score)
        {
            scoreRight.text = (int.Parse(scoreRight.text) + score).ToString();
        }
        
        public void SetWaitingTextEnabled(bool enable)
        {
            gameStatusText.gameObject.SetActive(enable);
        }
        
    }
}