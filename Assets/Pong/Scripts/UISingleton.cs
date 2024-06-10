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
        
        public static UISingleton Instance { get; private set; }
        
        private void Awake() 
        { 
            // If there is an instance, and it's not me, delete myself.
    
            if (Instance != null && Instance != this) 
            { 
                Destroy(this); 
            } 
            else 
            { 
                Instance = this; 
            } 
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