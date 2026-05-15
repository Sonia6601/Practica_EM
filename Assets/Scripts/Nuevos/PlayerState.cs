using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public NetworkVariable<FixedString64Bytes> SelectedCharacterName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log($"[PlayerState] ✓ OnNetworkSpawn() COMPLETADO - IsOwner: {IsOwner}, IsSpawned: {IsSpawned}, OwnerClientId: {OwnerClientId}");
    }

    private void Update()
    {
        // Debug: mostrar cambios de IsSpawned
        if (IsSpawned && !isReady.Value)
        {
            // Una vez spawneado, procesar acciones pendientes
        }
    }

    [Rpc(SendTo.Server)]
    public void SetReadyServerRpc(bool ready)
    {
        Debug.Log($"[PlayerState] ✓ SetReadyServerRpc recibido: {ready} del cliente {OwnerClientId}");
        isReady.Value = ready;
        GameManager.Instance?.CheckAllReady();
    }

    [Rpc(SendTo.Server)]
    public void SetCharacterServerRpc(FixedString64Bytes characterName)
    {
        Debug.Log($"[PlayerState] ✓ SetCharacterServerRpc recibido: {characterName} del cliente {OwnerClientId}");
        SelectedCharacterName.Value = characterName;
    }
}
