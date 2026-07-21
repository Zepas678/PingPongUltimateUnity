using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ColliderSeguidor : MonoBehaviour
{
    [Tooltip("La raqueta que debe seguir")]
    public Transform objetivo;

    [Tooltip("Script de animación de la raqueta")]
    public RaquetaAnimacion animacion;

    [Tooltip("Script de golpe de la raqueta")]
    public RaquetaGolpe golpe;

    void Update()
    {
        if (objetivo == null) return;
        transform.position = objetivo.position;
    }

    public void TriggerGolpe()
    {
        animacion?.AnimarGolpe();
    }

    public RaquetaGolpe GetRaquetaGolpe() => golpe;
}