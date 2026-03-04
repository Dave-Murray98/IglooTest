using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RumbleManager : MonoBehaviour
{
    public static RumbleManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // Otherwise, we have a duplicate (probably an error)
            Debug.LogWarning("Multiple RumbleManagers detected! Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;

    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StopAllRumbling();
    }

    private void OnSceneUnloaded(Scene scene)
    {
        StopAllRumbling();
    }

    private void OnApplicationQuit()
    {
        StopAllRumbling();
    }

    private void OnApplicationPause(bool pause)
    {
        StopAllRumbling();
    }

    public void RumblePulse(Gamepad assignedGamepad, float lowFrequency, float highFrequency, float duration)
    {
        if (assignedGamepad != null)
        {
            //start rumble 
            assignedGamepad.SetMotorSpeeds(lowFrequency, highFrequency);

            //stop rumble after duration
            StartCoroutine(StopRumbleAfterDuration(assignedGamepad, duration));
        }
    }

    private IEnumerator StopRumbleAfterDuration(Gamepad assignedGamepad, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (assignedGamepad != null)
        {
            assignedGamepad.SetMotorSpeeds(0f, 0f);
        }
    }

    private void StopAllRumbling()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            gamepad.SetMotorSpeeds(0f, 0f);
        }
    }
}