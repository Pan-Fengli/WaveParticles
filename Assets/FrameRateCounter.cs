using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro ;

public class FrameRateCounter : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI display = default;

    [SerializeField,Range(0.1f,2f)]
    float sampleDuration = 1f;

    int frames;
    float duration;
    float bestDuration = float.MaxValue;
    float worstDuration;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float frameDuration = Time.unscaledDeltaTime;
        frames += 1;
        duration += frameDuration;
        if (frameDuration < bestDuration)
        {
            bestDuration = frameDuration;
        }
        if (frameDuration > worstDuration)
        {
            worstDuration = frameDuration;
        }

        if (duration >= sampleDuration)//每隔这么多秒统计一次
        {
            display.SetText("FPS:{0:0},{1:0},{2:0}", 
                frames / duration,
                1f/bestDuration,
                1f/worstDuration);
            frames = 0;
            duration = 0;
            bestDuration = float.MaxValue;
            worstDuration = 0f;
        }
    }
}
