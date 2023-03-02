using UnityEngine;

public class FrameStart : MonoBehaviour
{
    private void Awake()
    {
        Util.Print(this, "FrameStart.Awake");
    }

    void Start()
    {
        Util.Print(this, "FrameStart.Start");
    }

    void FixedUpdate()
    {
        Util.Print(this, "FrameStart.FixedUpdate");
    }

    void Update()
    {
        Util.Print(this, "FrameStart.Update");
    }

    void LateUpdate()
    {
        Util.Print(this, "FrameStart.LateUpdate");
    }
}