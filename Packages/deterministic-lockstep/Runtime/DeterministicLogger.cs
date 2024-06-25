using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Logging.Sinks;
using UnityEngine;
using Logger = Unity.Logging.Logger;
using Unity.Entities.Serialization;

namespace DeterministicLockstep
{
    public class DeterministicLogger : MonoBehaviour
    {
        public static DeterministicLogger Instance { get; private set; }
        private Logger determinismLogger;
        private Logger inputLogger;
        private Logger settingsLogger;
        private Logger systemInfoLogger;
        const int maxBatchSize = 200; // without this division I was getting errors regarding the batch size
        private bool isInputWritten = false;
        private bool isHashWritten = false;
        private bool isSettingsWritten = false;
        private bool isSystemInfoWritten = false;

        private Dictionary<ulong, List<string>> _tickHashBuffer;

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

            _tickHashBuffer = new Dictionary<ulong, List<string>>();
            CreateInputLogger();
            CreateDeterminismLogger();
            CreateSettingsLogger();
            CreateSystemInfoLogger();
        }

        public Dictionary<ulong, List<string>> GetHashDictionary()
        {
            return _tickHashBuffer;
        }

        public void AddToHashDictionary(ulong tick, string message)
        {
            // Check if the dictionary contains more than 9 keys
            if (_tickHashBuffer.Count > 18)
            {
                // Find the lowest key
                ulong lowestKey = ulong.MaxValue;
                foreach (var key in _tickHashBuffer.Keys)
                {
                    if (key < lowestKey)
                    {
                        lowestKey = key;
                    }
                }

                // Remove the lowest key
                if (lowestKey != ulong.MaxValue)
                {
                    _tickHashBuffer.Remove(lowestKey);
                }
            }

            // Add the new message to the dictionary
            if (_tickHashBuffer.ContainsKey(tick))
            {
                _tickHashBuffer[tick].Add(message);
            }
            else
            {
                _tickHashBuffer.Add(tick, new List<string>());
                _tickHashBuffer[tick].Add(message);
            }
        }


        public void CreateDeterminismLogger()
        {
            var determinismLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                            DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                            "_" + DateTime.Now.Second + "/_DeterminismLogs_.txt";
            determinismLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }
        
        public void CreateSystemInfoLogger()
        {
            var determinismLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                            DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                            "_" + DateTime.Now.Second + "/_SystemInfo_.txt";
            systemInfoLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }

        public void CreateInputLogger()
        {
            var inputLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                      DateTime.Now.Month + "_" +
                                      DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                      "_" + DateTime.Now.Second + "/_ServerInputRecording_.txt";
            inputLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(inputLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }
        
        public void CreateSettingsLogger()
        {
            var inputLoggerFileName = "NonDeterminismLogs/" + DateTime.Now.Year + "_" +
                                      DateTime.Now.Month + "_" +
                                      DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute +
                                      "_" + DateTime.Now.Second + "/_ClientGameSettings_.txt";
            settingsLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(inputLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }
        
        public void LogSettings(string message)
        {
            Log.Logger = settingsLogger;
            Log.Info(message);
            Log.FlushAll();
        }
        
        public void LogSystemInfo()
        {
            Log.Logger = systemInfoLogger;
            Log.Info("Operating System: " + SystemInfo.operatingSystem);
            Log.Info("Processor: " + SystemInfo.processorType + " with " + SystemInfo.processorCount + " cores");
            Log.Info("GPU: " + SystemInfo.graphicsDeviceName + ", VRAM: " + SystemInfo.graphicsMemorySize + " MB");
            Log.Info("RAM: " + SystemInfo.systemMemorySize + " MB");
            Log.Info("Screen Resolution: " + Screen.currentResolution.width + "x" + Screen.currentResolution.height);
            Log.FlushAll();
        }
        

        public void LogInput(string message)
        {
            Log.Logger = inputLogger;
            Log.Info(message);
            Log.FlushAll();
        }

        public void LogHash(string message)
        {
            Log.Logger = determinismLogger;
            Log.Info(message);
            Log.FlushAll();
        }
        
        public void LogSettingsToFile(DeterministicSettings settings)
        {
            if (isSettingsWritten) return;
            isSettingsWritten = true;

            string testJsonOutput = JsonUtility.ToJson(settings, true);
            LogSettings(testJsonOutput);
            LogSystemInfo();
        }

        public void LogInputsToFile(NativeList<RpcBroadcastTickDataToClients> _serverDataToClients)
        {
            if (isInputWritten) return;
            isInputWritten = true;

            foreach (var rpc in _serverDataToClients)
            {
                TempRpcBroadcastTickDataToClients tempRpc = new TempRpcBroadcastTickDataToClients();
                tempRpc.NetworkIDs = new List<int>();
                tempRpc.PlayersPongGameInputs = new List<PongInputs>();
                tempRpc.SimulationTick = rpc.SimulationTick;
                foreach (var networkID in rpc.NetworkIDs)
                {
                    tempRpc.NetworkIDs.Add(networkID);
                }

                foreach (var pongInput in rpc.PlayersPongGameInputs)
                {
                    tempRpc.PlayersPongGameInputs.Add(pongInput);
                }
                
                string testJsonOutput = JsonUtility.ToJson(tempRpc, true);
                LogInput(testJsonOutput);
            }
        }

        [Serializable]
        public struct TempRpcBroadcastTickDataToClients
        {
            public List<PongInputs> PlayersPongGameInputs;
            public int SimulationTick;
            public List<int> NetworkIDs;
        }


        public NativeList<RpcBroadcastTickDataToClients> ReadTicksFromFile()
        {
            var filePath = "NonDeterminismLogs/_ServerInputRecordingToReplay_.txt";
            var test = new List<TempRpcBroadcastTickDataToClients>();
            
            using (var sr = new StreamReader(filePath))
            {
                StringBuilder jsonBuilder = new StringBuilder();
                string line;
                int counter = 0;
                while ((line = sr.ReadLine()) != null)
                {
                    jsonBuilder.Append(line);
                    counter++;
                    // Check if the line ends with a JSON object close (simple case)
                    if (counter == 15)
                    {
                        try
                        {
                            test.Add(JsonUtility.FromJson<TempRpcBroadcastTickDataToClients>(jsonBuilder.ToString()));
                            jsonBuilder.Clear(); // Clear the builder for the next JSON object
                            counter = 0;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Failed to parse JSON object: " + ex.Message);
                            // Optionally continue to try to read next object
                            jsonBuilder.Clear();
                            counter = 0;
                        }
                    }
                }
            }
            
            NativeList<RpcBroadcastTickDataToClients> rpcBroadcastTickDataToClients = new NativeList<RpcBroadcastTickDataToClients>(Allocator.Temp);
            foreach (var rpc in test)
            {
                RpcBroadcastTickDataToClients rpcBroadcastTickData = new RpcBroadcastTickDataToClients();
                rpcBroadcastTickData.SimulationTick = rpc.SimulationTick;
                rpcBroadcastTickData.NetworkIDs = new NativeList<int>(rpc.NetworkIDs.Count, Allocator.Persistent);
                rpcBroadcastTickData.PlayersPongGameInputs = new NativeList<PongInputs>(rpc.PlayersPongGameInputs.Count, Allocator.Persistent);
                foreach (var networkID in rpc.NetworkIDs)
                {
                    rpcBroadcastTickData.NetworkIDs.Add(networkID);
                }

                foreach (var pongInput in rpc.PlayersPongGameInputs)
                {
                    rpcBroadcastTickData.PlayersPongGameInputs.Add(pongInput);
                }
                rpcBroadcastTickDataToClients.Add(rpcBroadcastTickData);
            }
            
            return rpcBroadcastTickDataToClients;
        }
        
        public DeterministicSettings ReadSettingsFromFile()
        {
            var filePath = "NonDeterminismLogs/_ClientGameSettingsToReplay_.txt";
            var settings = new DeterministicSettings();
            try
            {
                // Read the entire file content at once
                string json = File.ReadAllText(filePath);
                settings = JsonUtility.FromJson<DeterministicSettings>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to read from file or parse JSON: " + ex.Message);
            }

            return settings;
        }




        public void LogHashesToFile(ulong nonDeterministicTick)
        {
            if (isHashWritten) return;
            
            isHashWritten = true;
            var logBuilder = new StringBuilder();
            foreach (var (tick, inputDataList) in GetHashDictionary())
            {
                if(nonDeterministicTick == tick)
                {
                    logBuilder.AppendLine("Tick " + tick);
                    for (int i = 0; i < inputDataList.Count; i++)
                    {
                        logBuilder.AppendLine(inputDataList[i]);
                        
                        if (logBuilder.Length >= maxBatchSize)
                        {
                            // Log the current batch
                            LogHash(logBuilder.ToString());
                            // Clear the log builder for the next batch
                            logBuilder.Clear();
                        }
                    }
                }
            }
            if (logBuilder.Length > 0)
            {
                LogHash(logBuilder.ToString());
            }
        }
    }
}