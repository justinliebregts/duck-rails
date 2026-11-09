using System;
using UnityEngine;

// Run batch-only: the networking suites use isolated scenes and synthetic Photon rooms.
public static class PlayerArmRegressionChecks
{
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch checks only");
        PlayerArmLengthChecks.RunAll();
        PickupPoseChecks.Run();
        HeadPivotLifeNetworkChecks.Run();
        RagdollOwnerCollisionChecks.Run();
        HelicopterNetworkChecks.Run();
        // This existing suite exits the batch process with the final result.
        PhotonAvatarRecoveryChecks.Run();
    }
}
