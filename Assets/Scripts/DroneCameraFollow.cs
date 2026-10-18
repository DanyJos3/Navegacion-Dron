
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

    void LateUpdate()
    {
        if (target == null)
            return;

        // Posición deseada detrás del dron
        Vector3 desiredPosition =
            target.position +
            target.rotation * offset;

        // Seguimiento suave
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            smoothSpeed * Time.deltaTime
        );

        // Mantener la cámara mirando al dron
        Vector3 lookTarget =
            target.position + Vector3.up * lookHeight;

        transform.LookAt(lookTarget);
    }
}
