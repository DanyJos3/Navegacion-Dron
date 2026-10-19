
using UnityEngine;

public class DroneCameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    public Transform target;

    [Header("Posición de la cámara")]
    public Vector3 offset = new Vector3(0f, 3f, -7f);

    [Header("Suavizado")]
    public float smoothSpeed = 5f;

    [Header("Punto de enfoque")]
    public float lookHeight = 1f;

    // Al dar Play, si ninguna cámara sigue al dron, la Main Camera lo sigue desde atrás.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AttachToMainCamera()
    {
        DroneAgent drone = FindAnyObjectByType<DroneAgent>();
        if (Camera.main == null || drone == null || FindAnyObjectByType<DroneCameraFollow>() != null)
            return;

        // La cámara de la escena es ortográfica: con perspectiva los edificios se ven sólidos.
        Camera.main.orthographic = false;
        Camera.main.fieldOfView = 60f;

        DroneCameraFollow follow = Camera.main.gameObject.AddComponent<DroneCameraFollow>();
        follow.target = drone.transform;
        follow.offset = new Vector3(0f, 20f, -22f);
        follow.smoothSpeed = 4f;
    }

    // El dron se mueve en FixedUpdate (50 pasos/s) sin interpolación: con un frame por paso
    // de física la cámara y el dron avanzan juntos y no hay temblor.
    void Start()
    {
        Application.targetFrameRate = Mathf.RoundToInt(1f / Time.fixedDeltaTime);
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        // Posición deseada con offset en coordenadas del mundo: la cámara no gira con el dron
        // (A -> B avanza en +Z, así que Z negativo queda detrás del recorrido)
        Vector3 desiredPosition =
            target.position + offset;

        Vector3 lookTarget =
            target.position + Vector3.up * lookHeight;

        // Si un edificio queda entre el dron y la cámara, acercarla para no atravesarlo
        if (Physics.Linecast(lookTarget, desiredPosition, out RaycastHit hit,
                LayerMask.GetMask("Obstacles"), QueryTriggerInteraction.Ignore))
            desiredPosition = hit.point + hit.normal * 0.3f;

        // Seguimiento suave
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        // Mantener la cámara mirando al dron
        transform.LookAt(lookTarget);
    }
}
