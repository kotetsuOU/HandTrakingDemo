using System;
using UnityEngine;

#nullable enable

public partial class HAP_AUTDController
{
    /// <summary>
    /// ハードウェア設定の変更監視を HAP_AUTDHardwareManager に委譲します。
    /// </summary>
    private void CheckForConfigChanges()
    {
        if (hardwareManager != null)
        {
            hardwareManager.CheckForConfigChanges();
        }
    }
}

