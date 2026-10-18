
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class DroneController : MonoBehaviour
{
    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float verticalSpeed = 3f;

    [Header("Rotación")]
    public float rotationSpeed = 90f;

    private Rigidbody rb;

    private float forwardInput;
    private float rotationInput;
    private float verticalInput;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = false;

        rb.interpolation = RigidbodyInterpolation.Interpolate;

        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        // Avanzar / retroceder
        forwardInput = 0f;

        if (keyboard.wKey.isPressed)
            forwardInput += 1f;

        if (keyboard.sKey.isPressed)
            forwardInput -= 1f;

        // Girar izquierda / derecha
        rotationInput = 0f;

        if (keyboard.aKey.isPressed)
            rotationInput -= 1f;

        if (keyboard.dKey.isPressed)
            rotationInput += 1f;

        // Subir / bajar
        verticalInput = 0f;

        if (keyboard.upArrowKey.isPressed)
            verticalInput += 1f;

        if (keyboard.downArrowKey.isPressed)
            verticalInput -= 1f;
    }

    void FixedUpdate()
    {
        // Rotar sobre el eje vertical
        Quaternion deltaRotation = Quaternion.Euler(
            0f,
            rotationInput * rotationSpeed * Time.fixedDeltaTime,
            0f
        );

        Quaternion newRotation = rb.rotation * deltaRotation;

        rb.MoveRotation(newRotation);

        // Dirección frontal según el giro del dron
        Vector3 forwardDirection =
            newRotation * Vector3.forward;

        forwardDirection.y = 0f;
        forwardDirection.Normalize();

        // Calcular velocidad deseada
        Vector3 horizontalVelocity =
            forwardDirection * forwardInput * moveSpeed;

        Vector3 verticalVelocity =
            Vector3.up * verticalInput * verticalSpeed;

        // Aplicar velocidad mediante física
        rb.linearVelocity =
            horizontalVelocity + verticalVelocity;
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log(
            "El dron colisionó con: " +
            collision.gameObject.name
        );
    }
}
