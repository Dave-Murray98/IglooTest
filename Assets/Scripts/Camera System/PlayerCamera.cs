using Crest;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [SerializeField]
    private UnderwaterRenderer crestUnderwaterRenderer;


    private void Awake()
    {
        if (crestUnderwaterRenderer == null)
        {
            crestUnderwaterRenderer = GetComponent<UnderwaterRenderer>();
        }

        if (PlayerCameraManager.Instance != null)
        {
            PlayerCameraManager.Instance.playerCameras.Add(this);
            Initialize(PlayerCameraManager.Instance.waterPortalMeshFilter);
        }
        else
        {
            Debug.LogWarning("No PlayerCameraManager found in scene!");
        }
    }

    public void Initialize(MeshFilter waterPortalMesh)
    {
        if (crestUnderwaterRenderer != null)
        {
            crestUnderwaterRenderer._volumeGeometry = waterPortalMesh;
            crestUnderwaterRenderer._mode = UnderwaterRenderer.Mode.Portal;
        }
    }

}
