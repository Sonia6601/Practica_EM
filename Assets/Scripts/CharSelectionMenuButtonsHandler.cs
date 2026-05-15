using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Text;

public class CharSelectionMenuButtonsHandler : MonoBehaviour
{
    [Header("Character Stats Assets")]
    [SerializeField] private PlayerStats greenCharacterStats;
    [SerializeField] private PlayerStats purpleCharacterStats;
    [SerializeField] private PlayerStats redCharacterStats;
    [SerializeField] private PlayerStats yellowCharacterStats;
    [SerializeField] private TextMeshProUGUI code;

    [Header("UI")]
    [SerializeField] private TMP_Text connectedPlayersText;
    [SerializeField] private TMP_Text playerListText;

    private PlayerState localPlayerState;
    private string pendingCharacterName;
    private bool pendingReadyRequest;
    private bool pendingReadyValue;

    public void Start()
    {
        if(GameManager.Instance != null)
        {
            code.text = GameManager.Instance.RoomCode;
        }
        
        localPlayerState = null; // Reset al iniciar
        
        // Buscar y configurar el botón START con el controlador
        Button startButton = FindStartButton();
        if (startButton != null && startButton.GetComponent<StartButtonController>() == null)
        {
            startButton.gameObject.AddComponent<StartButtonController>();
            Debug.Log("[CharSelection] StartButtonController agregado dinámicamente al botón");
        }
        
        UpdatePlayerList();
    }

    private Button FindStartButton()
    {
        // Buscar el botón llamado "ButtonStart" en la escena
        Transform canvas = FindObjectOfType<Canvas>()?.transform;
        if (canvas != null)
        {
            Transform startButtonTransform = canvas.Find("ButtonStart");
            if (startButtonTransform != null)
            {
                Button button = startButtonTransform.GetComponent<Button>();
                return button;
            }
        }
        
        // Alternativa: buscar por nombre en toda la jerarquía
        Button[] buttons = FindObjectsOfType<Button>();
        foreach (Button btn in buttons)
        {
            if (btn.gameObject.name == "ButtonStart")
            {
                return btn;
            }
        }
        
        Debug.LogWarning("[CharSelection] No se encontró el botón START");
        return null;
    }

    /// <summary>
    /// Vuelve al menú principal desde la pantalla de selección de personaje.
    /// </summary>
    public void OnBackButtonClicked()
    {
        SceneManager.LoadScene(SceneNames.MainMenu);
    }

    /// <summary>
    /// Selecciona el personaje verde e inicia la partida.
    /// </summary>
    /// 
    public void OnRandomButtonClicked()
    {
        // Multiuso: HOST inicia, CLIENTE marca como Listo
        if (NetworkManager.Singleton.IsServer)
        {
            // **FLUJO DEL HOST: Iniciar la partida**
            
            int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            
            // Verificar que al menos 2 jugadores estén conectados
            if (connectedCount < 2)
            {
                Debug.LogWarning("[CharSelection] Se necesita al menos 2 jugadores para comenzar.");
                return;
            }

            // Verificar que todos estén listos
            bool allReady = true;
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var playerState = client.PlayerObject?.GetComponent<PlayerState>();
                if (playerState == null)
                {
                    Debug.LogWarning($"[CharSelection] Cliente {client.ClientId} no tiene PlayerState");
                    allReady = false;
                    break;
                }
                if (!playerState.isReady.Value)
                {
                    Debug.LogWarning($"[CharSelection] Cliente {client.ClientId} no está listo");
                    allReady = false;
                    break;
                }
            }
            
            if (!allReady)
            {
                return;
            }

            // Todos listos, cargar el juego
            Debug.Log("[CharSelection] ✓ ¡TODOS LISTOS! Iniciando partida...");
            GameManager.Instance.CheckAllReady();
        }
        else
        {
            // **FLUJO DEL CLIENTE: Marcar como Listo**
            OnReadyButtonClicked();
        }
    }

    /// <summary>
    /// Marca al jugador actual como "Listo" o "No listo".
    /// Los clientes pueden presionar esto en cualquier momento.
    /// </summary>
    public void OnReadyButtonClicked()
    {
        if (GameManager.Instance?.SelectedCharacterStats == null)
        {
            Debug.LogWarning("[CharSelection] Selecciona un personaje antes de marcar Listo.");
            return;
        }

        EnsureLocalPlayerState();

        if (localPlayerState == null)
        {
            Debug.LogWarning("[CharSelection] PlayerObject local aún no disponible. Espera...");
            pendingReadyRequest = true;
            pendingReadyValue = !pendingReadyValue;
            framesWaited = 0;
            return;
        }

        Debug.Log("[CharSelection] Guardando toggle ready como pendiente");
        pendingReadyRequest = true;
        pendingReadyValue = !localPlayerState.isReady.Value;
        framesWaited = 0;
    }


    public void OnGreenButtonClicked()
    {
        SelectCharacter(greenCharacterStats);
    }

    /// <summary>
    /// Selecciona el personaje morado e inicia la partida.
    /// </summary>
    public void OnPurpleButtonClicked()
    {
        SelectCharacter(purpleCharacterStats);
    }

    /// <summary>
    /// Selecciona el personaje rojo e inicia la partida.
    /// </summary>
    public void OnRedButtonClicked()
    {
        SelectCharacter(redCharacterStats);
    }

    /// <summary>
    /// Selecciona el personaje amarillo e inicia la partida.
    /// </summary>
    public void OnYellowButtonClicked()
    {
        SelectCharacter(yellowCharacterStats);
    }

    /// <summary>
    /// Selecciona un personaje y lo sincroniza con el servidor.
    /// </summary>
    private void SelectCharacter(PlayerStats characterStats)
    {
        if (characterStats == null)
        {
            Debug.LogError("[CharSelection] PlayerStats nulo");
            return;
        }

        // Guardar localmente
        GameManager.Instance.SelectedCharacterStats = characterStats;

        EnsureLocalPlayerState();
        
        Debug.Log($"[CharSelection] Guardando selección para reintento: {characterStats.characterName}");
        pendingCharacterName = characterStats.characterName;
        framesWaited = 0; // Reset del contador de espera

        // Actualizar la lista visual
        UpdatePlayerList();
    }

    /// <summary>
    /// Obtiene y cachea el PlayerState del jugador local si está disponible.
    /// </summary>
    private void EnsureLocalPlayerState()
    {
        if (localPlayerState != null)
            return; // Ya está cacheado

        var localClient = NetworkManager.Singleton?.LocalClient;
        if (localClient == null)
        {
            return;
        }

        if (localClient.PlayerObject == null)
        {
            return;
        }

        localPlayerState = localClient.PlayerObject.GetComponent<PlayerState>();
        if (localPlayerState != null)
        {
            Debug.Log($"[CharSelection] PlayerState encontrado y cacheado. IsSpawned: {localPlayerState.IsSpawned}");
        }
    }

    private void ProcessPendingActions()
    {
        EnsureLocalPlayerState();

        if (localPlayerState == null)
        {
            return; // Aún no está disponible
        }

        // Esperar a que IsSpawned sea verdadero (máximo 60 frames)
        if (!localPlayerState.IsSpawned)
        {
            framesWaited++;
            if (framesWaited < 60)
            {
                return; // Seguir esperando
            }
            // Si pasaron 60 frames, intentar de todas formas
            Debug.LogWarning("[CharSelection] Timeout esperando IsSpawned. Intentando enviar de todas formas...");
            framesWaited = 0;
        }
        else if (framesWaited > 0)
        {
            Debug.Log($"[CharSelection] ✓ IsSpawned = true después de {framesWaited} frames");
            framesWaited = 0;
        }

        // Procesar selección de personaje pendiente
        if (!string.IsNullOrEmpty(pendingCharacterName))
        {
            try
            {
                Debug.Log($"[CharSelection] Enviando selección pendiente: {pendingCharacterName}");
                localPlayerState.SetCharacterServerRpc(pendingCharacterName);
                pendingCharacterName = null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CharSelection] No se pudo enviar selección: {e.Message}");
                // Reintentar la próxima vez
            }
        }

        // Procesar ready pendiente
        if (pendingReadyRequest)
        {
            try
            {
                Debug.Log($"[CharSelection] Enviando ready pendiente: {pendingReadyValue}");
                localPlayerState.SetReadyServerRpc(pendingReadyValue);
                pendingReadyRequest = false;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CharSelection] No se pudo enviar ready: {e.Message}");
                // Reintentar la próxima vez
            }
        }
    }

    private void UpdatePlayerList()
    {
        if (connectedPlayersText == null || playerListText == null)
            return;

        int totalConnected = NetworkManager.Singleton.ConnectedClients.Count;
        connectedPlayersText.text = $"Jugadores conectados: {totalConnected}/4";

        StringBuilder playerListBuilder = new StringBuilder();
        bool allReady = totalConnected >= 2; // Asumir que no todos están listos al inicio
        
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerState = client.PlayerObject?.GetComponent<PlayerState>();
            if (playerState != null)
            {
                string charName = playerState.SelectedCharacterName.Value.ToString();
                bool isReady = playerState.isReady.Value;
                
                if (string.IsNullOrEmpty(charName) || charName == "")
                {
                    charName = "(sin personaje)";
                }

                // Mostrar si es HOST o CLIENT
                string role = (client.ClientId == 0) ? "👑 HOST" : "🎮 CLIENT";
                
                // Si es el jugador local, marcar
                bool isLocalPlayer = NetworkManager.Singleton.LocalClient.ClientId == client.ClientId;
                string localMarker = isLocalPlayer ? " ← TÚ" : "";
                
                string readyStatus = isReady ? "✓ LISTO" : "⏳ Esperando";
                playerListBuilder.AppendLine($"{role} • {charName} [{readyStatus}]{localMarker}");

                if (!isReady)
                    allReady = false;
            }
        }

        playerListText.text = playerListBuilder.ToString();

        // Mostrar estado del botón Start
        if (NetworkManager.Singleton.IsServer)
        {
            if (totalConnected >= 2 && allReady)
            {
                playerListBuilder.AppendLine("\n✅ ¡TODOS LISTOS! Presiona START para iniciar");
            }
            else
            {
                playerListBuilder.AppendLine("\n⏳ Esperando a que todos estén listos...");
            }
        }
        else
        {
            playerListBuilder.AppendLine("\n⏳ Esperando a que el HOST inicie la partida...");
        }

        playerListText.text = playerListBuilder.ToString();
        Debug.Log($"[Lobby] Jugadores conectados: {totalConnected}");
    }

    /// <summary>
    /// Valida la selección del personaje y delega el inicio de partida en GameManager.
    /// </summary>
    private void selectCharacterAndStartGame(PlayerStats characterStats)
    {
        if (characterStats == null)
        {
            Debug.LogError("[CharSelection] No se ha asignado PlayerStats para este personaje");
            return;
        }

        GameManager.Instance?.StartGame(characterStats);
    }

    private void Update()
    {
        // Intentar obtener PlayerState si aún no lo hemos hecho
        EnsureLocalPlayerState();

        // Procesar cualquier acción pendiente
        ProcessPendingActions();

        // Actualizar la lista cada frame para reflejar cambios en tiempo real
        UpdatePlayerList();
        
        // Actualizar estado del botón START
        UpdateStartButtonState();
    }

    private void UpdateStartButtonState()
    {
        // Encontrar botón START y actualizar su estado
        Button startButton = FindStartButton();
        if (startButton == null)
            return;

        bool isHost = NetworkManager.Singleton.IsServer;
        
        if (isHost)
        {
            // HOST: Solo habilitar si todos están listos
            int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
            bool allReady = connectedCount >= 2;
            
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                var playerState = client.PlayerObject?.GetComponent<PlayerState>();
                if (playerState == null || !playerState.isReady.Value)
                {
                    allReady = false;
                    break;
                }
            }
            
            startButton.interactable = allReady && connectedCount >= 2;
            
            // Actualizar texto si existe
            Text buttonText = startButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = allReady ? "Comenzar" : "Esperando...";
            }
        }
        else
        {
            // CLIENT: Siempre habilitado para marcar listo
            startButton.interactable = true;
            
            Text buttonText = startButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = "Marcar Listo";
            }
        }
    }

}