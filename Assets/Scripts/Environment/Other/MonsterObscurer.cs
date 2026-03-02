using UnityEngine;

public class MonsterObscurer : MonoBehaviour
{
    [SerializeField] private Transform player;

    [SerializeField] private Material fogMaterial;

    private void Start()
    {
        if (fogMaterial == null)
            fogMaterial = GetComponent<Renderer>().material;


    }

    private void Update()
    {
        fogMaterial.SetFloat("_PlayerYPosition", player.transform.position.y);
    }
}