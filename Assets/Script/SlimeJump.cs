using UnityEngine;

public class SlimeJumpCharge : MonoBehaviour
{
    public Rigidbody rb;           // 史萊姆 Rigidbody
    public float maxJumpForce = 10f; // 最大跳躍力
    public float chargeRate = 2f;    // 累積速度，每秒累加數值

    private float currentCharge = 0f;  // 當前累積值

    void Start()
    {
        if(rb == null)
            rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 長按往下鍵累積數值
        if(Input.GetKey(KeyCode.DownArrow))
        {
            currentCharge += chargeRate * Time.deltaTime;
            currentCharge = Mathf.Min(currentCharge, maxJumpForce); // 限制最大值
        }

        // 松開鍵時跳躍
        if(Input.GetKeyUp(KeyCode.DownArrow))
        {
            Jump(currentCharge);
            currentCharge = 0f; // 重置累積值
        }
    }

    void Jump(float jumpPower)
    {
        // rb.AddForce(Vector3.up * jumpPower, ForceMode.Impulse);
        // Debug.Log("Jump! Force: " + jumpPower);
    }
}
