using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    public GameObject obstaclePrefab;

    public float minX = -3f;
    public float maxX = 3f;
    public float spawnY = 6f;

    public float spawnInterval = 1.2f;

    float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnObstacle();
            timer = 0f;
        }
    }

    void SpawnObstacle()
    {
        float x = Random.Range(minX, maxX);
        Vector3 pos = new Vector3(x, spawnY, 0f);
        Instantiate(obstaclePrefab, pos, Quaternion.identity);
    }
}
