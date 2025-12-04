using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerCar : MonoBehaviour
{
    [Header("Movement")]
    public float steerSpeed = 5f;
    public float minX = -3.5f;   
    public float maxX = 3.5f;   

    [Header("Hit Feedback")]
    public float flashDuration = 0.1f;
    public int flashCount = 3;

    bool isFlashing = false;

    void Update()
    {
        float input = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed)
                input -= 1f;

            if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed)
                input += 1f;
        }

        if (Gamepad.current != null)
        {
            float stickX = Gamepad.current.leftStick.x.ReadValue();
            if (Mathf.Abs(stickX) > 0.1f)
                input = stickX;
        }

        Vector3 pos = transform.position;
        pos.x += input * steerSpeed * Time.deltaTime;
        pos.x = Mathf.Clamp(pos.x, minX, maxX);  
        transform.position = pos;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Obstacle"))
        {
            SpriteRenderer carSR = GetComponent<SpriteRenderer>();
            SpriteRenderer obstacleSR = collision.GetComponent<SpriteRenderer>();

            if (!isFlashing)
            {
                StartCoroutine(FlashHit(carSR, obstacleSR));
            }

            if (MinigameManager.Instance != null)
            {
                MinigameManager.Instance.RegisterCarCollision();
            }
        }
    }

    IEnumerator FlashHit(SpriteRenderer carSR, SpriteRenderer obstacleSR)
    {
        isFlashing = true;

        if (carSR == null)
        {
            isFlashing = false;
            yield break;
        }

        Color carOriginal = carSR.color;
        Color obstacleOriginal = obstacleSR != null ? obstacleSR.color : Color.white;
        Color flashColor = Color.red;

        for (int i = 0; i < flashCount; i++)
        {
            carSR.color = flashColor;
            if (obstacleSR != null) obstacleSR.color = flashColor;

            yield return new WaitForSeconds(flashDuration);

            carSR.color = carOriginal;
            if (obstacleSR != null) obstacleSR.color = obstacleOriginal;

            yield return new WaitForSeconds(flashDuration);
        }

        isFlashing = false;
    }
}
