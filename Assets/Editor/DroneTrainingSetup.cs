using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: Dron > Configurar escena para entrenamiento
// Prepara la escena abierta para entrenar DroneAgent con ML-Agents.
// Todos los cambios se registran en Undo (Ctrl+Z los revierte).
public static class DroneTrainingSetup
{
    private const string ObstacleLayerName = "Obstacles";
    private const string DroneName = "Agente";
    private const string StartName = "Target init";
    private const string TargetName = "Target End";
    private const float FlightHeight = 8f;

    [MenuItem("Dron/Configurar escena para entrenamiento")]
    public static void Configure()
    {
        var report = new List<string>();
        var warnings = new List<string>();

        int obstacleLayer = EnsureLayer(ObstacleLayerName, report);
        if (obstacleLayer < 0)
        {
            Debug.LogError("[SETUP] No hay capas libres para crear 'Obstacles'.");
            return;
        }

        Undo.SetCurrentGroupName("Configurar escena del dron");
        int undoGroup = Undo.GetCurrentGroup();

        ConfigureBuildings(obstacleLayer, report, warnings);

        Transform start = FindByName(StartName);
        Transform target = FindByName(TargetName);

        if (start == null || target == null)
        {
            Debug.LogError(
                $"[SETUP] No encontre '{StartName}' o '{TargetName}' en la escena.");
            return;
        }

        SetHeight(start, report);
        SetHeight(target, report);

        GameObject drone = FindDrone();
        if (drone == null)
        {
            Debug.LogError($"[SETUP] No encontre el dron '{DroneName}'.");
            return;
        }

        ConfigureDrone(drone, start, target, obstacleLayer, report);
        CheckLayout(start, target, obstacleLayer, warnings);

        Undo.CollapseUndoOperations(undoGroup);

        var scene = drone.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        report.Add($"Escena guardada: {scene.path}");

        Debug.Log("[SETUP] Listo.\n- " + string.Join("\n- ", report));
        foreach (string w in warnings)
            Debug.LogWarning("[SETUP] " + w);

        EditorUtility.DisplayDialog(
            "Configuracion del dron",
            "Escena configurada para entrenamiento.\n\n" +
            (warnings.Count > 0
                ? "Revisa las advertencias en la consola:\n- " + string.Join("\n- ", warnings)
                : "Sin advertencias.") +
            "\n\nDetalle completo en la consola ([SETUP]).",
            "OK");
    }

    private static int EnsureLayer(string name, List<string> report)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0)
        {
            report.Add($"Capa '{name}' ya existia (indice {existing}).");
            return existing;
        }

        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tagManager.ApplyModifiedProperties();
                report.Add($"Capa '{name}' creada (indice {i}).");
                return i;
            }
        }

        return -1;
    }

    private static void ConfigureBuildings(
        int obstacleLayer, List<string> report, List<string> warnings)
    {
        // Raiz de cada edificio: nombre "Building..." cuyo padre no es otro "Building...".
        var buildings = Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.name.StartsWith("Building") &&
                        (t.parent == null || !t.parent.name.StartsWith("Building")))
            .ToList();

        if (buildings.Count == 0)
        {
            warnings.Add("No encontre edificios (objetos cuyo nombre empieza con 'Building').");
            return;
        }

        int collidersAdded = 0;
        float lowestTop = float.MaxValue;

        foreach (Transform building in buildings)
        {
            foreach (Transform t in building.GetComponentsInChildren<Transform>(true))
            {
                Undo.RecordObject(t.gameObject, "Layer");
                t.gameObject.layer = obstacleLayer;

                var filter = t.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null &&
                    t.GetComponent<Collider>() == null)
                {
                    var collider = Undo.AddComponent<MeshCollider>(t.gameObject);
                    collider.sharedMesh = filter.sharedMesh;
                    collidersAdded++;
                }
            }

            var renderers = building.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                lowestTop = Mathf.Min(lowestTop, b.max.y);
            }
        }

        report.Add($"{buildings.Count} edificios en capa '{ObstacleLayerName}'; " +
                   $"{collidersAdded} MeshCollider agregados.");

        if (lowestTop < 10f)
        {
            warnings.Add(
                $"El edificio mas bajo mide {lowestTop:F1} de alto. El dron vuela entre 6 y 10; " +
                "los edificios mas bajos que 10 se pueden sobrevolar sin esquivarlos.");
        }
    }

    private static void SetHeight(Transform t, List<string> report)
    {
        Undo.RecordObject(t, "Height");
        Vector3 p = t.position;
        p.y = FlightHeight;
        t.position = p;
        report.Add($"'{t.name}' en {t.position}.");
    }

    private static GameObject FindDrone()
    {
        var agent = Object.FindAnyObjectByType<DroneAgent>(FindObjectsInactive.Include);
        if (agent != null) return agent.gameObject;

        Transform byName = FindByName(DroneName);
        if (byName != null) return byName.gameObject;

        var mission = Object.FindAnyObjectByType<DroneMissionController>(FindObjectsInactive.Include);
        return mission != null ? mission.gameObject : null;
    }

    private static void ConfigureDrone(
        GameObject drone, Transform start, Transform target,
        int obstacleLayer, List<string> report)
    {
        // Estos scripts mueven el Rigidbody por su cuenta y pelean con el agente.
        // DroneController ademas pone isKinematic=false en Awake aunque este desactivado.
        foreach (var c in drone.GetComponents<DroneMissionController>())
        {
            Undo.DestroyObjectImmediate(c);
            report.Add("DroneMissionController eliminado del dron.");
        }
        foreach (var c in drone.GetComponents<DroneController>())
        {
            Undo.DestroyObjectImmediate(c);
            report.Add("DroneController eliminado del dron.");
        }

        var rb = drone.GetComponent<Rigidbody>() ?? Undo.AddComponent<Rigidbody>(drone);
        Undo.RecordObject(rb, "Rigidbody");
        rb.useGravity = false;
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.None;

        var agent = drone.GetComponent<DroneAgent>() ?? Undo.AddComponent<DroneAgent>(drone);
        Undo.RecordObject(agent, "DroneAgent");
        agent.startPoint = start;
        agent.targetPoint = target;
        agent.obstacleLayers = 1 << obstacleLayer;
        agent.saveEpisodeCSV = false;
        agent.MaxStep = 0;
        EditorUtility.SetDirty(agent);

        var behavior = drone.GetComponent<BehaviorParameters>();
        Undo.RecordObject(behavior, "BehaviorParameters");
        behavior.BehaviorName = "DroneNavigation";
        behavior.BrainParameters.VectorObservationSize = 23;
        behavior.BrainParameters.NumStackedVectorObservations = 1;
        behavior.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(3);
        behavior.BehaviorType = BehaviorType.Default;
        behavior.Model = null;
        EditorUtility.SetDirty(behavior);

        var requester = drone.GetComponent<DecisionRequester>() ??
                        Undo.AddComponent<DecisionRequester>(drone);
        Undo.RecordObject(requester, "DecisionRequester");
        requester.DecisionPeriod = 1;
        requester.TakeActionsBetweenDecisions = true;
        EditorUtility.SetDirty(requester);

        Undo.RecordObject(drone.transform, "Drone position");
        drone.transform.position = start.position;

        report.Add("Dron configurado: DroneAgent + BehaviorParameters " +
                   "(DroneNavigation, 23 obs, 3 continuas, Default) + DecisionRequester (1).");
    }

    private static void CheckLayout(
        Transform start, Transform target, int obstacleLayer, List<string> warnings)
    {
        float distance = Vector3.Distance(start.position, target.position);
        if (distance < 10f)
        {
            warnings.Add(
                $"Inicio y meta estan a solo {distance:F1} unidades. " +
                "Mueve 'Target End' mas lejos, con edificios en medio.");
        }
        if (distance > 100f)
        {
            warnings.Add(
                $"Inicio y meta estan a {distance:F1} unidades; la observacion de distancia " +
                "se satura en 100.");
        }

        int mask = 1 << obstacleLayer;
        if (Physics.CheckSphere(start.position, 0.5f, mask, QueryTriggerInteraction.Ignore))
            warnings.Add("El inicio esta dentro de un edificio: el dron chocara al aparecer.");
        if (Physics.CheckSphere(target.position, 2.5f, mask, QueryTriggerInteraction.Ignore))
            warnings.Add("La meta esta dentro o pegada a un edificio.");
    }

    private static Transform FindByName(string name)
    {
        return Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == name);
    }
}
