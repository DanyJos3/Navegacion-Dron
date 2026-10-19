
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Policies;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(BehaviorParameters))]
public class DroneAgent : Agent
{
    [Header("Mission")]
    public Transform startPoint;
    public Transform targetPoint;

    [Header("Movement")]
    public float forwardSpeed = 8f;
    public float rotationSpeed = 90f;
    public float verticalSpeed = 2f;

    [Header("Altitude")]
    public float minAltitude = 6f;
    public float maxAltitude = 10f;

    [Header("Goal")]
    public float goalDistance = 2.5f;

    [Header("Sensors")]
    public float sensorDistance = 12f;
    public LayerMask obstacleLayers;

    [Header("Collision")]
    public float collisionRadius = 0.5f;

    [Header("Terminal Rewards")]
    public float goalReward = 20f;
    public float collisionPenalty = -8f;
    public float stagnationPenalty = -3f;

    [Header("Navigation Rewards")]
    public float progressRewardScale = 0.08f;
    public float stepPenalty = -0.0005f;
    public float alignmentRewardScale = 0.001f;
    public float wrongDirectionPenaltyScale = 0.001f;

    [Header("Safety Reward")]
    public float dangerDistance = 3f;
    public float proximityPenaltyScale = 0.002f;

    [Header("Distance Limit")]
    [Range(0f, 1f)]
    public float maximumDistanceTolerance = 0.20f;

    public float distanceLimitPenalty = -20f;

    [Header("Moving Away Penalty")]
    public float movingAwayPenaltyScale = 0.15f;

    [Header("Stagnation")]
    public int stagnationSteps = 600;
    // Minimum reduction in distance to the goal required to reset stagnation.
    public float minimumGoalProgress = 1.0f;

    [Header("Path Efficiency")]
    // Small cost per Unity unit traveled; discourages unnecessary detours.
    public float pathLengthPenaltyScale = 0.005f;

    [Header("Episode")]
    public bool randomizeYaw = true;

    // Set Agent Max Step = 0 in Inspector.
    // This script controls episode termination.
    public int episodeMaxSteps = 3000;

    [Header("Automatic CSV")]
    public bool saveEpisodeCSV = true;

    [Header("Debug")]
    public bool enableDebug = false;
    public int debugInterval = 100;

    private Rigidbody rb;
    private BehaviorParameters behavior;

    private bool configurationValid;
    private bool episodeFinished;

    private float initialDistance;
    private float maximumAllowedDistance;
    private float previousDistance;

    private Vector3 measuredVelocity;
    private float progressAnchorDistance;

    private int stepsWithoutProgress;
    private int actionCounter;

    private float episodeDistanceTraveled;
    private float episodeMinimumDistance;
    private float episodeMaximumDistance;

    private Vector3 lastRecordedPosition;

    private DroneEvaluationLogger logger;

    private const int RayCount = 16;
    private const int ObservationCount = 23;
    private const int ContinuousActionCount = 3;

    public override void Initialize()
    {
        rb = GetComponent<Rigidbody>();
        behavior = GetComponent<BehaviorParameters>();

        rb.useGravity = false;
        rb.isKinematic = true;

        configurationValid = ValidateConfiguration();

        if (saveEpisodeCSV)
        {
            logger = GetComponent<DroneEvaluationLogger>();

            if (logger == null)
            {
                logger = gameObject.AddComponent<
                    DroneEvaluationLogger
                >();
            }
            logger.EnsureInitialized();
        }

        Debug.Log(
            "[DRONE INIT] " +
            $"Valid={configurationValid} | " +
            $"CSV={saveEpisodeCSV}",
            this
        );
    }

    private bool ValidateConfiguration()
    {
        if (startPoint == null || targetPoint == null)
        {
            Debug.LogError(
                "[DRONE] Missing Start Point or Target Point.",
                this
            );

            return false;
        }

        var spec = behavior.BrainParameters.ActionSpec;

        int continuous = spec.NumContinuousActions;
        int discrete = spec.NumDiscreteActions;
        int observations =
            behavior.BrainParameters.VectorObservationSize;

        if (continuous != ContinuousActionCount ||
            discrete != 0 ||
            observations != ObservationCount)
        {
            Debug.LogError(
                "[DRONE] Expected 23 observations, " +
                "3 continuous actions and 0 discrete branches.",
                this
            );

            return false;
        }

        if (maxAltitude <= minAltitude ||
            forwardSpeed <= 0f ||
            verticalSpeed <= 0f ||
            sensorDistance <= 0f ||
            collisionRadius <= 0f ||
            episodeMaxSteps <= 0 ||
            stagnationSteps <= 0 ||
            minimumGoalProgress <= 0f ||
            pathLengthPenaltyScale < 0f)
        {
            Debug.LogError(
                "[DRONE] Invalid movement or episode settings.",
                this
            );

            return false;
        }

        return true;
    }

    public override void OnEpisodeBegin()
    {
        episodeFinished = false;
        stepsWithoutProgress = 0;
        actionCounter = 0;

        if (!configurationValid)
            return;

        Vector3 spawn = startPoint.position;

        spawn.y = Mathf.Clamp(
            spawn.y,
            minAltitude,
            maxAltitude
        );

        Quaternion rotation = startPoint.rotation;

        if (randomizeYaw)
        {
            rotation = Quaternion.Euler(
                0f,
                Random.Range(0f, 360f),
                0f
            );
        }

        rb.position = spawn;
        rb.rotation = rotation;

        Physics.SyncTransforms();

        lastRecordedPosition = spawn;


        measuredVelocity = Vector3.zero;

        initialDistance = Vector3.Distance(
            spawn,
            targetPoint.position
        );

        maximumAllowedDistance =
            initialDistance *
            (1f + maximumDistanceTolerance);

        previousDistance = initialDistance;
        progressAnchorDistance = initialDistance;

        episodeMinimumDistance = initialDistance;
        episodeMaximumDistance = initialDistance;
        episodeDistanceTraveled = 0f;

        if (enableDebug)
        {
            Debug.Log(
                $"[RESET] Initial={initialDistance:F2} | " +
                $"Limit={maximumAllowedDistance:F2}",
                this
            );
        }
    }

    public override void CollectObservations(
        VectorSensor sensor
    )
    {
        if (!configurationValid)
        {
            for (int i = 0; i < ObservationCount; i++)
            {
                sensor.AddObservation(0f);
            }

            return;
        }

        Vector3 direction =
            targetPoint.position - transform.position;

        Vector3 localDirection =
            transform.InverseTransformDirection(direction);

        Vector3 normalizedDirection =
            localDirection.sqrMagnitude > 0.0001f
                ? localDirection.normalized
                : Vector3.zero;

        // Observations 1-3: Direction
        sensor.AddObservation(normalizedDirection);

        // Observation 4: Distance
        sensor.AddObservation(
            Mathf.Clamp01(direction.magnitude / 100f)
        );

        // Observation 5: Altitude
        float altitudeRange = Mathf.Max(
            0.01f,
            maxAltitude - minAltitude
        );

        sensor.AddObservation(
            Mathf.Clamp01(
                (transform.position.y - minAltitude) /
                altitudeRange
            )
        );

        // Observations 6-7: Local velocity
        Vector3 localVelocity =
            transform.InverseTransformDirection(
                measuredVelocity
            );

        sensor.AddObservation(
            Mathf.Clamp(
                localVelocity.x / forwardSpeed,
                -1f,
                1f
            )
        );

        sensor.AddObservation(
            Mathf.Clamp(
                localVelocity.z / forwardSpeed,
                -1f,
                1f
            )
        );

        // Observations 8-23: Horizontal raycasts
        for (int i = 0; i < RayCount; i++)
        {
            float angle = i * (360f / RayCount);

            Vector3 rayDirection =
                Quaternion.Euler(0f, angle, 0f) *
                transform.forward;

            float observation = 1f;

            if (Physics.Raycast(
                transform.position,
                rayDirection,
                out RaycastHit hit,
                sensorDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            ))
            {
                observation =
                    hit.distance / sensorDistance;
            }

            sensor.AddObservation(observation);
        }
    }

    public override void OnActionReceived(
        ActionBuffers actions
    )
    {
        if (!configurationValid || episodeFinished)
            return;

        if (actions.ContinuousActions.Length !=
            ContinuousActionCount)
        {
            Debug.LogError(
                "[DRONE] Invalid action count.",
                this
            );

            return;
        }

        float forward = Mathf.Clamp(
            actions.ContinuousActions[0],
            -1f,
            1f
        );

        float yaw = Mathf.Clamp(
            actions.ContinuousActions[1],
            -1f,
            1f
        );

        float vertical = Mathf.Clamp(
            actions.ContinuousActions[2],
            -1f,
            1f
        );

        float dt = Time.fixedDeltaTime;

        Vector3 currentPosition = rb.position;

        Quaternion nextRotation =
            rb.rotation *
            Quaternion.Euler(
                0f,
                yaw * rotationSpeed * dt,
                0f
            );

        Vector3 nextForward =
            nextRotation * Vector3.forward;

        Vector3 movement =
            nextForward * forward * forwardSpeed +
            Vector3.up * vertical * verticalSpeed;

        Vector3 nextPosition =
            currentPosition + movement * dt;

        nextPosition.y = Mathf.Clamp(
            nextPosition.y,
            minAltitude,
            maxAltitude
        );

        Vector3 displacement =
            nextPosition - currentPosition;

        float displacementDistance =
            displacement.magnitude;

        // Detect obstacles BEFORE moving.
        bool collisionDetected = false;

        if (displacementDistance > 0.0001f)
        {
            collisionDetected = Physics.SphereCast(
                currentPosition,
                collisionRadius,
                displacement.normalized,
                out RaycastHit collisionHit,
                displacementDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        if (!collisionDetected)
        {
            collisionDetected = Physics.CheckSphere(
                nextPosition,
                collisionRadius,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            );
        }

        if (collisionDetected)
        {
            AddReward(collisionPenalty);

            FinishEpisode(
                "collision",
                currentPosition
            );

            return;
        }

        // Execute movement
        rb.MoveRotation(nextRotation);
        rb.MovePosition(nextPosition);

        measuredVelocity =
            displacement /
            Mathf.Max(dt, 0.0001f);


        actionCounter++;

        episodeDistanceTraveled += Vector3.Distance(
            lastRecordedPosition,
            nextPosition
        );

        lastRecordedPosition = nextPosition;

        float currentDistance = Vector3.Distance(
            nextPosition,
            targetPoint.position
        );

        episodeMinimumDistance = Mathf.Min(
            episodeMinimumDistance,
            currentDistance
        );

        episodeMaximumDistance = Mathf.Max(
            episodeMaximumDistance,
            currentDistance
        );

        // -------------------------------------
        // 1. Time and path-length penalties
        // -------------------------------------
        AddReward(stepPenalty);
        AddReward(-displacementDistance * pathLengthPenaltyScale);

        // -------------------------------------
        // 2. Progress reward
        // -------------------------------------
        float progress =
            previousDistance - currentDistance;

        AddReward(
            progress * progressRewardScale
        );

        // -------------------------------------
        // 3. Moving away penalty
        // -------------------------------------
        if (progress < 0f)
        {
            float movingAwayDistance = -progress;

            AddReward(
                -movingAwayDistance *
                movingAwayPenaltyScale
            );
        }

        previousDistance = currentDistance;

        // -------------------------------------
        // 4. Distance limit
        // -------------------------------------
        if (currentDistance > maximumAllowedDistance)
        {
            AddReward(distanceLimitPenalty);

            FinishEpisode(
                "distance_limit",
                nextPosition
            );

            return;
        }

        // -------------------------------------
        // 5. Direction reward
        // -------------------------------------
        Vector3 toTarget =
            targetPoint.position - currentPosition;

        Vector3 horizontalTarget = new Vector3(
            toTarget.x,
            0f,
            toTarget.z
        );

        Vector3 horizontalMovement = new Vector3(
            displacement.x,
            0f,
            displacement.z
        );

        if (horizontalTarget.sqrMagnitude > 0.0001f &&
            horizontalMovement.sqrMagnitude > 0.000001f)
        {
            float alignment = Vector3.Dot(
                horizontalMovement.normalized,
                horizontalTarget.normalized
            );

            float movementFraction = Mathf.Clamp01(
                horizontalMovement.magnitude /
                Mathf.Max(0.0001f, forwardSpeed * dt)
            );

            if (alignment > 0f)
            {
                AddReward(
                    alignment *
                    movementFraction *
                    alignmentRewardScale
                );
            }
            else
            {
                AddReward(
                    alignment *
                    movementFraction *
                    wrongDirectionPenaltyScale
                );
            }
        }

        // -------------------------------------
        // 6. Obstacle proximity penalty
        // -------------------------------------
        float nearestObstacle = sensorDistance;

        for (int i = 0; i < RayCount; i++)
        {
            float angle = i * (360f / RayCount);

            Vector3 rayDirection =
                Quaternion.Euler(0f, angle, 0f) *
                nextForward;

            if (Physics.Raycast(
                nextPosition,
                rayDirection,
                out RaycastHit hit,
                sensorDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            ))
            {
                nearestObstacle = Mathf.Min(
                    nearestObstacle,
                    hit.distance
                );
            }
        }

        if (nearestObstacle < dangerDistance)
        {
            float danger = 1f -
                Mathf.Clamp01(
                    nearestObstacle /
                    Mathf.Max(0.01f, dangerDistance)
                );

            float movementFraction = Mathf.Clamp01(
                horizontalMovement.magnitude /
                Mathf.Max(0.0001f, forwardSpeed * dt)
            );

            AddReward(
                -danger *
                movementFraction *
                proximityPenaltyScale
            );
        }

        // -------------------------------------
        // 7. Goal
        // -------------------------------------
        if (currentDistance <= goalDistance)
        {
            AddReward(goalReward);

            FinishEpisode(
                "success",
                nextPosition
            );

            return;
        }

        // -------------------------------------
        // 8. Stagnation based on actual goal progress
        // -------------------------------------
        // Moving sideways or in circles no longer resets this counter.
        // Allow enough steps for detours around buildings.
        if (currentDistance <= progressAnchorDistance - minimumGoalProgress)
        {
            progressAnchorDistance = currentDistance;
            stepsWithoutProgress = 0;
        }
        else
        {
            stepsWithoutProgress++;
        }

        if (stepsWithoutProgress >= Mathf.Max(1, stagnationSteps))
        {
            AddReward(stagnationPenalty);
            FinishEpisode("stagnation", nextPosition);
            return;
        }

        // -------------------------------------
        // 9. Episode timeout
        // -------------------------------------
        if (actionCounter >= episodeMaxSteps)
        {
            FinishEpisode(
                "timeout",
                nextPosition
            );

            return;
        }

        if (enableDebug &&
            actionCounter %
            Mathf.Max(1, debugInterval) == 0)
        {
            Debug.Log(
                $"[DRONE] Step={StepCount} | " +
                $"Distance={currentDistance:F2} | " +
                $"Limit={maximumAllowedDistance:F2} | " +
                $"Progress={progress:F3} | " +
                $"Reward={GetCumulativeReward():F3}",
                this
            );
        }
    }

    private void FinishEpisode(
        string result,
        Vector3 finalPosition
    )
    {
        if (episodeFinished)
            return;

        episodeFinished = true;

        float finalDistance = Vector3.Distance(
            finalPosition,
            targetPoint.position
        );

        if (saveEpisodeCSV && logger != null)
        {
            logger.RecordEpisode(
                result,
                actionCounter,
                GetCumulativeReward(),
                episodeDistanceTraveled,
                initialDistance,
                finalDistance,
                episodeMinimumDistance,
                episodeMaximumDistance,
                maximumAllowedDistance
            );
        }

        if (enableDebug)
        {
            Debug.Log(
                $"[EPISODE END] {result} | " +
                $"FinalDistance={finalDistance:F2} | " +
                $"Reward={GetCumulativeReward():F3}",
                this
            );
        }

        EndEpisode();
    }

    public override void Heuristic(
        in ActionBuffers actionsOut
    )
    {
        var actions = actionsOut.ContinuousActions;

        if (actions.Length < ContinuousActionCount)
            return;

        actions[0] = 1f;
        actions[1] = 0f;
        actions[2] = 0f;
    }

    private void OnDrawGizmos()
    {
        if (targetPoint != null)
        {
            Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(
                targetPoint.position,
                goalDistance
            );

            Gizmos.color = Color.cyan;

            Gizmos.DrawLine(
                transform.position,
                targetPoint.position
            );
        }

        Gizmos.color = Color.yellow;

        for (int i = 0; i < RayCount; i++)
        {
            float angle = i * (360f / RayCount);

            Vector3 direction =
                Quaternion.Euler(0f, angle, 0f) *
                transform.forward;

            Gizmos.DrawRay(
                transform.position,
                direction * sensorDistance
            );
        }
    }
}
