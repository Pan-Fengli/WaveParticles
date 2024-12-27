using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro ;
using UnityEngine.UI;

public class FrameRateCounter : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI display = default;
    private Text t_Text;
    public GameObject TimeText;
    [SerializeField,Range(0.1f,2f)]
    float sampleDuration = 1f;
    private float timer = 0f;
    private float time = 0f;

    int frames;
    float duration;
    float bestDuration = float.MaxValue;
    float worstDuration;
    // Start is called before the first frame update
    void Start()
    {
        t_Text = TimeText.GetComponent<Text>();
    }

    // Update is called once per frame
    void Update()
    {
        //updateTime();
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
    private void updateTime()
    {
        timer += Time.deltaTime;
        if (timer >= 0.1)// 定时0.1秒 10hz
        {

            t_Text.text = time.ToString();
        }
    }
}
