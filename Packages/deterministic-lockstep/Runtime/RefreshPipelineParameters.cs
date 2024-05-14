using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;

namespace DeterministicLockstep
{
    // public static class NetworkSimulatorSettings
    // {
    //     /// <summary>
    //     /// Function to refresh the simulation pipeline parameters during execution
    //     /// </summary>
    //     /// <param name="parameters"></param>
    //     /// <param name="driver"></param>
    //     public static void RefreshSimulationPipelineParametersLive(in SimulatorUtility.Parameters parameters,
    //         ref NetworkDriver driver)
    //     {
    //         var driverCurrentSettings = driver.CurrentSettings;
    //         var simParams = driverCurrentSettings.GetSimulatorStageParameters();
    //         simParams.Mode = parameters.Mode;
    //         simParams.PacketDelayMs = parameters.PacketDelayMs;
    //         simParams.PacketJitterMs = parameters.PacketJitterMs;
    //         simParams.PacketDropPercentage = 0; // // Set this to zero to avoid applying packet loss twice.
    //         simParams.PacketDropInterval = parameters.PacketDropInterval;
    //         simParams.PacketDuplicationPercentage = parameters.PacketDuplicationPercentage;
    //         simParams.FuzzFactor = parameters.FuzzFactor;
    //         simParams.FuzzOffset = parameters.FuzzOffset;
    //         driver.ModifySimulatorStageParameters(simParams);
    //
    //         // This new simulator has less features, but it does allow us to drop ALL packets (even low-level connection ones),
    //         // allowing us to test timeouts etc. Setting it instead of on the "light simulator".
    //         driver.ModifyNetworkSimulatorParameters(new NetworkSimulatorParameter
    //         {
    //             ReceivePacketLossPercent = parameters.PacketDropPercentage,
    //             SendPacketLossPercent = parameters.PacketDropPercentage,
    //         });
    //     }
    // }
}