using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;
using System;
using System.Linq;


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
    public TMP_InputField inputHostIP;

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
            if (availableMaps != null && availableMaps.Length > 0)
            {
                GameManager.Instance.SelectedMapConfig = availableMaps[0];
                Debug.LogWarning("[MainMenu] No había mapa seleccionado. Se usa el primer mapa disponible.");
            }
            else
            {
                Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
                return;
            }
        }

        string codeRoom = GeneracionCodigoSala(); //Se genera el código de la sala
        Debug.Log("[HOST]: Sala creada con codigo: " + codeRoom);
        GameManager.Instance.RoomCode = codeRoom;

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // El host debe escuchar en todas las interfaces para ser más robusto en el mismo PC.
        transport.SetConnectionData("0.0.0.0", 7777);

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("[HOST] No se pudo iniciar el host.");
            return;
        }

        GameManager.Instance.Code.Value = codeRoom;
        NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.CharSelection, LoadSceneMode.Single);
    }

    private string GetLocalIPv4()
    {
        // 1. Dns.GetHostName(): Obtiene el nombre del ordenador local (ej: "PC-Juan").
        // 2. Dns.GetHostEntry(...): Busca en la red ese nombre y devuelve todas las interfaces de red de este PC.
        // 3. AddressList.First(...): Filtra la lista para quedarse con la primera dirección que sea de tipo 'InterNetwork' (es decir, IPv4). Filtrar es necesario porque también puede devolver direcciones IPv6 o de red virtual.
        // 4. ToString(): Convierte la IP encontrada a texto (ej: "192.168.1.33").
        return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
    }


    // Método que ejecuta el jugador que intenta unirse (Cliente) a una sala ya creada.
    public void StartClient()
    {
        string codigoIntroducido = inputCode?.text.Trim().ToUpper(); //pone con un buen formato el codigo (por si hay espacio o se pone en minuscula)

        if (string.IsNullOrEmpty(codigoIntroducido))
        {
            Debug.Log("Introduce un código.");
            return;
        }

        // Guardamos el código introducido localmente para mostrarlo si hace falta.
        GameManager.Instance.RoomCode = codigoIntroducido;

        // Recuperamos el componente de transporte para decirle al cliente a dónde debe conectarse.
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // Si quieres probar en el mismo ordenador, usa localhost.
        // Si quieres probar desde otro ordenador, escribe la IP del host en el campo Host IP.
        string hostIp = "127.0.0.1";
        if (inputHostIP != null && !string.IsNullOrWhiteSpace(inputHostIP.text))
        {
            hostIp = inputHostIP.text.Trim();
        }

        transport.SetConnectionData(hostIp, 7777);

        // Inicia los sistemas internos, conecta por UDP al servidor y sincroniza la partida actual.
        NetworkManager.Singleton.StartClient();
    }

    // [Rpc(SendTo.Server)] indica que este método es una llamada de red de Cliente a Servidor.
    // Aunque un Cliente llame a esta función en su propio código, la función se envía por red
    // y se EJECUTA ÚNICAMENTE EN EL ORDENADOR DEL HOST (Servidor).
    // Es el sustituto moderno del antiguo atributo [ServerRpc].
    [Rpc(SendTo.Server)]
    public void ValidarCodigoServerRpc(string codigoCliente, ulong clientId)
    {
        if (codigoCliente != GameManager.Instance.RoomCode)
        {
            Debug.Log($"Cliente {clientId} tiene código incorrecto");
        }
        else
        {
            Debug.Log($"Cliente {clientId} validado");
        }
    }

    private bool GUIcodigo()
    {
        //Comprobaciones varias para saber por qué no funcionaba        if (inputCode == null)
        {
            Debug.LogError("inputCode es NULL Asígnalo en el Inspector");
            return false;
        }


        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager.Instance es NULL Falta el GameManager en la escena");
            return false;
        
        }

        string codigoIntroducido = inputCode.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(codigoIntroducido))
        {
            Debug.Log("El campo de código está vacío.");
            return false;
        }

        string codigoSala = GameManager.Instance.RoomCode.ToString();

        if (codigoIntroducido == codigoSala)
        {
            Debug.Log("Código correcto.");
            return true;
        }
        else
        {
            Debug.Log($"Código incorrecto. Introducido: {codigoIntroducido} | Esperado: {codigoSala}");
            return false;
        }
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
        string code = ""; //no pongais un espacio que si no se inicializa con uno

        for (int i = 0; i < 6; i++)
        {
            int index = UnityEngine.Random.Range(0, characters.Length);
            code += characters[index];

        }

        return code;
    }
}