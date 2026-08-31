using UnityEngine;
using UnityEngine.InputSystem;

public class RaquetaControl : MonoBehaviour
{
    [Header("Zonas en Z (eje de movimiento)")]
    public float zIzquierda = 20f;
    public float zCentro    = 0f;
    public float zDerecha   = -20f;

    [Header("Velocidad de movimiento")]
    public float velocidad = 400f;

    private float            destinoZ;
    private RaquetaAnimacion animacion;
    private RaquetaGolpe     golpe;

    void Start()
    {
        destinoZ  = transform.position.z;
        animacion = GetComponent<RaquetaAnimacion>();
        golpe     = GetComponent<RaquetaGolpe>();
    }

    void Update()
    {
        if (golpe != null && golpe.EstaDestruida) return;

        Vector3 objetivo = new Vector3(transform.position.x,
                                       transform.position.y,
                                       destinoZ);
        transform.position = Vector3.MoveTowards(transform.position,
                                                  objetivo,
                                                  velocidad * Time.deltaTime);
    }

    public void OnMover(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        if (golpe != null && golpe.EstaDestruida) return;

        string tecla = context.control.name;

        switch (tecla)
        {
            case "a": case "leftArrow":
                destinoZ = zDerecha;
                animacion?.AnimarMovimiento(-1);
                break;

            case "d": case "rightArrow":
                destinoZ = zIzquierda;
                animacion?.AnimarMovimiento(1);
                break;

            case "s": case "downArrow": case "upArrow":
                destinoZ = zCentro;
                animacion?.AnimarMovimiento(0);
                break;
        }
    }
}