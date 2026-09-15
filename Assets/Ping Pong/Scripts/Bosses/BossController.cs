using UnityEngine;

/// <summary>
/// Clase base para todos los jefes.
/// Contiene únicamente la funcionalidad común de un combate de jefe.
/// Las subclases (BossZeus, BossColossus, BossMirage) implementan
/// las habilidades específicas sobrescribiendo los métodos virtuales.
/// </summary>
public class BossController : MonoBehaviour
{
    [Header("Datos del jefe")]
    [Tooltip("Información del jefe (configurada desde el Inspector en BossManager).")]
    public BossData bossData;

    [Header("Estado del combate")]
    [Tooltip("Indica si el combate contra este jefe está en curso.")]
    public bool combateActivo = false;

    [Tooltip("Indica si este jefe ya ha sido derrotado.")]
    public bool derrotado = false;

    [Header("Referencias")]
    [Tooltip("Referencia al GameManager de la escena.")]
    public GameManager gameManager;

    [Tooltip("Referencia a la pelota del partido.")]
    public PingPongBall ball;

    [Tooltip("Transform del jugador.")]
    public Transform jugador;

    [Tooltip("Transform de este jefe.")]
    public Transform jefe;

    [Tooltip("Transform de la mesa del escenario del jefe (para calcular límites de la mesa).")]
    public Transform mesa;

    // ──────────────────────────────────────────────
    /// <summary>
    /// Inicia el combate contra este jefe.
    /// Las subclases pueden sobrescribir este método para
    /// agregar comportamiento específico al comenzar.
    /// </summary>
    public virtual void IniciarCombate()
    {
        combateActivo = true;
        derrotado = false;

        if (bossData != null)
            bossData.derrotado = false;

        // Centralizar la activación del escenario del jefe: antes de lanzar la
        // pelota, MapManager activa el mapa guiado por BossData (mapaAsociado /
        // nombreMapa). Así la carga del mapa es automática y modular para
        // CUALQUIER jefe, sin hardcodear nombres de mapas en los scripts de los jefes.
        if (bossData != null && MapManager.Instance != null)
            MapManager.Instance.SeleccionarMapaBoss(bossData);

        Debug.Log("[BossController] Combate iniciado contra " + (bossData != null ? bossData.nombre : "Jefe sin datos"));
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Finaliza el combate contra este jefe.
    /// Las subclases pueden sobrescribir este método para
    /// agregar comportamiento específico al terminar.
    /// </summary>
    public virtual void FinalizarCombate()
    {
        combateActivo = false;

        Debug.Log("[BossController] Combate finalizado contra " + (bossData != null ? bossData.nombre : "Jefe sin datos"));
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Activa la habilidad especial del jefe.
    /// La implementación específica está en cada subclase.
    /// </summary>
    public virtual void ActivarHabilidad()
    {
        // Sin implementación base: cada jefe define su propia habilidad.
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Desactiva la habilidad especial del jefe.
    /// La implementación específica está en cada subclase.
    /// </summary>
    public virtual void DesactivarHabilidad()
    {
        // Sin implementación base: cada jefe define su propia habilidad.
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Notifica al jefe activo que el JUGADOR le anotó un punto (evento real
    /// invocado por GameManager.RegisterPoint cuando puntúa el jugador). La
    /// implementación base no hace nada; cada jefe puede reaccionar a su manera
    /// (p. ej. Colossus crece, rearma su inmunidad por puntos y avanza de fase).
    /// </summary>
    public virtual void OnPuntoDelJugador()
    {
        // Sin implementación base: solo los jefes que lo necesiten lo sobrescriben.
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Consulta si este jefe puede resistir un impacto de la pelota a la
    /// velocidad indicada. Por defecto los jefes NO se rompen por velocidad
    /// estándar (return true).
    /// Las subclases (p. ej. BossColossus) sobrescriben este método para
    /// gestionar cargas de inmunidad/resistencia en modo épico: mientras tengan
    /// cargas absorben el impacto (return true) y al agotarse rompen (false).
    /// NOTA: es una API de jefe independiente de RaquetaGolpe.PuedeResistir
    /// (que es la lógica de la raqueta genérica del jugador/CPU).
    /// </summary>
    public virtual bool PuedeResistir(float velocidadPelota)
    {
        return true; // Por defecto los jefes no se rompen por velocidad estándar
    }

    // ──────────────────────────────────────────────
    /// <summary>
    /// Obtiene el centro y el tamaño reales de la mesa del escenario de forma
    /// generalizada (por tag "Mesa" o por la referencia 'mesa' de esta clase).
    /// Devuelve false si no se encuentra ninguna mesa.
    /// </summary>
    public bool ObtenerDatosMesa(out Vector3 centro, out Vector3 tamano)
    {
        centro = Vector3.zero;
        tamano = Vector3.zero;

        BoxCollider colliderMesa = null;

        // 1) Buscar la mesa por tag "Mesa" (robusto si el tag no está definido).
        try
        {
            GameObject mesaPorTag = GameObject.FindGameObjectWithTag("Mesa");
            if (mesaPorTag != null)
                colliderMesa = mesaPorTag.GetComponent<BoxCollider>();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BossController] No existe el tag 'Mesa' o fallo al buscarlo: {e.Message}");
        }

        // 2) Usar la referencia explícita de mesa configurada en esta clase base.
        if (colliderMesa == null && mesa != null)
            colliderMesa = mesa.GetComponent<BoxCollider>();

        // 3) Fallback: la mesa es el propio objeto del jefe (si tiene BoxCollider).
        if (colliderMesa == null && jefe != null)
            colliderMesa = jefe.GetComponent<BoxCollider>();

        if (colliderMesa != null)
        {
            centro = colliderMesa.bounds.center;
            tamano = colliderMesa.bounds.size;
            return true;
        }

        return false;
    }
}