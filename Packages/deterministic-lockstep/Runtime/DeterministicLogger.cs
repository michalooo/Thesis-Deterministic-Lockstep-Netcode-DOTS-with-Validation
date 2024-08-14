using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Logging;
using Unity.Logging.Sinks;
using UnityEngine;
using Logger = Unity.Logging.Logger;
using Random = System.Random;

namespace DeterministicLockstep
{
    public class DeterministicLogger : MonoBehaviour
    {
        public static DeterministicLogger Instance { get; private set; }
        
        private StreamWriter _nondeterminismInfoClientLogger; // 2 loggers in case of local client testing
        private StreamWriter _nondeterminismInfoClientLogger2;
        private bool _isNondeterminismClientLoggerInitialized = false;
        private bool _isNondeterminismClientLoggerInitialized2 = false;
        
        private StreamWriter _serverInputRecordingLogger;
        private bool _isServerInputRecordingLoggerInitialized = false;
        
        private StreamWriter _clientSettingsLogger; // 2 loggers in case of local client testing
        private StreamWriter _clientSettingsLogger2;
        private bool _isClientSettingsLoggerInitialized = false;
        private bool _isClientSettingsLoggerInitialized2 = false;
        
        private StreamWriter _clientSystemInfoLogger; // 2 loggers in case of local client testing
        private StreamWriter _clientSystemInfoLogger2;
        private bool _isClientSystemInfoLoggerInitialized = false;
        private bool _isClientSystemInfoLoggerInitialized2 = false;
        
        /// <summary>
        /// Maximum batch size for logging to avoid writing too much data at once.
        /// Value is given in bytes
        /// </summary>
        const int MaxLoggingBatchSize = 500; // TODO: investigate optimal value for speed

        /// <summary>
        /// Information about the game state after hashing
        /// </summary>
        private Dictionary<ulong, List<string>> _clientHashInfoBuffer;
        
        /// <summary>
        /// Information about the game state after hashing for the second client.
        /// This is used for local simulation only.
        /// </summary>
        private Dictionary<ulong, List<string>> _clientHashInfoBuffer2;
        
        private int _deterministicEntityIDInDictionary = -1;
        private int _deterministicEntityIDInDictionary2 = -1; // For local client testing
        
        public int GetDeterministicEntityID(string worldName) // TODO: remove the problems with separation on 2 client worlds
        {
            if(worldName == "ClientWorld")
            {
                _deterministicEntityIDInDictionary++;
                return _deterministicEntityIDInDictionary;
            }
            
            _deterministicEntityIDInDictionary2++;
            return _deterministicEntityIDInDictionary2;
        }

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

            _clientHashInfoBuffer = new Dictionary<ulong, List<string>>();
            _clientHashInfoBuffer2 = new Dictionary<ulong, List<string>>();
        }
        
        /// <summary>
        /// Function which adds the message to the dictionary for the client.
        /// This dictionary will be used when nondeterminism is detected to print all of the messages to the log file.
        /// </summary>
        /// <param name="worldName"> The name of the world, used in order to add the message to aproperiate dictionary </param>
        /// <param name="tick"> On which simulation tick is this message </param>
        /// <param name="message"> What text should be added to the dictionary </param>
        public void AddToClientHashDictionary(string worldName, ulong tick, string message)
        {
            var dictionaryToWrite = worldName == "ClientWorld" ? _clientHashInfoBuffer : _clientHashInfoBuffer2;
            
            if (dictionaryToWrite.ContainsKey(tick))
            {
                dictionaryToWrite[tick].Add(message);
            }
            else
            {
                dictionaryToWrite.Add(tick, new List<string>());
                dictionaryToWrite[tick].Add(message);
            }
        }
        
        /// <summary>
        /// Function which creates the logger for the client nondeterminism info.
        /// </summary>
        private void CreateNondeterminismClientLogger(string worldName, DeterministicSettings settings)
        {
            var randomHashForAGameWithGivenSeed = new Random((int)settings.randomSeed).Next();
            
            if (worldName == "ClientWorld")
            {
                if (_isNondeterminismClientLoggerInitialized) return;
                var directoryPath = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                    DateTime.Now.Month + "_" +
                                    DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                    "_" + randomHashForAGameWithGivenSeed;
                Directory.CreateDirectory(directoryPath);
                var nonDeterminismLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                                   DateTime.Now.Month + "_" +
                                                   DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                                   "_" + randomHashForAGameWithGivenSeed + "/_NondeterminismClientLogs_.txt";
                _nondeterminismInfoClientLogger = new StreamWriter(nonDeterminismLoggerFileName, true);
                
                _isNondeterminismClientLoggerInitialized = true;
            }
            else
            {
                if (_isNondeterminismClientLoggerInitialized2) return;
                var directoryPath = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                    DateTime.Now.Month + "_" +
                                    DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                    "_" + randomHashForAGameWithGivenSeed;
                Directory.CreateDirectory(directoryPath);
                var nonDeterminismLoggerFileName2 = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                                    DateTime.Now.Month + "_" +
                                                    DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                                    "_" + randomHashForAGameWithGivenSeed + "/_NondeterminismClientLogs2_.txt";
            
                _nondeterminismInfoClientLogger2 = new StreamWriter(nonDeterminismLoggerFileName2, true);
                
                _isNondeterminismClientLoggerInitialized2 = true;
            }
        }
        
        /// <summary>
        /// Function which creates the logger for the client system info.
        /// </summary>
        private void CreateClientSystemInfoLogger(string worldName, DeterministicSettings settings)
        {
            var randomHashForAGameWithGivenSeed = new Random((int)settings.randomSeed).Next();
            
            if (worldName == "ClientWorld")
            {
                if(_isClientSystemInfoLoggerInitialized) return;
                
                var systemInfoLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                               DateTime.Now.Month + "_" +
                                               DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                               "_" + randomHashForAGameWithGivenSeed + "/_SystemInfo_.txt";
                _clientSystemInfoLogger = new StreamWriter(systemInfoLoggerFileName, true);
                
                _isClientSystemInfoLoggerInitialized = true;
            }
            else
            {
                if(_isClientSystemInfoLoggerInitialized2) return;
                
                var systemInfoLoggerFileName2 = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                                DateTime.Now.Month + "_" +
                                                DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                                "_" + randomHashForAGameWithGivenSeed + "/_SystemInfo2_.txt";
            
                _clientSystemInfoLogger2 = new StreamWriter(systemInfoLoggerFileName2, true);
                
                _isClientSystemInfoLoggerInitialized2 = true;
            }
        }

        /// <summary>
        /// Function which creates the logger for the server input recording.
        /// </summary>
        private void CreateServerInputRecordingLogger(DeterministicSettings settings)
        {
            var randomHashForAGameWithGivenSeed = new Random((int)settings.randomSeed).Next();
            
            if(_isServerInputRecordingLoggerInitialized) return;

            var directoryPath = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                DateTime.Now.Month + "_" +
                                DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                "_" + randomHashForAGameWithGivenSeed;
            Directory.CreateDirectory(directoryPath);
            
            var serverInputRecordingLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                      DateTime.Now.Month + "_" +
                                      DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                      "_" + randomHashForAGameWithGivenSeed + "/_ServerInputRecording_.txt";
            _serverInputRecordingLogger = new StreamWriter(serverInputRecordingLoggerFileName, true);
            
            _isServerInputRecordingLoggerInitialized = true;
        }
        
        /// <summary>
        /// Function which creates the logger for the client game settings.
        /// </summary>
        private void CreateClientSettingsLogger(string worldName, DeterministicSettings settings)
        {
            var randomHashForAGameWithGivenSeed = new Random((int)settings.randomSeed).Next();
            
            if (worldName == "ClientWorld")
            {
                if(_isClientSettingsLoggerInitialized) return;
                var directoryPath = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                    DateTime.Now.Month + "_" +
                                    DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                    "_" + randomHashForAGameWithGivenSeed;
                Directory.CreateDirectory(directoryPath);
                var clientSettingsLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                                   DateTime.Now.Month + "_" +
                                                   DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                                   "_" + randomHashForAGameWithGivenSeed + "/_ClientGameSettings_.txt";
                _clientSettingsLogger = new StreamWriter(clientSettingsLoggerFileName, true);
                
                _isClientSettingsLoggerInitialized = true;
            }
            else
            {
                if(_isClientSettingsLoggerInitialized2) return;
                var directoryPath = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                    DateTime.Now.Month + "_" +
                                    DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                    "_" + randomHashForAGameWithGivenSeed;
                Directory.CreateDirectory(directoryPath);
                var clientSettingsLoggerFileName2 = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                                    DateTime.Now.Month + "_" +
                                                    DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                                    "_" + randomHashForAGameWithGivenSeed + "/_ClientGameSettings2_.txt";
                _clientSettingsLogger2 = new StreamWriter(clientSettingsLoggerFileName2, true);
                
                _isClientSettingsLoggerInitialized2 = true;
            }
        }
        
        /// <summary>
        /// Function which saves client determinism related settings to the file.
        /// </summary>
        /// <param name="settings"> Game determinism related setting stored in DeterministicSettings component </param>
        public void LogClientSettingsToTheFile(string worldName, DeterministicSettings settings)
        {
            CreateClientSettingsLogger(worldName, settings); 
            var loggerToUse = worldName == "ClientWorld" ? _clientSettingsLogger : _clientSettingsLogger2;
            
            var jsonOutput = JsonUtility.ToJson(settings, true);
            loggerToUse.Write(jsonOutput);
            loggerToUse.Flush();
            // Log.Logger = loggerToUse;
            // Log.Info(jsonOutput);
            // Log.FlushAll();
        }
        
        /// <summary>
        /// Function which saves client system info to the file.
        /// </summary>
        public void LogSystemInfoToTheFile(string worldName, DeterministicSettings settings)
        {
            CreateClientSystemInfoLogger(worldName, settings);
            var loggerToUse = worldName == "ClientWorld" ? _clientSystemInfoLogger : _clientSystemInfoLogger2;
            
            // Log.Logger = loggerToUse;
            loggerToUse.Write("Operating System: " + SystemInfo.operatingSystem);
            loggerToUse.Flush();
            loggerToUse.Write("Processor: " + SystemInfo.processorType + " with " + SystemInfo.processorCount + " cores");
            loggerToUse.Flush();
            loggerToUse.Write("GPU: " + SystemInfo.graphicsDeviceName + ", VRAM: " + SystemInfo.graphicsMemorySize + " MB");
            loggerToUse.Flush();
            loggerToUse.Write("RAM: " + SystemInfo.systemMemorySize + " MB");
            loggerToUse.Flush();
            loggerToUse.Write("Screen Resolution: " + Screen.currentResolution.width + "x" + Screen.currentResolution.height);
            loggerToUse.Flush();
        }
        
        /// <summary>
        /// Function which logs all of the inputs which were send to the server to the file.
        /// Those are not simple inputs but rather a full RPC`s messages that clients were sending to the server.
        /// This form of storage allows for easy replay of the game.
        /// </summary>
        /// <param name="serverInputRecording">NativeList containing all of the RPC`s with client input which were send to the server</param>
        public void LogServerInputRecordingToTheFile(NativeList<RpcBroadcastTickDataToClients> serverInputRecording, DeterministicSettings settings)
        {
            CreateServerInputRecordingLogger(settings);
            // Log.Logger = _serverInputRecordingLogger;
            
            foreach (var rpc in serverInputRecording)
            {
                var tempSerializableRpc = new SerializableRpcBroadcastTickDataToClients
                {
                    networkIDsOfAllClients = new List<int>(),
                    gameInputsFromAllClients = new List<PongInputs>(),
                    simulationTick = rpc.SimulationTick
                };
                
                foreach (var clientNetworkID in rpc.NetworkIDsOfAllClients)
                {
                    tempSerializableRpc.networkIDsOfAllClients.Add(clientNetworkID);
                }

                foreach (var gameInput in rpc.GameInputsFromAllClients)
                {
                    tempSerializableRpc.gameInputsFromAllClients.Add(gameInput);
                }
                
                var jsonOutput = JsonUtility.ToJson(tempSerializableRpc, true);
                _serverInputRecordingLogger.Write(jsonOutput);
                _serverInputRecordingLogger.Flush();
            }
        }
        
        /// <summary>
        /// Function which is used to save the nondeterminism debug info to the file.
        /// </summary>
        /// <param name="message"></param>
        private void LogClientNondeterminismInfoToTheFile(string worldName, string message)
        {
            var loggerToUse = worldName == "ClientWorld" ? _nondeterminismInfoClientLogger : _nondeterminismInfoClientLogger2;
            // Log.Logger = loggerToUse;
            loggerToUse.Write(message);
            loggerToUse.Flush();
        }
        
        /// <summary>
        /// Serializable version of RpcBroadcastTickDataToClients struct
        /// </summary>
        [Serializable]
        public struct SerializableRpcBroadcastTickDataToClients
        {
            public List<int> networkIDsOfAllClients;
            public List<PongInputs> gameInputsFromAllClients;
            public int simulationTick;
        }
        
        /// <summary>
        /// Function that returns the list of RpcBroadcastTickDataToClients which were send from clients to the server.
        /// This allows for smooth replay of the game state based on those.
        /// The file needs to be placed under NonDeterminismLogs/_ServerInputRecording_.txt path
        /// </summary>
        /// <returns>List of RpcBroadcastTickDataToClients which were send from clients to the server</returns>
        public NativeList<RpcBroadcastTickDataToClients> ReadServerInputRecordingFromTheFile()
        {
            const string filePath = "NonDeterminismLogs/_ServerInputRecording_.txt";
            var listOfSerializableRPCs = new List<SerializableRpcBroadcastTickDataToClients>();
            
            using (var streamReader = new StreamReader(filePath))
            {
                var jsonBuilder = new StringBuilder();
                string jsonLine;
                while ((jsonLine = streamReader.ReadLine()) != null)
                {
                    jsonBuilder.Append(jsonLine);
                    
                    // Check if the line ends with a JSON object close. This is a temporary solution since when trying to parse entire file to the list at once an error is thrown and thus we need to divide it into smaller parts.
                    if (jsonLine == "}")
                    {
                        try
                        {
                            listOfSerializableRPCs.Add(JsonUtility.FromJson<SerializableRpcBroadcastTickDataToClients>(jsonBuilder.ToString()));
                            jsonBuilder.Clear();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Failed to parse JSON object: " + ex.Message);
                            jsonBuilder.Clear();
                        }
                    }
                }
            }
            
            NativeList<RpcBroadcastTickDataToClients> nativeListOfRpcBroadcastTickDataToClients = new NativeList<RpcBroadcastTickDataToClients>(Allocator.Persistent);
            foreach (var rpc in listOfSerializableRPCs)
            {
                RpcBroadcastTickDataToClients rpcBroadcastTickData = new RpcBroadcastTickDataToClients
                {
                    SimulationTick = rpc.simulationTick,
                    NetworkIDsOfAllClients = new NativeList<int>(rpc.networkIDsOfAllClients.Count, Allocator.Persistent),
                    GameInputsFromAllClients = new NativeList<PongInputs>(rpc.gameInputsFromAllClients.Count, Allocator.Persistent)
                };
                
                foreach (var clientNetworkID in rpc.networkIDsOfAllClients)
                {
                    rpcBroadcastTickData.NetworkIDsOfAllClients.Add(clientNetworkID);
                }

                foreach (var gameInput in rpc.gameInputsFromAllClients)
                {
                    rpcBroadcastTickData.GameInputsFromAllClients.Add(gameInput);
                }
                
                nativeListOfRpcBroadcastTickDataToClients.Add(rpcBroadcastTickData);
            }
            
            return nativeListOfRpcBroadcastTickDataToClients;
        }
        
        /// <summary>
        /// Function that returns the DeterministicSettings component which has values from the file.
        /// This allows for proper game replay based on the same settings.
        /// The file needs to be placed under NonDeterminismLogs/_ClientGameSettings_.txt path
        /// </summary>
        /// <returns>DeterministicSettings component which has values from the file</returns>
        public DeterministicSettings ReadSettingsFromFile()
        {
            const string filePath = "NonDeterminismLogs/_ClientGameSettings_.txt";
            var deterministicSettingsComponent = new DeterministicSettings();
            
            try
            {
                var jsonText = File.ReadAllText(filePath);
                deterministicSettingsComponent = JsonUtility.FromJson<DeterministicSettings>(jsonText);
            }
            catch (Exception exc)
            {
                Debug.LogError("Failed to read from file or parse JSON: " + exc.Message);
            }

            return deterministicSettingsComponent;
        }
        
        /// <summary>
        /// Function which logs the information about the client nondeterministic frame to a file
        /// </summary>
        /// <param name="nonDeterministicTick">Nondeterministic tick to log</param>
        public void LogClientNondeterministicTickInfoToTheFile(string worldName, ulong nonDeterministicTick, DeterministicSettings settings)
        {
            var logBuilder = new StringBuilder();
            var hashInfoBuffer = worldName == "ClientWorld" ? _clientHashInfoBuffer : _clientHashInfoBuffer2;
            CreateNondeterminismClientLogger(worldName, settings);

            if (settings.isReplayFromFile)
            {
                for (ulong i = 0; i <= nonDeterministicTick; i++)
                {
                    if (!hashInfoBuffer.TryGetValue(i, out var nondeterministicFrameInfo)) continue; //TODO: throw new Exception("No data to log for tick " + i);
            
                    logBuilder.AppendLine("Tick " + i);
                    foreach (var frameInfoLine in nondeterministicFrameInfo)
                    {
                        logBuilder.AppendLine(frameInfoLine);
                        
                        if (logBuilder.Length >= MaxLoggingBatchSize)
                        {
                            LogClientNondeterminismInfoToTheFile(worldName, logBuilder.ToString());
                            logBuilder.Clear();
                        }
                    }
                    if (logBuilder.Length > 0)
                    {
                        LogClientNondeterminismInfoToTheFile(worldName, logBuilder.ToString());
                    }
                }
            }
            else
            {
                if(!hashInfoBuffer.TryGetValue(nonDeterministicTick, out var nondeterministicFrameInfo)) throw new Exception("No data to log for nondeterministic tick " + nonDeterministicTick);
            
                logBuilder.AppendLine("Tick " + nonDeterministicTick);
                foreach (var frameInfoLine in nondeterministicFrameInfo)
                {
                    logBuilder.AppendLine(frameInfoLine);
                        
                    if (logBuilder.Length >= MaxLoggingBatchSize)
                    {
                        LogClientNondeterminismInfoToTheFile(worldName, logBuilder.ToString());
                        logBuilder.Clear();
                    }
                }
                if (logBuilder.Length > 0)
                {
                    LogClientNondeterminismInfoToTheFile(worldName, logBuilder.ToString());
                }
            }
        }
    }
}