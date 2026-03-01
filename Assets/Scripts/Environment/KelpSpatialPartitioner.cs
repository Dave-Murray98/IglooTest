using System;
using System.Collections;
using System.Collections.Generic;
using Kellojo.StylizedKelp;
using UnityEngine;

public class KelpSpatialPartitioner : MonoBehaviour
{

    [SerializeField] private Transform player;

    private List<Kelp> kelpRenderers = new List<Kelp>();

    [SerializeField] private float activationCheckFrequency = 0.5f;

    [SerializeField] private StylizedKelpRenderer stylizedKelpRenderer;

    // [SerializeField] private float updateFrequency = 0.53f;
    // private float updateTimer = 0f;

    private void Awake()
    {
        kelpRenderers.AddRange(FindObjectsByType<Kelp>(FindObjectsSortMode.None));

        if (player == null)
        {
            player = Camera.main.transform;
        }

        if (stylizedKelpRenderer == null)
        {
            stylizedKelpRenderer = FindFirstObjectByType<StylizedKelpRenderer>();
        }
    }

    private void OnEnable()
    {
        StartCoroutine(RunPartitioner());
    }

    // private void Update()
    // {

    //     updateTimer += Time.deltaTime;
    //     if (updateTimer > updateFrequency)
    //     {
    //         if (stylizedKelpRenderer != null)
    //         {
    //             stylizedKelpRenderer.Simulate();
    //         }

    //         updateTimer = 0f;
    //     }
    // }


    IEnumerator RunPartitioner()
    {
        while (enabled)
        {
            yield return new WaitForSeconds(activationCheckFrequency);

            foreach (var kelpRenderer in kelpRenderers)
            {
                var distance = Vector3.Distance(kelpRenderer.transform.position, player.position);
                kelpRenderer.gameObject.SetActive(distance < stylizedKelpRenderer.simulationDistance);
            }
        }
    }

}