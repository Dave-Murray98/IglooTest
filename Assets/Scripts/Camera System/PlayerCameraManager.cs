using System.Collections.Generic;
using UnityEngine;

public class PlayerCameraManager : MonoBehaviour
{
    [Header("References")]
    public MeshFilter waterPortalMeshFilter;

    [Header("Cameras")]
    public List<PlayerCamera> playerCameras;

    public static PlayerCameraManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (waterPortalMeshFilter == null)
        {
            // assign it by finding the object with a tag
            waterPortalMeshFilter = GameObject.FindGameObjectWithTag("CrestWaterPortal").GetComponent<MeshFilter>();
        }

    }

}
