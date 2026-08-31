using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Control del Jugador 2 para modo PvP.
/// </summary>
public class RaquetaControlP2 : MonoBehaviour
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

    void Awake()
    {
        destinoZ  = transform.position.z;
        animacion = GetComponent<RaquetaAnimacion>();
        golpe     = GetComponent<RaquetaGolpe>();
        // No desactivar aquí — el GameManager se encarga del estado según el modo
    }

    void Update()
    {
        if (golpe != null && golpe.EstaDestruida) return;

        // Input directo por teclado — más confiable que Player Input para P2
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            destinoZ = zDerecha;
            animacion?.AnimarMovimiento(-1);
        }
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            destinoZ = zIzquierda;
            animacion?.AnimarMovimiento(1);
        }
        else if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            destinoZ = zCentro;
            animacion?.AnimarMovimiento(0);
        }

        // Golpe con Numpad2
        if (Input.GetKeyDown(KeyCode.Keypad2))
            golpe?.OnGolpe();

        // Mover hacia destino
        Vector3 objetivo = new Vector3(transform.position.x,
                                       transform.position.y,
                                       destinoZ);
        transform.position = Vector3.MoveTowards(transform.position,
                                                  objetivo,
                                                  velocidad * Time.deltaTime);
    }

    void OnEnable()
    {
    Debug.Log("[S/ RaquetaControlP2] OnEnable\n" + System.Environment.StackTrace);
    }

    void OnDisable()
    {
    Debug.Log("[S/ RaquetaControlP2] OnDisable\n" + System.Environment.StackTrace);
    }
}