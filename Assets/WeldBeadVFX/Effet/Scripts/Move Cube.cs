using UnityEngine;
using UnityEngine.InputSystem;

public class MoveCube : MonoBehaviour
{

    [SerializeField] private float moveSpeed = 0.5f;

    void Update()
    {
        if (Keyboard.current == null) return;

        float direction = 0f;

        if (Keyboard.current.leftArrowKey.isPressed)  direction = -1f;
        if (Keyboard.current.rightArrowKey.isPressed) direction =  1f;

        transform.Translate(Vector3.right * direction * moveSpeed * Time.deltaTime);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created

}
