using UnityEngine;

public class PracticeBallListener : MonoBehaviour
{
    private int bounces = 0;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision collision)
    {
        // Contar rebotes en cualquier superficie (tabla, raqueta, muro)
        bounces++;
    }

    void OnTriggerEnter(Collider other)
    {
        // No contar triggers como rebotes
    }

    public int GetBounces() => bounces;

    public float GetCurrentSpeed() => rb != null ? rb.linearVelocity.magnitude : 0f;

    public void ResetCounters()
    {
        bounces = 0;
    }
}
