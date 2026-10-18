
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DroneMissionController : MonoBehaviour
{
    public enum MissionState
    {
        Takeoff,
        Navigate,
        Land,
        Completed
    }

    [Header("Coordenadas de la misión")]
    public Vector3 startPosition =
        new Vector3(4.503191f, 0f, -23.59933f);

    public Vector3 targetPosition =
        new Vector3(-15.77406f, 0f, 31.15596f);

    [Header("Configuración de vuelo")]
    public float flightHeight = 15f; // 5 
    public float horizontalSpeed = 4f;
    public float verticalSpeed = 2f;
    public float rotationSpeed = 120f;

    [Header("Tolerancias")]
    public float positionTolerance = 0.5f;
    public float landingTolerance = 0.1f;

    [Header("Estado actual")]
    public MissionState currentState;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false;

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        rb.collisionDetectionMode =
            CollisionDetectionMode.ContinuousSpeculative;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void Start()
    {
        // Ubicar el dron en el origen
        rb.position = startPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        currentState = MissionState.Takeoff;

        Debug.Log("Misión iniciada: despegue");
    }

    void FixedUpdate()
    {
        switch (currentState)
        {
            case MissionState.Takeoff:
                Takeoff();
                break;

            case MissionState.Navigate:
                Navigate();
                break;

            case MissionState.Land:
                Land();
                break;

            case MissionState.Completed:
                rb.linearVelocity = Vector3.zero;
                break;
        }
    }

    void Takeoff()
    {
        float altitudeError =
            flightHeight - rb.position.y;

        if (altitudeError > positionTolerance)
        {
            float climbSpeed = Mathf.Min(
                verticalSpeed,
                altitudeError / Time.fixedDeltaTime
            );

            rb.linearVelocity =
                Vector3.up * climbSpeed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
            currentState = MissionState.Navigate;

            Debug.Log(
                "Altura alcanzada. Iniciando navegación."
            );
        }
    }

    void Navigate()
    {
        Vector3 targetAtHeight = new Vector3(
            targetPosition.x,
            flightHeight,
            targetPosition.z
        );

        Vector3 horizontalDirection =
            targetAtHeight - rb.position;

        horizontalDirection.y = 0f;

        float distance = horizontalDirection.magnitude;

        if (distance <= positionTolerance)
        {
            rb.linearVelocity = Vector3.zero;
            currentState = MissionState.Land;

            Debug.Log(
                "Destino alcanzado. Iniciando aterrizaje."
            );

            return;
        }

        Vector3 direction = horizontalDirection.normalized;

        // Girar hacia el destino
        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        Quaternion newRotation =
            Quaternion.RotateTowards(
                rb.rotation,
                targetRotation,
                rotationSpeed * Time.fixedDeltaTime
            );

        rb.MoveRotation(newRotation);

        // Avanzar solo cuando esté orientado
        float angle = Quaternion.Angle(
            newRotation,
            targetRotation
        );

        float alignment = Mathf.Clamp01(
            1f - angle / 90f
        );

        float speed = Mathf.Min(
            horizontalSpeed * alignment,
            distance / Time.fixedDeltaTime
        );

        // Corregir altura durante la navegación
        float altitudeError =
            flightHeight - rb.position.y;

        float climbSpeed = Mathf.Clamp(
            altitudeError / Time.fixedDeltaTime,
            -verticalSpeed,
            verticalSpeed
        );

        rb.linearVelocity =
            direction * speed +
            Vector3.up * climbSpeed;
    }

    void Land()
    {
        float altitudeError =
            targetPosition.y - rb.position.y;

        if (Mathf.Abs(altitudeError) > landingTolerance)
        {
            float speed = Mathf.Clamp(
                altitudeError / Time.fixedDeltaTime,
                -verticalSpeed,
                verticalSpeed
            );

            rb.linearVelocity =
                Vector3.up * speed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;

            currentState = MissionState.Completed;

            Debug.Log(
                "Misión completada. Dron aterrizado."
            );
        }
    }

    void OnDrawGizmos()
    {
        Vector3 takeoffPoint = new Vector3(
            startPosition.x,
            flightHeight,
            startPosition.z
        );

        Vector3 flightTarget = new Vector3(
            targetPosition.x,
            flightHeight,
            targetPosition.z
        );

        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPosition, takeoffPoint);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(takeoffPoint, flightTarget);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(flightTarget, targetPosition);

        Gizmos.DrawWireSphere(
            targetPosition,
            positionTolerance
        );
    }
}
