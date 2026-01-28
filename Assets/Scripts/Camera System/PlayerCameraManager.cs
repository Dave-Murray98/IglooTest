using System.Collections.Generic;
using Igloo.Common;
using UnityEngine;

public class PlayerCameraManager : MonoBehaviour
{
    public MeshFilter waterPortalMeshFilter;

    public static PlayerCameraManager Instance { get; private set; }

    public List<PlayerCamera> playerCameras;

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

    // private void Start()
    // {
    //     SetUpPlayerCameras();
    // }

    // private void SetUpPlayerCameras()
    // {
    //     foreach (PlayerCamera cam in GetComponentsInChildren<PlayerCamera>())
    //     {
    //         playerCameras.Add(cam);
    //         cam.Initialize(waterPortalMeshFilter);
    //     }
    // }

}
