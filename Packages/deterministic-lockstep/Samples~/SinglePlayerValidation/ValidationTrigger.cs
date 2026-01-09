using UnityEngine;

namespace DeterministicLockstep.Samples
{
    /// <summary>
    /// Simple component to trigger single-player validation from the inspector.
    /// </summary>
    public class ValidationTrigger : MonoBehaviour
    {
        [Header("Validation Settings")]
        [Tooltip("Number of times to run the simulation for comparison.")]
        public int numberOfRuns = 2;
        
        [Tooltip("Number of ticks to simulate per run.")]
        public int ticksToSimulate = 1000;
        
        [Tooltip("Random seed for deterministic simulation.")]
        public uint randomSeed = 12345;
        
        [Header("Controls")]
        [Tooltip("Start validation on awake.")]
        public bool startOnAwake = false;
        
        [Header("Status")]
        [SerializeField] private string currentStatus = "Idle";
        [SerializeField] private int currentRun = 0;
        [SerializeField] private int totalRuns = 0;
        [SerializeField] private bool nondeterminismDetected = false;
        [SerializeField] private int nondeterministicTick = -1;
        
        private void Awake()
        {
            // Create validation manager if it doesn't exist
            if (SinglePlayerValidationManager.Instance == null)
            {
                var go = new GameObject("SinglePlayerValidationManager");
                go.AddComponent<SinglePlayerValidationManager>();
            }
        }
        
        private void Start()
        {
            // Subscribe to events
            SinglePlayerValidationManager.Instance.OnValidationComplete += OnValidationComplete;
            SinglePlayerValidationManager.Instance.OnRunComplete += OnRunComplete;
            SinglePlayerValidationManager.Instance.OnNondeterminismDetected += OnNondeterminismDetected;
            
            if (startOnAwake)
            {
                StartValidation();
            }
        }
        
        private void OnDestroy()
        {
            if (SinglePlayerValidationManager.Instance != null)
            {
                SinglePlayerValidationManager.Instance.OnValidationComplete -= OnValidationComplete;
                SinglePlayerValidationManager.Instance.OnRunComplete -= OnRunComplete;
                SinglePlayerValidationManager.Instance.OnNondeterminismDetected -= OnNondeterminismDetected;
            }
        }
        
        /// <summary>
        /// Start the validation process.
        /// </summary>
        [ContextMenu("Start Validation")]
        public void StartValidation()
        {
            var config = new ValidationConfig
            {
                numberOfRuns = numberOfRuns,
                ticksToSimulate = ticksToSimulate,
                randomSeed = randomSeed
            };
            
            totalRuns = numberOfRuns;
            currentRun = 0;
            nondeterminismDetected = false;
            nondeterministicTick = -1;
            currentStatus = "Starting...";
            
            SinglePlayerValidationManager.Instance.StartValidation(config);
        }
        
        /// <summary>
        /// Stop the current validation.
        /// </summary>
        [ContextMenu("Stop Validation")]
        public void StopValidation()
        {
            SinglePlayerValidationManager.Instance.StopValidation();
            currentStatus = "Stopped";
        }
        
        private void OnRunComplete(int run, int total)
        {
            currentRun = run;
            totalRuns = total;
            currentStatus = $"Run {run}/{total} complete";
        }
        
        private void OnNondeterminismDetected(int tick, int runIndex, ulong expectedHash, ulong actualHash)
        {
            nondeterminismDetected = true;
            nondeterministicTick = tick;
            currentStatus = $"NONDETERMINISM at tick {tick}!";
            
            Debug.LogError($"[ValidationTrigger] Nondeterminism detected!\n" +
                         $"  Tick: {tick}\n" +
                         $"  Run: {runIndex + 1}\n" +
                         $"  Expected hash: {expectedHash:X16}\n" +
                         $"  Actual hash: {actualHash:X16}");
        }
        
        private void OnValidationComplete(ValidationResult result)
        {
            if (result.nondeterminismDetected)
            {
                currentStatus = $"FAILED - Nondeterminism at tick {result.firstNondeterministicTick}";
                Debug.LogError($"[ValidationTrigger] Validation FAILED!\n" +
                             $"  First nondeterministic tick: {result.firstNondeterministicTick}\n" +
                             $"  Runs completed: {result.totalRuns}\n" +
                             $"  Ticks simulated: {result.ticksSimulated}");
            }
            else
            {
                currentStatus = "PASSED - All runs match!";
                Debug.Log($"[ValidationTrigger] Validation PASSED!\n" +
                         $"  Runs completed: {result.totalRuns}\n" +
                         $"  Ticks simulated: {result.ticksSimulated}\n" +
                         $"  All hashes matched across all runs.");
            }
        }
        
        private void Update()
        {
            // Update status from manager
            if (SinglePlayerValidationManager.Instance != null)
            {
                var state = SinglePlayerValidationManager.Instance.GetState();
                if (state == ValidationState.Running)
                {
                    currentRun = SinglePlayerValidationManager.Instance.GetCurrentRunIndex() + 1;
                }
            }
        }
    }
}

