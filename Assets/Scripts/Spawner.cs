using UnityEngine;

public class Spawner : MonoBehaviour
{
    public GameObject[] toSpawn;

    private void Awake()
    {
        DoSpawn("Awake");
    }

    private void Start()
    {
        DoSpawn("Start");
    }

    private void FixedUpdate()
    {
        DoSpawn("FixedUpdate");
    }

    private void Update()
    {
        DoSpawn("Update");
    }

    private void LateUpdate()
    {
        DoSpawn("LateUpdate");
    }

    private void DoSpawn(string phase)
    {
        foreach (var prefab in toSpawn)
        {
            var compType = "";
            if (prefab.TryGetComponent<LogFixedUpdate>(out _))
            {
                compType = "LogFixedUpdate";
            }
            else if (prefab.TryGetComponent<LogUpdate>(out _))
            {
                compType = "LogUpdate";
            }
            else if (prefab.TryGetComponent<LogLateUpdate>(out _))
            {
                compType = "LogLateUpdate";
            }

            var objName = $"{compType}-SpawnedDuring{phase}";
            Util.Print(this, $"Spawning {objName}");
            prefab.name = objName;
            Instantiate(prefab);
        }
    }
}