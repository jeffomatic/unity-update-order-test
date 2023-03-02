using UnityEngine;

public class LogUpdate : MonoBehaviour
{
    private void Awake()
    {
        Util.Print(this, "Awake");
    }

    private void OnEnable()
    {
        Util.Print(this, "OnEnable");
    }

    private void Start()
    {
        Util.Print(this, "Start");
        Util.CloneInStart(gameObject);
    }

    private void Update()
    {
        Util.Print(this, "Update");
    }
}
