using System;
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
        }
        
        public void LogHash(string message)
        {
            Log.Logger = determinismLogger;
            Log.Info(message);
        }
    }
}