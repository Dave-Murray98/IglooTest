using System.Collections;
using UnityEngine;

public class PlayerJackHammerTool : PlayerTool
{
    [SerializeField] private GameObject damager;

    [SerializeField] private float makeNoiseInterval = 2f;
    private float noiseTimer;


    private void Awake()
    {
        damager.SetActive(false);
    }

    public override void TriggerToolEffect()
    {
        damager.SetActive(true);
        StartCoroutine(MakeNoiseWhileActive());
    }

    public override void StopToolEffect()
    {
        damager.SetActive(false);
        StopAllCoroutines();
    }

    protected override void DebugLog(string message)
    {
        if (enableDebugLogs)
            Debug.Log($"[PlayerBlowTorchTool] {message}");
    }

    protected virtual IEnumerator MakeNoiseWhileActive()
    {
        while (true)
        {
            noiseTimer += Time.deltaTime;
            if (noiseTimer >= makeNoiseInterval)
            {
                noiseTimer = 0f;
                NoisePool.CreateNoise(transform.position, activeEffectNoiseVolume);
            }
            yield return null;
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }
}
