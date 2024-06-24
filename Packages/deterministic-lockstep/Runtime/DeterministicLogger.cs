using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Logging;
using Unity.Logging.Sinks;
using UnityEngine;
using Logger = Unity.Logging.Logger;

namespace DeterministicLockstep
{
    public class DeterministicLogger : MonoBehaviour 
    { 
        public static DeterministicLogger Instance { get; private set; }
        private Logger determinismLogger;
        private Logger inputLogger;
        const int maxBatchSize = 200; // without this division I was getting errors regarding the batch size
        private bool isInputWritten = false;
        private bool isHashWritten = false;
        
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
        }

        public Dictionary<ulong, List<string>> GetHashDictionary()
        {
            return _tickHashBuffer;
        }

        public void AddToHashDictionary(ulong tick, string message)
        {
            // Check if the dictionary contains more than 9 keys
            if (_tickHashBuffer.Count > 9)
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
            var determinismLoggerFileName = "NonDeterminismLogs/_DeterminismLogs_" + DateTime.Now.Year + "_" + DateTime.Now.Month + "_" +
                                            DateTime.Now.Day + "_" + DateTime.Now.Hour + "_" + DateTime.Now.Minute + "_" + DateTime.Now.Second + ".log";
            determinismLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(determinismLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
        }

        public void CreateInputLogger()
        {
            var inputLoggerFileName = "NonDeterminismLogs/_ServerInputRecording_" + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" +
                                      DateTime.Now.Day + "____" + DateTime.Now.Hour + "-" + DateTime.Now.Minute + "-" + DateTime.Now.Second + ".log";
            inputLogger = new Logger(new LoggerConfig()
                .MinimumLevel.Debug()
                .OutputTemplate("{Message}")
                .WriteTo.File(inputLoggerFileName, minLevel: LogLevel.Verbose)
                .WriteTo.StdOut(outputTemplate: "{Message}"));
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
        
        public void LogInputsToFile(ulong nonDeterministicTick, Dictionary<ulong, NativeList<RpcBroadcastPlayerTickDataToServer>> _everyTickInputBuffer)
        {
           if(isInputWritten) return;
           
           isInputWritten = true;
            var logBuilder = new System.Text.StringBuilder();
            foreach (var tickEntry in _everyTickInputBuffer)
            {
                ulong tick = tickEntry.Key;
                NativeList<RpcBroadcastPlayerTickDataToServer> inputDataList = tickEntry.Value;
            
                if (nonDeterministicTick < tick) return;
                
                logBuilder.AppendLine("Tick " + tick);
                for (int i = 0; i < inputDataList.Length; i++)
                {
                    var inputData = inputDataList[i];
                    logBuilder.AppendLine("     PlayerID: " + inputData.PlayerNetworkID + " Inputs: " + inputData.PongGameInputs.verticalInput);
                        
                    if (logBuilder.Length >= maxBatchSize)
                    {
                        // Log the current batch
                        LogInput(logBuilder.ToString());
                        // Clear the log builder for the next batch
                        logBuilder.Clear();
                    }
                }
            }
            if (logBuilder.Length > 0)
            {
                LogInput(logBuilder.ToString());
            }
        }
        
        public void LogHashesToFile(ulong nonDeterministicTick)
        {
            if (isHashWritten) return;
            
            Debug.Log(isHashWritten);
            isHashWritten = true;
            var logBuilder = new System.Text.StringBuilder();
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