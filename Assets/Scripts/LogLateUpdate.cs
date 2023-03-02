using UnityEngine;

public class LogLateUpdate : MonoBehaviour
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

    private void LateUpdate()
    {
        Util.Print(this, "LateUpdate");
    }
}