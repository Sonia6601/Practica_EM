using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    /// <summary>
    /// Esta clase almacena el estado del jugador y gestiona los datos 
    /// que se pasan entre el cliente y el servidor (esto es lo que se les pasa)
    /// </summary>
    /// 

    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
        ); // inicializacion de un booleano en red (se indican los permisos de lectura y escritura)
    
    public NetworkVariable<FixedString64Bytes> SelectedCharacterName = new NetworkVariable<FixedString64Bytes>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    ); // inicialización de un string en red (se indican los permisos de lectura y escritura)

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn(); //indica que se usa lo implementado ya según la documentación
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
        GameManager.Instance?.CheckAllReady(); //llama a la funcion de GameManager para saber si todos los jugadores están 
    }

    [Rpc(SendTo.Server)]
    public void SetCharacterServerRpc(FixedString64Bytes characterName) //pone el nombre del jugador
    {
        Debug.Log($"[PlayerState] ✓ SetCharacterServerRpc recibido: {characterName} del cliente {OwnerClientId}");
        SelectedCharacterName.Value = characterName;
    }
}
