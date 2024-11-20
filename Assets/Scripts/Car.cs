using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Cinemachine;
using TMPro;

public class Car : NetworkBehaviour
{
    [SerializeField]
    private float tocDoXe = 1500f;  // Tốc độ mặc định
    private float tocDoXeHost = 1500f; // Tốc độ Host, điều chỉnh sau
    [SerializeField]
    private float lucReXe = 225f;
    [SerializeField]
    private float lucPhanh = 90f;

    [SerializeField] float boostSpeed = 1800f;  // Tăng tốc
    [SerializeField] float reduceSpeed = 450f;  // Giảm tốc
    [SerializeField] float normalSpeed = 300f;  // Tốc độ bình thường

    private float timer_Speed_Increase = 0f;
    private float timer_Speed_Decrease = 0f;
    private bool isPauseSI = true;
    private bool isPauseSD = true;
    private Rigidbody rb;

    [SerializeField] private TextMeshProUGUI playerNameUI;

    [SerializeField] CinemachineVirtualCamera vc;
    [SerializeField] private AudioListener listener;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    public void SetPlayerName(string name)
    {
        playerNameUI.text = name;
    }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            // Tăng tốc độ thêm cho Host
            if (IsServer)
            {
                tocDoXeHost = tocDoXe * 1.5f * 2f;  // Tăng tốc độ cho Host thêm 2 lần nữa (gấp 3 lần tốc độ ban đầu)
            }

            // Kích hoạt camera của xe này
            CinemachineVirtualCamera cam = GetComponentInChildren<CinemachineVirtualCamera>();
            if (cam != null)
            {
                cam.Priority = 1; // Camera ưu tiên cao nhất
                cam.Follow = transform;
                // Cài đặt offset cho camera
                cam.GetCinemachineComponent<CinemachineTransposer>().m_FollowOffset = new Vector3(0, 5, -10);
                cam.LookAt = transform;

                Debug.Log("Camera đã được gán cho xe của người chơi.");
            }
            else
            {
                Debug.LogError("Không tìm thấy CinemachineVirtualCamera trong Prefab của xe!");
            }
        }
        else
        {
            // Vô hiệu hóa camera của xe khác
            CinemachineVirtualCamera cam = GetComponentInChildren<CinemachineVirtualCamera>();
            if (cam != null)
            {
                cam.Priority = 0; // Camera ưu tiên thấp
            }
        }
    }

    private void FixedUpdate()
    {
        if (IsOwner)
        {
            float moveInput = Input.GetAxis("Vertical");
            float turnInput = Input.GetAxis("Horizontal");
            HandleSpeedEffects();

            // Gửi điều khiển đầu vào lên server
            SendMovementInputServerRpc(moveInput, turnInput);

            if (moveInput > 0 && Input.GetKey(KeyCode.LeftShift))
            {
                PhanhXeServerRpc();  // Gọi phanh từ server
            }
        }
    }

    private bool time_Manager(bool Pausing)
    {
        Pausing = !Pausing;
        return Pausing;
    }

    private void HandleSpeedEffects()
    {
        // Xử lý tăng tốc độ
        if (!isPauseSI)
        {
            timer_Speed_Increase -= Time.deltaTime;
            if (timer_Speed_Increase <= 0f)
            {
                isPauseSI = time_Manager(isPauseSI);
                tocDoXe = Mathf.Lerp(tocDoXe, normalSpeed, 0.015f); // Giảm thời gian Lerp để tốc độ tăng nhanh hơn
            }
        }

        // Xử lý giảm tốc độ
        if (!isPauseSD)
        {
            timer_Speed_Decrease -= Time.deltaTime;
            if (timer_Speed_Decrease <= 0f)
            {
                isPauseSD = time_Manager(isPauseSD);
                tocDoXe = Mathf.Lerp(tocDoXe, normalSpeed, 0.015f); // Giảm thời gian Lerp để tốc độ giảm nhanh hơn
            }
        }
    }

    [ServerRpc]
    private void SendMovementInputServerRpc(float moveInput, float turnInput)
    {
        // Xử lý tốc độ cho Host và Client
        float currentSpeed = IsServer ? tocDoXeHost : tocDoXe;

        // Tính toán và gửi vị trí mượt mà
        DiChuyenXe(moveInput, currentSpeed);
        ReXe(turnInput);

        // Gửi vị trí và tốc độ đến các client
        UpdatePositionClientRpc(transform.position, transform.rotation, rb.velocity);
    }

    [ClientRpc]
    private void UpdatePositionClientRpc(Vector3 position, Quaternion rotation, Vector3 velocity)
    {
        if (!IsOwner)
        {
            transform.position = Vector3.Lerp(transform.position, position, 0.2f);  // Tăng hệ số Lerp để mượt mà hơn
            transform.rotation = Quaternion.Lerp(transform.rotation, rotation, 0.2f);
            rb.velocity = velocity;
        }
    }

    [ServerRpc]
    private void PhanhXeServerRpc()
    {
        if (rb.velocity.z != 0)
        {
            rb.AddRelativeForce(-Vector3.forward * lucPhanh);
            UpdateBrakeEffectClientRpc();
        }
    }

    [ClientRpc]
    private void UpdateBrakeEffectClientRpc()
    {
        if (!IsOwner)
        {
            // Thêm hiệu ứng phanh nếu cần
        }
    }

    public void DiChuyenXe(float diChuyen, float currentSpeed)
    {
        // Xử lý di chuyển mượt mà và tăng tốc
        Vector3 forwardMovement = transform.forward * diChuyen * currentSpeed * Time.deltaTime;
        rb.AddForce(forwardMovement, ForceMode.Acceleration);  // Sử dụng AddForce với mức Acceleration
    }

    public void ReXe(float re)
    {
        // Xử lý quay xe mượt mà
        Quaternion xoay = Quaternion.Euler(Vector3.up * re * lucReXe * Time.deltaTime);
        rb.MoveRotation(rb.rotation * xoay);
    }

    private void LateUpdate()
    {
        if (playerNameUI != null)
        {
            if (Camera.main != null)
            {
                playerNameUI.transform.LookAt(Camera.main.transform);
                playerNameUI.transform.Rotate(0, 180, 0);
            }
            else
            {
                Debug.LogWarning("Camera.main is null.");
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Speed_Increase"))
        {
            StartCoroutine(SpeedBoost(boostSpeed, 6f));  // Tăng tốc mạnh hơn
            Destroy(other.gameObject);
        }
        else if (other.CompareTag("Speed_Descrease"))
        {
            StartCoroutine(SpeedBoost(reduceSpeed, 6f));  // Giảm tốc mạnh hơn
            Destroy(other.gameObject);
        }
    }

    private IEnumerator SpeedBoost(float newSpeed, float duration)
    {
        float originalSpeed = tocDoXe;
        tocDoXe = newSpeed;
        yield return new WaitForSeconds(duration);
        tocDoXe = Mathf.Lerp(tocDoXe, normalSpeed, 0.015f);  // Lerp nhanh để phục hồi tốc độ
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Obstacles"))
        {
            tocDoXe = normalSpeed;  // Phục hồi tốc độ bình thường
            Destroy(collision.gameObject);
        }
        else if (collision.gameObject.CompareTag("Barriers") || collision.gameObject.CompareTag("Player"))
        {
            tocDoXe = Mathf.Lerp(tocDoXe, tocDoXe * 0.5f, 0.1f);  // Giảm tốc độ từ từ hơn khi va chạm
        }
    }
}
