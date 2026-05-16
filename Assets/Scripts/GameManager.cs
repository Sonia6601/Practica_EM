using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNames
{
    public const string MainMenu = "MainMenu";
    public const string CharSelection = "CharSelectionScene";
    public const string PlaygroundLevel = "PlaygroundLevel";
    public const string DeadScene = "DeadScene";
    public const string VictoryScene = "VictoryScene";
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public NetworkManager _networkManager;

    [SerializeField] private GameObject _playerBall;

    public PlayerController LocalPlayerController { get; private set; }
    public Transform LocalPlayerTransform => LocalPlayerController != null ? LocalPlayerController.transform : null;
    public UniqueEntity LocalPlayerEntity { get; private set; }

    public int EnemiesKilled { get; private set; }
    public PlayerStats SelectedCharacterStats { get; set; }
    public MapConfig SelectedMapConfig { get; set; }
    public string RoomCode { get; set; }

    public NetworkVariable<Unity.Collections.FixedString64Bytes> Code = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [SerializeField] private float delayBeforeScene = 0.5f;

    private PlayerGameState playerState;

    public NetworkVariable<int> clientes = new NetworkVariable<int>();
    
    public NetworkVariable<int> MapSeed = new NetworkVariable<int>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    /// <summary>
    /// Inicializa el singleton del juego y sus datos persistentes.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerState = new PlayerGameState("PLAYER_1");
        SceneManager.sceneUnloaded += onSceneUnloaded;
    }

    public void Start()
    {

        if (_networkManager == null)
            _networkManager = NetworkManager.Singleton;

        // Desactivar auto-spawn para evitar conflictos con spawn manual
        _networkManager.NetworkConfig.AutoSpawnPlayerPrefabClientSide = false;

        _networkManager.OnServerStarted += onServerStarted;
        _networkManager.OnClientConnectedCallback += onClientConnected;
        _networkManager.OnClientDisconnectCallback += onClientDisconnect;
    }

    /// <summary>
    /// Libera suscripciones globales al destruir el gestor.
    /// </summary>
    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= onSceneUnloaded;
    }

    /// <summary>
    /// Suscribe callbacks de eventos persistentes del juego.
    /// </summary>
    private void OnEnable()
    {
        GameEvents.OnPlayerDied += onPlayerDeath;
    }

    /// <summary>
    /// Desuscribe callbacks de eventos persistentes del juego.
    /// </summary>
    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= onPlayerDeath;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClientAndHostRpc(int value, ulong sourceNetworkObjectId)
    {
        Debug.Log($"Client Received the RPC #{value} on NetworkObject #{sourceNetworkObjectId}");
        if (IsOwner) //Only send an RPC to the owner of the NetworkObject
        {
            ServerOnlyRpc(value + 1, sourceNetworkObjectId);
        }
    }

    [Rpc(SendTo.Server)]
    private void ServerOnlyRpc(int value, ulong sourceNetworkObjectId)
    {
        Debug.Log($"Server received RPC #{value} on NetworkObject #{sourceNetworkObjectId}" );
        ClientAndHostRpc(value, sourceNetworkObjectId);
    }

    private void onServerStarted()
    {
        print("El servidor está listo");
        clientes.Value = 0;
        _networkManager.SceneManager.OnLoadEventCompleted += onSceneLoadCompleted;

    }

    private void onSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode,
    List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName != SceneNames.CharSelection) return;
        if (!_networkManager.IsServer) return;

        Debug.Log($"[GameManager] Escena {sceneName} cargada en servidor. Clients completados: {clientsCompleted.Count}");

        // Spawnear jugadores para cada cliente que completó la carga
        foreach (ulong clientId in clientsCompleted)
        {
            Debug.Log($"[GameManager] Procesando spawn para cliente {clientId}");

            var existingPlayerObject = _networkManager.ConnectedClients[clientId].PlayerObject;
            
            // Si ya tiene PlayerObject pero NO está spawneado, destruirlo e intentar de nuevo
            if (existingPlayerObject != null)
            {
                var networkObj = existingPlayerObject.GetComponent<NetworkObject>();
                if (networkObj != null && networkObj.IsSpawned)
                {
                    Debug.Log($"[GameManager] Cliente {clientId} ya tiene PlayerObject spawneado. Saltando.");
                    continue;
                }
                else
                {
                    Debug.Log($"[GameManager] Cliente {clientId} tiene PlayerObject pero NO está spawneado. Destruyendo...");
                    Destroy(existingPlayerObject);
                }
            }

            Debug.Log($"[GameManager] Instantiando PlayerObject para cliente {clientId}");
            var playerObject = Instantiate(_playerBall);
            Debug.Log($"[GameManager] PlayerObject instantiado: {playerObject.name}");

            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[GameManager] PlayerObject sin NetworkObject en prefab. Destroy.");
                Destroy(playerObject);
                continue;
            }

            Debug.Log($"[GameManager] Llamando SpawnAsPlayerObject({clientId})");
            networkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"[GameManager] SpawnAsPlayerObject completado. IsSpawned ahora: {networkObject.IsSpawned}");
        }
    }

    // Evento cuando un cliente se ha conectado
    private void onClientConnected(ulong clientId)
    {
        // Solo si eres el servidor decides instanciar a los clientes
        if (!_networkManager.IsServer) return;

        clientes.Value += 1;
        Debug.Log($"[GameManager] Cliente {clientId} conectado. Total: {clientes.Value}");

        // Solo spawnear si ya estamos en CharSelection
        // Si no, onSceneLoadCompleted lo hará al cargar la escena
        if (SceneManager.GetActiveScene().name == SceneNames.CharSelection)
        {
            Debug.Log($"[GameManager] CharSelection ya activa. Intentando spawn para cliente {clientId}");
            
            var existingPlayerObject = _networkManager.ConnectedClients[clientId].PlayerObject;
            
            if (existingPlayerObject != null)
            {
                var networkObj = existingPlayerObject.GetComponent<NetworkObject>();
                if (networkObj != null && networkObj.IsSpawned)
                {
                    Debug.Log($"[GameManager] Cliente {clientId} ya está spawneado. Skip.");
                    return;
                }
                Destroy(existingPlayerObject);
            }
            
            var playerObject = Instantiate(_playerBall);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[GameManager] PlayerObject sin NetworkObject. Destroy.");
                Destroy(playerObject);
                return;
            }
            Debug.Log($"[GameManager] Spawneando para cliente {clientId}");
            networkObject.SpawnAsPlayerObject(clientId);
            Debug.Log($"[GameManager] Spawn completado. IsSpawned: {networkObject.IsSpawned}");
        }
        else
        {
            Debug.Log($"[GameManager] Cliente {clientId} conectado. Esperando carga de CharSelection para spawn.");
        }
    }

    private void onClientDisconnect(ulong clientId)
    {
        StartCoroutine(HandleDisconnect(clientId));
    }

    private IEnumerator HandleDisconnect(ulong disconnectedClientId)
    {
        // Esperar un frame para asegurar que el cliente se ha desconectado completamente
        yield return null;

        Debug.Log($"[GameManager] Cliente {disconnectedClientId} se ha desconectado");

        // Decrementar contador de clientes
        clientes.Value = Mathf.Max(0, clientes.Value - 1);
        Debug.Log($"[GameManager] Clientes conectados: {clientes.Value}");

        // Si estamos en el lobby (CharSelection), solo decrementar contador
        // Si estamos en juego (PlaygroundLevel), despawnear el jugador
        if (SceneManager.GetActiveScene().name == SceneNames.PlaygroundLevel)
        {
            var allPlayers = GameObject.FindGameObjectsWithTag("Player");

            foreach (var player in allPlayers)
            {
                if (player.TryGetComponent<NetworkObject>(out var netObj))
                {
                    // Verificar si este jugador pertenecía al cliente desconectado
                    if (netObj.OwnerClientId == disconnectedClientId)
                    {
                        netObj.Despawn(destroy: true);
                        Debug.Log($"[GameManager] Jugador del cliente {disconnectedClientId} despawneado");
                    }
                }
            }

            // Notificar a los demás jugadores
            if (IsServer)
            {
                Debug.Log($"[GameManager] Un jugador se ha desconectado. Jugadores restantes: {clientes.Value}");
            }
        }
    }



    public void CheckAllReady() //comprueba si los clientes están (función del boton start del lobby)
    {
        if (!IsServer) return; //si no eres el host, no puedes continuar (no puedes pasar a la siguiente pantalla)

        Debug.Log($"[GameManager] CheckAllReady() llamado. Clientes conectados: {NetworkManager.Singleton.ConnectedClientsList.Count}");

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2)
        {
            Debug.Log("[GameManager] No hay suficientes clientes conectados (mínimo 2)");
            return;
        }

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject?.GetComponent<PlayerState>(); //intenta conseguir los stats de los jugadores
            if (player == null)
            {
                Debug.Log($"[GameManager] Cliente {client.ClientId} no tiene PlayerState");
                return;
            }

            if (!player.isReady.Value)
            {
                Debug.Log($"[GameManager] Cliente {client.ClientId} no está listo");
                return;
            }
        }

        // Todos están listos
        Debug.Log("[GameManager] ¡TODOS LOS JUGADORES ESTÁN LISTOS! Cargando PlaygroundLevel...");
        StartCoroutine(DespawnAndLoadScene()); //empieza la corrutina de gestión de escena
    }

    private IEnumerator DespawnAndLoadScene() //Funcion de carga y descarga de escenas en linea
    {
        var allPlayers = GameObject.FindGameObjectsWithTag("Player"); //Busca los Gameobjects con el tag "Player" en Unity
        foreach (var player in allPlayers)
        {
            if (player.TryGetComponent<NetworkObject>(out var netObj)) //intenta conseguir los Gameobjets de los objectos (en solo lectura)
            {
                netObj.Despawn(); 
            }
        }

        // Esperar 1 frame (mínimo)
        yield return null;

        // Generar seed sincronizado para el mapa en el servidor
        if (IsServer)
        {
            int newSeed = Random.Range(0, int.MaxValue);
            MapSeed.Value = newSeed;
            Debug.Log($"[GameManager] Seed de mapa generado en servidor: {newSeed}");
        }

        NetworkManager.Singleton.SceneManager.LoadScene("PlaygroundLevel", LoadSceneMode.Single);
    }

    public override void OnNetworkSpawn() // Cuando se crea la red
    {
        if (IsServer)
        {
            NetworkObject.DontDestroyWithOwner = true;
        }

        if (!IsServer && IsOwner) //si eres el servidor y el que está ejecutando este código
        {
            ServerOnlyRpc(0, NetworkObjectId);
        }
    }

    /// <summary>
    /// Registra el jugador local activo y publica su evento de registro.
    /// </summary>
    public void RegisterLocalPlayer(PlayerController player, UniqueEntity entity)
    {
        LocalPlayerController = player;
        LocalPlayerEntity = entity;
        SetPlayerData(entity);
        GameEvents.LocalPlayerRegistered(player);
    }

    /// <summary>
    /// Inicializa el estado del jugador con el identificador de su entidad.
    /// </summary>
    public void SetPlayerData(UniqueEntity playerEntity)
    {
        if (playerEntity == null || string.IsNullOrEmpty(playerEntity.EntityId)) return;
        playerState = new PlayerGameState(playerEntity.EntityId);
    }

    /// <summary>
    /// Reinicia los datos de partida del jugador y estadísticas globales.
    /// </summary>
    public void ResetGameData()
    {
        playerState?.ResetState();
        EnemiesKilled = 0;
    }

    /// <summary>
    /// Incrementa el contador global de enemigos eliminados.
    /// </summary>
    public void AddEnemyKill()
    {
        EnemiesKilled++;
        GameEvents.EnemyKilled(EnemiesKilled);
    }

    /// <summary>
    /// Devuelve la cantidad actual de llaves del jugador local.
    /// </summary>
    public int GetKeys()
    {
        return playerState?.Keys ?? 0;
    }

    /// <summary>
    /// Devuelve la cantidad actual de diamantes del jugador local.
    /// </summary>
    public int GetDiamonds()
    {
        return playerState?.Diamonds ?? 0;
    }

    /// <summary>
    /// Intenta añadir una llave al inventario del jugador actual.
    /// </summary>
    public bool TryAddKey(string playerEntityId, string keyEntityId)
    {
        if (playerState == null) return false;
        playerState.AddKey();
        return true;
    }

    /// <summary>
    /// Intenta añadir un diamante al inventario del jugador actual.
    /// </summary>
    public bool TryAddDiamond(string playerEntityId, string diamondEntityId)
    {
        if (playerState == null) return false;
        playerState.AddDiamond();
        return true;
    }

    /// <summary>
    /// Intenta abrir una puerta consumiendo una llave del jugador actual.
    /// </summary>
    public bool TryOpenDoor(string playerEntityId, string doorEntityId)
    {
        if (playerState == null) return false;
        return playerState.UseKey();
    }

    /// <summary>
    /// Intenta activar la condición de victoria para el jugador actual.
    /// </summary>
    public bool TryTriggerVictory(string playerEntityId, string chestEntityId)
    {
        if (playerState == null) return false;
        victoryAchieved();
        return true;
    }

    /// <summary>
    /// Guarda el personaje seleccionado, reinicia datos y carga el nivel de juego.
    /// </summary>
    public void StartGame(PlayerStats selectedCharacter)
    {
        if (selectedCharacter == null)
        {
            Debug.LogError("[GameManager] StartGame llamado sin personaje seleccionado.");
            return;
        }

        Debug.Log($"selected character is {selectedCharacter.characterName}");
        SelectedCharacterStats = selectedCharacter;
        ResetGameData();

        SceneManager.LoadScene(SceneNames.PlaygroundLevel);
    }

    /// <summary>
    /// Guarda mapa y personaje seleccionados e inicia la partida.
    /// </summary>
    public void StartGame(PlayerStats selectedCharacter, MapConfig selectedMap)
    {
        SelectedMapConfig = selectedMap;
        StartGame(selectedCharacter);
    }

    /// <summary>
    /// Inicia el flujo de fin de partida por muerte del jugador.
    /// </summary>
    public void TriggerGameOver()
    {
        Debug.Log($"[GameManager] Game Over. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
        Invoke(nameof(loadDeadScene), delayBeforeScene);
    }

    /// <summary>
    /// Limpia los eventos de escena cuando se descarga el nivel jugable.
    /// </summary>
    private void onSceneUnloaded(Scene scene)
    {
        if (scene.name == SceneNames.PlaygroundLevel)
        {
            GameEvents.ClearSceneEvents();
        }
    }

    /// <summary>
    /// Carga la escena de derrota del jugador.
    /// </summary>
    private void loadDeadScene()
    {
        SceneManager.LoadScene(SceneNames.DeadScene);
    }

    /// <summary>
    /// Registra logs de victoria y programa la carga de la escena final.
    /// </summary>
    private void victoryAchieved()
    {
        Debug.Log($"[GameManager] Victoria. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
        Invoke(nameof(loadVictoryScene), delayBeforeScene);
    }

    /// <summary>
    /// Carga la escena de victoria del juego.
    /// </summary>
    private void loadVictoryScene()
    {
        SceneManager.LoadScene(SceneNames.VictoryScene);
    }

    /// <summary>
    /// Registra en consola el estado del juego cuando el jugador muere.
    /// </summary>
    private void onPlayerDeath()
    {
        Debug.Log($"[GameManager] Jugador muerto. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
    }

}



