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
}