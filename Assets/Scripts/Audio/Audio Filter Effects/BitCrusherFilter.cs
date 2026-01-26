using UnityEngine;

public class BitCrusherFilter : MonoBehaviour
{

    [SerializeField, Range(1, 16)] private int bitDepth = 8;

    [SerializeField, Range(1, 32)] private int sampleRateReduction = 1;

    private void OnAudioFilterRead(float[] data, int channels)
    {
        // quatization step based on the bit depth
        float step = 1f / (1 << bitDepth);


        // variable to hold the last sample
        float lastSample = 0f;

        //cache array length
        int dataLength = data.Length;

        for (int i = 0; i < dataLength; i++)
        {
            // sample rate reduction (holding the previous version instead of skipping)
            if (i % sampleRateReduction == 0)
            {
                // cache the current sample
                lastSample = data[i];
            }
            else
            {
                // assign the cached sample
                data[i] = lastSample;
            }

            // quantization
            data[i] = Mathf.Round(data[i] / step) * step;
        }
    }
}
