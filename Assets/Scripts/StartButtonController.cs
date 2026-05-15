using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

/// <summary>
/// Controla el comportamiento del botón START/LISTO.
/// - HOST: Muestra "Comenzar" y puede iniciar cuando todos están listos
/// - CLIENT: Muestra "Marcar Listo" y puede marcar su estado
/// </summary>
public class StartButtonController : MonoBehaviour
{
    private Button startButton;
    private Text buttonText;

    private void Start()
    {
        startButton = GetComponent<Button>();
        if (startButton == null)
        {
            Debug.LogError("[StartButtonController] No se encontró Button component");
            return;
        }

        // Buscar Text en hijos o en el mismo componente
        buttonText = GetComponentInChildren<Text>();
        if (buttonText == null)
        {
            // Buscar en el componente directo del Canvas
            buttonText = transform.Find("Text")?.GetComponent<Text>();
        }
        if (buttonText == null)
        {
            Debug.LogWarning("[StartButtonController] No se encontró Text component. Continuando sin actualizar texto.");
        }

        UpdateButtonState();
    }

    private void Update()
    {
        if (startButton == null)
            return;

        // Actualizar cada frame para reflejar cambios en tiempo real
        UpdateButtonState();
    }

    private void UpdateButtonState()
    {
        if (startButton == null)
            return;

        bool isHost = NetworkManager.Singleton?.IsServer ?? false;
        bool allReady = CheckAllReady();

        // Determinar si el botón debe estar habilitado
        if (isHost)
        {
            // HOST: Solo habilitar si todos están listos
            int connectedCount = NetworkManager.Singleton?.ConnectedClientsList.Count ?? 0;
            startButton.interactable = connectedCount >= 2 && allReady;
            
            if (buttonText != null)
            {
                buttonText.text = (connectedCount >= 2 && allReady) ? "Comenzar" : "Esperando...";
            }
        }
        else
        {
            // CLIENT: Siempre habilitado para marcar listo
            startButton.interactable = true;
            
            if (buttonText != null)
            {
                buttonText.text = "Marcar Listo";
            }
        }
    }

    private bool CheckAllReady()
    {
        if (NetworkManager.Singleton?.ConnectedClientsList == null)
            return false;

        int connectedCount = NetworkManager.Singleton.ConnectedClientsList.Count;
        if (connectedCount < 2)
            return false;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerState = client.PlayerObject?.GetComponent<PlayerState>();
            if (playerState == null || !playerState.isReady.Value)
                return false;
        }

        return true;
    }
}
