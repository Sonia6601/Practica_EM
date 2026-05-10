using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;

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

    public void Start()
    {
        if(GameManager.Instance != null)
        {
            code.text = GameManager.Instance.RoomCode;
        }
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
        if (GameManager.Instance?.SelectedCharacterStats == null)
        {
            Debug.LogWarning("[CharSelection] Selecciona un personaje antes de marcar Ready.");
            return;
        }

        // Ahora usamos la instancia local directa, sin pasar por NetworkManager
        var localPlayer = PlayerState.LocalInstance;

        if (localPlayer == null)
        {
            Debug.LogError("[CharSelection] PlayerState.LocalInstance es null. " +
                           "El prefab del jugador no tiene PlayerState, o aún no se spawneó.");
            return;
        }

        Debug.Log($"[CharSelection] Marcando ready. Estado actual: {localPlayer.isReady.Value}");
        localPlayer.SetReadyServerRpc(!localPlayer.isReady.Value);

    }


    public void OnGreenButtonClicked()
    {
        //selectCharacterAndStartGame(greenCharacterStats);
        GameManager.Instance.SelectedCharacterStats = greenCharacterStats;

    }

    /// <summary>
    /// Selecciona el personaje morado e inicia la partida.
    /// </summary>
    public void OnPurpleButtonClicked()
    {
        GameManager.Instance.SelectedCharacterStats = purpleCharacterStats;

    }

    /// <summary>
    /// Selecciona el personaje rojo e inicia la partida.
    /// </summary>
    public void OnRedButtonClicked()
    {
        GameManager.Instance.SelectedCharacterStats = redCharacterStats;

    }

    /// <summary>
    /// Selecciona el personaje amarillo e inicia la partida.
    /// </summary>
    public void OnYellowButtonClicked()
    {
        GameManager.Instance.SelectedCharacterStats = yellowCharacterStats;

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

}