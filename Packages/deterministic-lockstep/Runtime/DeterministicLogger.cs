using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Logging;
using Unity.Logging.Sinks;
using UnityEngine;
using Logger = Unity.Logging.Logger;

namespace DeterministicLockstep
{
    public class DeterministicLogger : MonoBehaviour
    {
        public static DeterministicLogger Instance { get; private set; }
        
        private Logger nondeterminismInfoClientLogger; // 2 loggers in case of local client testing
        private Logger nondeterminismInfoClientLogger2;
        
        private Logger serverInputRecordingLogger;
        
        private Logger clientSettingsLogger; // 2 loggers in case of local client testing
        private Logger clientSettingsLogger2;
        
        private Logger clientSystemInfoLogger; // 2 loggers in case of local client testing
        private Logger clientSystemInfoLogger2;
        
        /// <summary>
        /// Maximum batch size for logging to avoid writing too much data at once.
        /// Value is given in bytes
        /// </summary>
        const int maxBatchSize = 500;

        /// <summary>
        /// Information about the game state after hashing
        /// </summary>
        private Dictionary<ulong, List<string>> clientHashInfoBuffer;
        
        /// <summary>
        /// Information about the game state after hashing for the second client.
        /// This is used for local simulation only.
        /// </summary>
        private Dictionary<ulong, List<string>> clientHashInfoBuffer2;

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

            clientHashInfoBuffer = new Dictionary<ulong, List<string>>();
            clientHashInfoBuffer2 = new Dictionary<ulong, List<string>>();
            
            CreateClientSettingsLogger();
            CreateNondeterminismClientLogger();
            CreateClientSystemInfoLogger();
            CreateServerInputRecordingLogger();
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
            var dictionaryToWrite = worldName == "ClientWorld" ? clientHashInfoBuffer : clientHashInfoBuffer2;
            
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
        private void CreateNondeterminismClientLogger()
        {
            var determinismLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                            DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                            "_" + DateTime.Now.Second + "/_NondeterminismClientLogs_.txt";
            nondeterminismInfoClientLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
            
            var determinismLoggerFileName2 = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                            DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                            "_" + DateTime.Now.Second + "/_NondeterminismClientLogs2_.txt";
            nondeterminismInfoClientLogger2 = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }
        
        /// <summary>
        /// Function which creates the logger for the client system info.
        /// </summary>
        private void CreateClientSystemInfoLogger()
        {
            var determinismLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                            DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                            "_" + DateTime.Now.Second + "/_SystemInfo_.txt";
            clientSystemInfoLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
            
            var determinismLoggerFileName2 = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                            DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                            "_" + DateTime.Now.Second + "/_SystemInfo2_.txt";
            clientSystemInfoLogger2 = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }

        /// <summary>
        /// Function which creates the logger for the server input recording.
        /// </summary>
        private void CreateServerInputRecordingLogger()
        {
            var inputLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                      DateTime.Now.Month + "_" +
                                      DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                      "_" + DateTime.Now.Second + "/_ServerInputRecording_.txt";
            serverInputRecordingLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(inputLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }
        
        /// <summary>
        /// Function which creates the logger for the client game settings.
        /// </summary>
        private void CreateClientSettingsLogger()
        {
            var inputLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                      DateTime.Now.Month + "_" +
                                      DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                      "_" + DateTime.Now.Second + "/_ClientGameSettings_.txt";
            clientSettingsLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(inputLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
            
            var inputLoggerFileName2 = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                      DateTime.Now.Month + "_" +
                                      DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                      "_" + DateTime.Now.Second + "/_ClientGameSettings2_.txt";
            clientSettingsLogger2 = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(inputLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }
        
        /// <summary>
        /// Function which saves client determinism related settings to the file.
        /// </summary>
        /// <param name="settings"> Game determinism related setting stored in DeterministicSettings component </param>
        public void LogClientSettingsToTheFile(string worldName, DeterministicSettings settings)
        {
            var loggerToUse = worldName == "ClientWorld" ? clientSettingsLogger : clientSettingsLogger2;
            
            string jsonOutput = JsonUtility.ToJson(settings, true);
            Log.Logger = loggerToUse;
            Log.Info(jsonOutput);
            Log.FlushAll();
        }
        
        /// <summary>
        /// Function which saves client system info to the file.
        /// </summary>
        public void LogSystemInfoToTheFile(string worldName)
        {
            var loggerToUse = worldName == "ClientWorld" ? clientSystemInfoLogger : clientSystemInfoLogger2;
            
            Log.Logger = loggerToUse;
            Log.Info("Operating System: " + SystemInfo.operatingSystem);
            Log.Info("Processor: " + SystemInfo.processorType + " with " + SystemInfo.processorCount + " cores");
            Log.Info("GPU: " + SystemInfo.graphicsDeviceName + ", VRAM: " + SystemInfo.graphicsMemorySize + " MB");
            Log.Info("RAM: " + SystemInfo.systemMemorySize + " MB");
            Log.Info("Screen Resolution: " + Screen.currentResolution.width + "x" + Screen.currentResolution.height);
            Log.FlushAll();
        }
        
        /// <summary>
        /// Function which logs all of the inputs which were send to the server to the file.
        /// Those are not simple inputs but rather a full RPC`s messages that clients were sending to the server.
        /// This form of storage allows for easy replay of the game.
        /// </summary>
        /// <param name="serverInputRecording">NativeList containing all of the RPC`s with client input which were send to the server</param>
        public void LogServerInputRecordingToTheFile(NativeList<RpcBroadcastTickDataToClients> serverInputRecording)
        {
            Log.Logger = serverInputRecordingLogger;
            
            foreach (var rpc in serverInputRecording)
            {
                var tempRpc = new SerializableRpcBroadcastTickDataToClients
                {
                    NetworkIDsOfAllClients = new List<int>(),
                    GameInputsFromAllClients = new List<PongInputs>(),
                    SimulationTick = rpc.SimulationTick
                };
                
                foreach (var clientNetworkID in rpc.NetworkIDsOfAllClients)
                {
                    tempRpc.NetworkIDsOfAllClients.Add(clientNetworkID);
                }

                foreach (var gameInput in rpc.GameInputsFromAllClients)
                {
                    tempRpc.GameInputsFromAllClients.Add(gameInput);
                }
                
                string jsonOutput = JsonUtility.ToJson(tempRpc, true);
                Log.Info(jsonOutput);
                Log.FlushAll();
            }
        }
        
        /// <summary>
        /// Function which is used to save the nondeterminism debug info to the file.
        /// </summary>
        /// <param name="message"></param>
        private void LogClientNondeterminismInfoToTheFile(string worldName, string message)
        {
            var loggerToUse = worldName == "ClientWorld" ? nondeterminismInfoClientLogger : nondeterminismInfoClientLogger2;
            Log.Logger = loggerToUse;
            Log.Info(message);
            Log.FlushAll();
        }
        
        /// <summary>
        /// Serializable version of RpcBroadcastTickDataToClients struct
        /// </summary>
        [Serializable]
        public struct SerializableRpcBroadcastTickDataToClients
        {
            public List<int> NetworkIDsOfAllClients;
            public List<PongInputs> GameInputsFromAllClients;
            public int SimulationTick;
        }
        
        /// <summary>
        /// Function that returns the list of RpcBroadcastTickDataToClients which were send from clients to the server.
        /// This allows for smooth replay of the game state based on those.
        /// The file needs to be placed under NonDeterminismLogs/_ServerInputRecording_.txt path
        /// </summary>
        /// <returns>List of RpcBroadcastTickDataToClients which were send from clients to the server</returns>
        public NativeList<RpcBroadcastTickDataToClients> ReadServerInputRecordingFromTheFile()
        {
            var filePath = "NonDeterminismLogs/_ServerInputRecording_.txt";
            var listOfSerializableRPC = new List<SerializableRpcBroadcastTickDataToClients>();
            
            using (var sr = new StreamReader(filePath))
            {
                StringBuilder jsonBuilder = new StringBuilder();
                string jsonLine;
                while ((jsonLine = sr.ReadLine()) != null)
                {
                    jsonBuilder.Append(jsonLine);
                    
                    // Check if the line ends with a JSON object close. This is a temporary solution since when trying to parse entire file to the list at once an error is thrown and thus we need to divide it into smaller parts.
                    if (jsonLine == "}")
                    {
                        try
                        {
                            listOfSerializableRPC.Add(JsonUtility.FromJson<SerializableRpcBroadcastTickDataToClients>(jsonBuilder.ToString()));
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
            foreach (var rpc in listOfSerializableRPC)
            {
                RpcBroadcastTickDataToClients rpcBroadcastTickData = new RpcBroadcastTickDataToClients
                {
                    SimulationTick = rpc.SimulationTick,
                    NetworkIDsOfAllClients = new NativeList<int>(rpc.NetworkIDsOfAllClients.Count, Allocator.Persistent),
                    GameInputsFromAllClients = new NativeList<PongInputs>(rpc.GameInputsFromAllClients.Count, Allocator.Persistent)
                };
                
                foreach (var clientNetworkID in rpc.NetworkIDsOfAllClients)
                {
                    rpcBroadcastTickData.NetworkIDsOfAllClients.Add(clientNetworkID);
                }

                foreach (var gameInput in rpc.GameInputsFromAllClients)
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
            var filePath = "NonDeterminismLogs/_ClientGameSettings_.txt";
            var deterministicSettingsComponent = new DeterministicSettings();
            
            try
            {
                string json = File.ReadAllText(filePath);
                deterministicSettingsComponent = JsonUtility.FromJson<DeterministicSettings>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to read from file or parse JSON: " + ex.Message);
            }

            return deterministicSettingsComponent;
        }
        
        /// <summary>
        /// Function which logs the information about the client nondeterministic frame to a file
        /// </summary>
        /// <param name="nonDeterministicTick">Nondeterministic tick to log</param>
        public void LogClientNondeterministicTickInfoToTheFile(string worldName, ulong nonDeterministicTick)
        {
            var logBuilder = new StringBuilder();
            var hashInfoBuffer = worldName == "ClientWorld" ? clientHashInfoBuffer : clientHashInfoBuffer2;
            
            if(!hashInfoBuffer.TryGetValue(nonDeterministicTick, out var nondeterministicFrameInfo)) throw new Exception("No data to log for nondeterministic tick " + nonDeterministicTick);
            
            logBuilder.AppendLine("Tick " + nonDeterministicTick);
            foreach (var frameInfoLine in nondeterministicFrameInfo)
            {
                logBuilder.AppendLine(frameInfoLine);
                        
                if (logBuilder.Length >= maxBatchSize)
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