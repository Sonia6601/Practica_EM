using Unity.Netcode;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Rpc(SendTo.Server)]
    public void SetReadyServerRpc(bool ready)
    {
        isReady.Value = ready;
        GameManager.Instance.CheckAllReady();
    }
}
