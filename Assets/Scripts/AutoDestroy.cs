using UnityEngine;

public class AutoDestroy : MonoBehaviour
{
    public int frames = 2;

    private void LateUpdate()
    {
        frames -= 1;
        if (frames <= 0)
        {
            Destroy(gameObject);
        }
    }
}