using UnityEngine;

namespace DeterministicLockstep.Samples
{
    /// <summary>
    /// Example MonoBehaviour demonstrating full-game validation.
    /// </summary>
    public class GameValidationExample : MonoBehaviour
    {
        [Header("Validation Settings")]
        [SerializeField] private int numberOfRuns = 2;
        [SerializeField] private int ticksToSimulate = 500;
        [SerializeField] private uint randomSeed = 12345;
        [SerializeField] private bool exportLogs = true;
        
        [Header("Session Recording")]
        [SerializeField] private bool recordSession = false;
        [SerializeField] private string sessionFilename = "recorded_session.json";
        
        private void Start()
        {
            // Subscribe to validation events
            if (GameValidator.Instance != null)
            {
                GameValidator.Instance.OnValidationComplete += OnValidationComplete;
                GameValidator.Instance.OnNondeterminismDetected += OnNondeterminismDetected;
                GameValidator.Instance.OnRunComplete += OnRunComplete;
            }
            
            if (SessionReplayer.Instance != null)
            {
                SessionReplayer.Instance.OnReplayComplete += OnReplayComplete;
            }
        }
        
        private void OnDestroy()
        {
            if (GameValidator.Instance != null)
            {
                GameValidator.Instance.OnValidationComplete -= OnValidationComplete;
                GameValidator.Instance.OnNondeterminismDetected -= OnNondeterminismDetected;
                GameValidator.Instance.OnRunComplete -= OnRunComplete;
            }
            
            if (SessionReplayer.Instance != null)
            {
                SessionReplayer.Instance.OnReplayComplete -= OnReplayComplete;
            }
        }
        
        [ContextMenu("Start Validation")]
        public void StartValidation()
        {
            Debug.Log("--- Starting Game Validation ---");
            
            var config = new ValidationConfig
            {
                numberOfRuns = numberOfRuns,
                ticksToSimulate = ticksToSimulate,
                randomSeed = randomSeed,
                exportLogs = exportLogs
            };
            
            GameValidator.Instance?.StartValidation(config);
        }
        
        [ContextMenu("Stop Validation")]
        public void StopValidation()
        {
            GameValidator.Instance?.StopValidation();
        }
        
        [ContextMenu("Record Session")]
        public void RecordSession()
        {
            if (recordSession && SessionRecorder.Instance != null)
            {
                SessionRecorder.Instance.StartRecording(randomSeed: randomSeed);
                Debug.Log("Started recording session...");
            }
        }
        
        [ContextMenu("Stop Recording and Save")]
        public void StopRecordingAndSave()
        {
            if (SessionRecorder.Instance != null && SessionRecorder.Instance.IsRecording)
            {
                var session = SessionRecorder.Instance.StopRecording();
                SessionRecorder.Instance.SaveSession(session, sessionFilename);
            }
        }
        
        [ContextMenu("Export Hash Log")]
        public void ExportHashLog()
        {
            if (SessionRecorder.Instance != null && !SessionRecorder.Instance.IsRecording)
            {
                // If we have a recorded session, export it
                Debug.Log("Use 'Stop Recording and Save' first to save a session.");
            }
        }
        
        private void OnValidationComplete(GameValidationResult result)
        {
            if (result.nondeterminismDetected)
            {
                Debug.LogError($"VALIDATION FAILED at tick {result.firstNondeterministicTick}");
            }
            else
            {
                Debug.Log($"VALIDATION PASSED - {result.numberOfRuns} runs, {result.ticksSimulated} ticks each");
            }
        }
        
        private void OnNondeterminismDetected(int tick, int runIndex, ulong expected, ulong actual)
        {
            Debug.LogWarning($"Nondeterminism detected at tick {tick} (run {runIndex + 1})");
            Debug.LogWarning($"Expected: {expected:X16}, Got: {actual:X16}");
        }
        
        private void OnRunComplete(int currentRun, int totalRuns)
        {
            Debug.Log($"Completed run {currentRun}/{totalRuns}");
        }
        
        private void OnReplayComplete(ReplayValidationResult result)
        {
            if (result.matchesOriginal)
            {
                Debug.Log($"REPLAY MATCHED - {result.totalTicksReplayed} ticks validated");
            }
            else
            {
                Debug.LogError($"REPLAY MISMATCH - First difference at tick {result.firstMismatchTick}");
            }
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            
            GUILayout.Label("Determinism Validation", GUI.skin.box);
            
            if (GameValidator.Instance != null)
            {
                if (GameValidator.Instance.IsValidating)
                {
                    GUILayout.Label($"Validating... Run {GameValidator.Instance.CurrentRunIndex + 1}/{numberOfRuns}");
                    
                    if (GUILayout.Button("Stop Validation"))
                    {
                        StopValidation();
                    }
                }
                else
                {
                    if (GUILayout.Button("Start Validation"))
                    {
                        StartValidation();
                    }
                }
            }
            
            GUILayout.Space(10);
            
            if (SessionRecorder.Instance != null)
            {
                if (SessionRecorder.Instance.IsRecording)
                {
                    GUILayout.Label("Recording...");
                    if (GUILayout.Button("Stop & Save"))
                    {
                        StopRecordingAndSave();
                    }
                }
                else
                {
                    if (GUILayout.Button("Start Recording"))
                    {
                        RecordSession();
                    }
                }
            }
            
            GUILayout.EndArea();
        }
    }
}

