using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PracticeWall : MonoBehaviour
{
    [Tooltip("Si true, devuelve la pelota exactamente (sin aleatoriedad). Si false, añade pequeña variación en Z.")]
    public bool randomBounce = false;

    void OnCollisionEnter(Collision col)
    {
        if (col == null || col.gameObject == null) return;
        var ball = col.gameObject.GetComponent<PingPongBall>();
        if (ball == null) return;

        var rb = col.rigidbody;
        if (rb == null) return;

        Vector3 vel = rb.linearVelocity;
        // Invertir X para devolver al jugador
        float newX = -vel.x;
        float newZ = -vel.z;

        if (randomBounce)
            newZ += Random.Range(-0.4f, 0.4f);

        float minX = 4f;
        if (Mathf.Abs(newX) < minX)
            newX = Mathf.Sign(newX != 0f ? newX : col.contacts[0].normal.x) * minX;

        Vector3 newVel = new Vector3(newX, vel.y, newZ);
        rb.position = col.contacts[0].point + col.contacts[0].normal * 0.12f;
        rb.WakeUp();
        rb.linearVelocity = newVel;
    }
}
