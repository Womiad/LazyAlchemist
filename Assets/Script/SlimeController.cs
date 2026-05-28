using UnityEngine;

public class SlimeController : MonoBehaviour
{
    [Header("JellyMesh")]
    public JellyMesh jelly;
    public float pressIntensity = 0.5f; // 每幀累積位移速度
    public float reboundSpeed = 5f;     // 放開後彈回速度
    public float maxFlatten = 0.5f;     // 最大壓扁距離

    private bool isPressing = false;

    void FixedUpdate()
    {
        // 長按下鍵 → 累積向下位移
        if(Input.GetKey(KeyCode.DownArrow))
        {
            isPressing = true;
            for(int i = 0; i < jelly.jv.Length; i++)
            {
                // 累積偏移
                jelly.jv[i].externalOffset += Vector3.down * pressIntensity * Time.fixedDeltaTime;

                // 限制壓扁上限
                if(jelly.jv[i].externalOffset.y < -maxFlatten)
                    jelly.jv[i].externalOffset = new Vector3(0f, -maxFlatten, 0f);
            }
        }
        else
        {
            if(isPressing)
                isPressing = false;

            // 放開鍵 → Lerp 回零
            for(int i = 0; i < jelly.jv.Length; i++)
            {
                jelly.jv[i].externalOffset = Vector3.Lerp(jelly.jv[i].externalOffset, Vector3.zero, reboundSpeed * Time.fixedDeltaTime);
            }
        }
    }
}
