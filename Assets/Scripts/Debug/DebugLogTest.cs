using UnityEngine;

public class DebugLogTest : MonoBehaviour
{
    [SerializeField] private KeyCode key = KeyCode.Space;

    void Update()
    {
        if (Input.GetKeyDown(key))
        {
            Debug.Log("========================== DEBUG LOG TEST ================================");
        }
    }
}
