
using UnityEngine;

public class FrameEnd : MonoBehaviour
{
    private void Awake()
    {
        Util.Print(this, "FrameEnd.Awake");
    }


    void Start()
    {
        Util.Print(this, "FrameEnd.Start");
    }

    void FixedUpdate()
    {
        Util.Print(this, "FrameEnd.FixedUpdate");
    }

    void Update()
    {
        Util.Print(this, "FrameEnd.Update");
    }

    void LateUpdate()
    {
        Util.Print(this, "FrameEnd.LateUpdate");
    }
}
