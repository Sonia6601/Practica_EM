using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuButtonsHandler : NetworkBehaviour
{
    [Header("Map Configs disponibles")]
    [SerializeField] private MapConfig[] availableMaps;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown mapsDropdown;

    public TMP_InputField inputCode;

    /// <summary>
    /// Inicializa el dropdown de mapas al cargar el menú principal.
    /// </summary>
    private void Start()
    {
        initializeMapDropdown();
    }

    /// <summary>
    /// Libera la suscripción del dropdown al destruir el objeto.
    /// </summary>
    private void OnDestroy()
    {
        if (mapsDropdown != null)
            mapsDropdown.onValueChanged.RemoveListener(onMapDropdownChanged);
    }

    /// <summary>
    /// Navega a la escena de selección de personaje si hay mapa seleccionado.
    /// </summary>
    public void OnButtonPlayClicked()
    {
        if (GameManager.Instance?.SelectedMapConfig == null)
        {
            Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
            return;
        }

        //Aquí es donde al darle a jugar se pasa a la escena de seleccionar jugadores
        //Entonces, el primero que le de, poner que sea el host, es decir, Que el primero acceda al networkManager y active la opción de create Host
        //Y el resto serán clientes, acceda al networkManager y active el crear cliente

        SceneManager.LoadScene(SceneNames.CharSelection);
    }

    public void StartHost()
    {
        if (GameManager.Instance?.SelectedMapConfig == null)
        {
            Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
            return;
        }

        string localIP = GetLocalIPv4(); //Se coge la IPv4 del host para que los clientes se puedan conectar al host
        string codeRoom = GeneracionCodigoSala(); //Se genera el código de la sala
        Debug.Log("[HOST]: Sala creada con codigo: " + codeRoom);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(localIP, 7777);

        NetworkManager.Singleton.StartHost();

        NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.CharSelection, LoadSceneMode.Single);
    }

    private string GetLocalIPv4()
    {
        throw new NotImplementedException();
    }

    public void StartClient()
    {
        if (GUIcodigo())
        {
            NetworkManager.Singleton.StartClient();
        } else
        {
            Debug.Log("Codigo de sala incorrecto");
        }

        //SceneManager.LoadScene(SceneNames.CharSelection);
    }

    private bool GUIcodigo()
    {
        //Aquí se implementará la barra para que el cliente escriba el codigo y se pueda unir a la sala
        string codigo = inputCode.text;

        return true;
    }

    /// <summary>
    /// Registra la acción del botón de opciones del menú principal.
    /// </summary>
    public void OnOptionsButtonClicked()
    {
        Debug.Log("Options button pressed");
    }

    /// <summary>
    /// Cierra la aplicación o detiene la ejecución en el editor.
    /// </summary>
    public void OnExitButtonClicked()
    {
        Debug.Log("Exit button pressed");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Configura las opciones del dropdown y establece el mapa inicial seleccionado.
    /// </summary>
    private void initializeMapDropdown()
    {
        if (mapsDropdown == null || availableMaps == null || availableMaps.Length == 0)
        {
            Debug.LogWarning("[MainMenu] Dropdown de mapas no configurado.");
            return;
        }

        mapsDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (MapConfig map in availableMaps)
        {
            options.Add(new TMP_Dropdown.OptionData(map != null ? map.mapName : "Sin nombre"));
        }

        mapsDropdown.AddOptions(options);
        mapsDropdown.value = 0;
        mapsDropdown.RefreshShownValue();
        mapsDropdown.onValueChanged.AddListener(onMapDropdownChanged);

        applySelectedMap(0);
    }

    /// <summary>
    /// Aplica el mapa seleccionado cuando cambia el valor del dropdown.
    /// </summary>
    private void onMapDropdownChanged(int index)
    {
        applySelectedMap(index);
    }

    /// <summary>
    /// Guarda en GameManager el mapa correspondiente al índice indicado.
    /// </summary>
    private void applySelectedMap(int index)
    {
        if (availableMaps == null || index < 0 || index >= availableMaps.Length) return;
        if (GameManager.Instance == null) return;

        GameManager.Instance.SelectedMapConfig = availableMaps[index];
        Debug.Log($"[MainMenu] Mapa seleccionado: {availableMaps[index].mapName}");
    }

    private string GeneracionCodigoSala()
    {
        //Aquí se generará el código de la sala, el host se lo pasará a sus amiguitos para poder jugar juntos
        //El código debe aparecer en el canvas de la escena de selección de jugadores
        string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = " ";

        for (int i = 0; i < 6; i++)
        {
            int index = UnityEngine.Random.Range(0, characters.Length);
            code += characters[index];

        }

        return code;
    }
}
