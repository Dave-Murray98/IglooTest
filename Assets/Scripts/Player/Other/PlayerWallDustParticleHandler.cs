using UnityEngine;

public class PlayerWallDustParticleHandler : MonoBehaviour
{
    public ParticleSystem wallDustParticleSystem;

    private void Start()
    {
        wallDustParticleSystem.Stop();
    }

    private void OnTriggerEnter(Collider other)
    {
        wallDustParticleSystem.Play();
    }

    private void OnTriggerExit(Collider other)
    {
        wallDustParticleSystem.Stop();
    }
}
