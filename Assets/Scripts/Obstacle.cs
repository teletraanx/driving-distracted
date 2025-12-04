using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public float fallSpeed = 5f;
    public float destroyY = -7f;

    void Update()
    {
        transform.Translate(Vector2.down * fallSpeed * Time.deltaTime, Space.World);

        if (transform.position.y < destroyY)
        {
            Destroy(gameObject);
        }
    }
}
