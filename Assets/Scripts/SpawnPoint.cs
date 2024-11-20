using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    [SerializeField] private GameObject carPrefab; // Prefab của xe
    [SerializeField] private List<Transform> spawnPoints; // Danh sách các điểm spawn

    private List<ulong> spawnedPlayers = new List<ulong>(); // Lưu danh sách ID người chơi đã spawn

    private void Start()
    {
        //if (NetworkManager.Singleton.IsServer)
        //{
        //    NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        //    if(GameData.Instance!=null)
        //    {
        //        Debug.Log("Player list: " + string.Join(", ", GameData.Instance.playerNames));
        //    }
        //    else
        //    {
        //        Debug.LogWarning("GameData has not been initialized!");
        //    }
        //}
        if (GameData.Instance.isHost)
        {
            Debug.Log("Người chơi này là Host!");
            NetworkManager.Singleton.StartHost(); // Bắt đầu với vai trò Host
        }
        else
        {
            Debug.Log("Người chơi này là Client!");
            NetworkManager.Singleton.StartClient(); // Bắt đầu với vai trò Client
        }

        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        StartCoroutine(HandleClientConnected(clientId));
    }

    private IEnumerator HandleClientConnected(ulong clientId)
    {
        // Chờ một frame để đảm bảo client đã kết nối hoàn toàn
        yield return null;

        if (spawnPoints == null || spawnPoints.Count == 0) // Dùng .Count thay vì .Length
        {
            Debug.LogWarning("Không có điểm spawn được định nghĩa.");
            yield break;
        }

        if (GameData.Instance.playerNames == null || GameData.Instance.playerNames.Count == 0)
        {
            Debug.LogWarning("Danh sách tên người chơi không có dữ liệu.");
            yield break;
        }

        if (spawnedPlayers.Count < GameData.Instance.playerNames.Count)
        {
            Transform spawnPoint = spawnPoints[spawnedPlayers.Count];
            GameObject car = Instantiate(carPrefab, spawnPoint.position, spawnPoint.rotation);

            car.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

            spawnedPlayers.Add(clientId);

            // Cập nhật tên người chơi
            string playerName = GameData.Instance.playerNames[spawnedPlayers.Count - 1];
            car.GetComponent<Car>().SetPlayerName(playerName);

            Debug.Log($"Client {clientId} đã được spawn xe.");
        }
        else
        {
            Debug.LogWarning("Không đủ điểm spawn cho người chơi mới.");
        }
    }


    private void OnClientDisconnected(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        if (spawnedPlayers.Contains(clientId))
        {
            spawnedPlayers.Remove(clientId);
            Debug.Log($"Client {clientId} đã rời phòng.");
        }
    }
}
