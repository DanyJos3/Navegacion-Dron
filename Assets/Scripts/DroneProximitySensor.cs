
using UnityEngine;

public class DroneProximitySensor : MonoBehaviour
{
    [Header("Configuración del sensor")]
    public int numberOfRays = 16;
    public float detectionDistance = 15f;
    public LayerMask obstacleLayer;

    [Header("Visualización")]
    public bool showRays = true;

    [Header("Lecturas")]
    public float[] distances;

    void Awake()
    {
        numberOfRays = Mathf.Max(1, numberOfRays);
        distances = new float[numberOfRays];
    }

    void FixedUpdate()
    {
        ScanEnvironment();
    }

    void ScanEnvironment()
    {
        Vector3 origin = transform.position;

        for (int i = 0; i < numberOfRays; i++)
        {
            float angle = i * 360f / numberOfRays;

            Vector3 direction =
                Quaternion.Euler(0f, angle, 0f) *
                transform.forward;

            RaycastHit hit;

            if (Physics.Raycast(
                origin,
                direction,
                out hit,
                detectionDistance,
                obstacleLayer,
                QueryTriggerInteraction.Ignore))
            {
                distances[i] = hit.distance;

                if (showRays)
                {
                    Debug.DrawRay(
                        origin,
                        direction * hit.distance,
                        Color.red
                    );
                }
            }
            else
            {
                distances[i] = detectionDistance;

                if (showRays)
                {
                    Debug.DrawRay(
                        origin,
                        direction * detectionDistance,
                        Color.green
                    );
                }
            }
        }
    }

    public float[] GetNormalizedDistances()
    {
        float[] normalized =
            new float[distances.Length];

        for (int i = 0; i < distances.Length; i++)
        {
            normalized[i] =
                distances[i] / detectionDistance;
        }

        return normalized;
    }
}
