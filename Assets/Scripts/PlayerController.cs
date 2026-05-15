using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : CharController
{
    protected int damageToEnemy;
    protected float attackCooldown;

    private PlayerControls controls;

    public bool IsAttacking { get; private set; } = false;
    public int DamageToEnemy => damageToEnemy;
    public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>();


    /// <summary>
    /// Inicializa controles de entrada y registra el jugador local en el gestor global.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();

        controls.Player.Move.performed += ctx => movement = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => movement = Vector2.zero;

        // ✅ Ocultar hasta que LevelGenerator lo reposicione
        gameObject.SetActive(false);

        UniqueEntity uniqueEntity = GetComponent<UniqueEntity>();
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterLocalPlayer(this, uniqueEntity);
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Dispara eventos iniciales para actualizar el HUD
        GameEvents.HealthChanged(health);
        GameEvents.KeysChanged();
        GameEvents.DiamondsChanged();

        IsAttacking = false;

        // Sincronizar posición inicial
        if (IsOwner)
        {
            Position.Value = (Vector2)transform.position;
        }

        // Suscribir a cambios de posición para otros jugadores
        Position.OnValueChanged += onPositionChanged;
    }


    /// <summary>
    /// Inicializa estado del jugador y notifica los valores iniciales al HUD.
    /// </summary>
    /*protected override void Start()
    {
        base.Start();

    }*/

    /// <summary>
    /// Actualiza animación, orientación y estado de vida en cada frame.
    /// </summary>
    protected override void Update()
    {
        if (!IsOwner) return; //si no eres el jugador no puedes mover el jugador

        animator.SetFloat("speed", movement.sqrMagnitude);

        if (movement.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        checkDeath();
    }

    /// <summary>
    /// Override de FixedUpdate para sincronizar posición del owner al servidor.
    /// </summary>
    protected override void FixedUpdate()
    {
        // El owner calcula su propio movimiento
        if (IsOwner && !isKnockback && !isDead)
        {
            base.FixedUpdate();
            // Enviar posición actualizada al servidor
            UpdatePositionServerRpc((Vector2)transform.position);
        }
        else if (!IsOwner)
        {
            // Los otros clientes usan la posición sincronizada de la red
            // (se actualiza mediante onPositionChanged)
            if (isKnockback)
            {
                knockbackTimer -= Time.fixedDeltaTime;
                if (knockbackTimer <= 0f)
                {
                    isKnockback = false;
                    rb.linearVelocity = Vector2.zero;
                }
            }
        }
        else if (isDead || isKnockback)
        {
            // Si el owner está en knockback o muerto, aplicar física normal
            base.FixedUpdate();
        }
    }

    /// <summary>
    /// Activa el mapa de controles y suscribe la acción de ataque.
    /// </summary>
    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.performed += onAttack;
    }

    /// <summary>
    /// Desuscribe la acción de ataque y desactiva el mapa de controles.
    /// </summary>
    private void OnDisable()
    {
        controls.Player.Attack.performed -= onAttack;
        controls.Disable();
    }

    /// <summary>
    /// Gestiona la muerte del jugador y lanza el flujo de fin de partida.
    /// </summary>
    public override void Die()
    {
        base.Die();

        // Dispara evento de muerte
        GameEvents.PlayerDied();

        GameManager.Instance?.TriggerGameOver();

    }

    /// <summary>
    /// Aplica daño al jugador y notifica el cambio de salud al HUD.
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);

        // Dispara evento de cambio de salud
        GameEvents.HealthChanged(health);
    }

    /// <summary>
    /// Aplica un conjunto de estadísticas de personaje y recarga sus valores activos.
    /// </summary>
    public void ApplyCharacterStats(PlayerStats newStats)
    {
        if (newStats == null)
        {
            Debug.LogWarning("[PlayerController] ApplyCharacterStats llamado con null");
            return;
        }

        stats = newStats;

        // Recargar todas las stats
        LoadStats();

        Debug.Log($"[PlayerController] Stats aplicadas: {newStats.characterName}");
    }

    /// <summary>
    /// Carga estadísticas del personaje seleccionado y aplica valores de combate y movimiento.
    /// </summary>
    protected override void LoadStats()
    {
        // ✅ PRIMERO: Intenta cargar desde GameManager (personaje seleccionado)
        if (GameManager.Instance != null && GameManager.Instance.SelectedCharacterStats != null)
        {
            stats = GameManager.Instance.SelectedCharacterStats;
            Debug.Log($"[PlayerController] Cargando personaje seleccionado: {stats.characterName}");
        }

        // Si no hay personaje seleccionado, usa el asignado en el prefab (fallback)
        if (stats == null)
        {
            Debug.LogWarning("[PlayerController] No hay personaje seleccionado, usando stats por defecto del prefab");
        }

        base.LoadStats();

        // ✅ Haz casting del campo heredado
        PlayerStats playerStats = stats as PlayerStats;

        if (playerStats != null)
        {
            // Aplica el bonus de velocidad del jugador
            moveSpeed *= playerStats.speedBonus;
            
            // Carga stats específicas del jugador
            damageToEnemy = playerStats.attackDamage;
            attackCooldown = playerStats.attackCooldown;
        }
        else
        {
            // Valores por defecto si no hay PlayerStats
            Debug.LogWarning($"[{gameObject.name}] No tiene PlayerStats asignado. Usando valores por defecto.");
            damageToEnemy = 50;
            attackCooldown = 0.5f;
            moveSpeed *= 1.25f; // Bonus por defecto
        }
    }

    /// <summary>
    /// RPC que envía la posición del owner al servidor para sincronizar con otros clientes.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void UpdatePositionServerRpc(Vector2 newPosition)
    {
        // Solo el servidor actualiza la NetworkVariable
        Position.Value = newPosition;
    }

    /// <summary>
    /// Callback que se ejecuta cuando la posición sincronizada cambia en otros clientes.
    /// </summary>
    private void onPositionChanged(Vector2 oldPosition, Vector2 newPosition)
    {
        // Los clientes que NO son owner aplican la posición sincronizada
        if (!IsOwner && !isKnockback && !isDead)
        {
            transform.position = newPosition;
        }
    }

    /// <summary>
    /// Verifica si la salud ha llegado a cero y ejecuta la muerte una sola vez.
    /// </summary>
    private void checkDeath()
    {
        if (health <= 0 && !isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// Inicia la animación de ataque y programa su final según el cooldown.
    /// </summary>
    private void onAttack(InputAction.CallbackContext context)
    {
        animator.SetTrigger("Attack");
        IsAttacking = true;
        Invoke(nameof(endAttack), attackCooldown);
    }

    /// <summary>
    /// Finaliza el estado de ataque del jugador.
    /// </summary>
    private void endAttack()
    {
        IsAttacking = false;
    }
}
