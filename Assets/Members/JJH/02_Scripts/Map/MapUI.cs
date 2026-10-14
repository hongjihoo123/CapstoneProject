using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace Members.JJH._02_Scripts.Map
{
    public class MapUI : MonoBehaviour
    {
        public enum RoomType
        {
            Start,
            Normal,
            Elite,
            Event,
            Shop,
            Boss
        }

        [Serializable]
        public struct RoomNode
        {
            public int id;
            public RoomType type;
            public Vector2 position;
            public List<int> connections;
            public MapRoomUI ui;
        }

        [Header("Map")]
        [SerializeField] private int seed = 0;
        [SerializeField] private int roomCount = 25;
        [SerializeField] private int extraConnectionCount = 12;

        [Header("UI")]
        [SerializeField] private RectTransform mapContent;
        [SerializeField] private MapRoomUI roomPrefab;
        [SerializeField] private Image connectionPrefab;

        [Header("Layout")]
        [SerializeField] private float mapPadding = 100f;
        [SerializeField] private float minRoomDistance = 150f;
        [SerializeField] private float connectionWidth = 5f;

        private readonly List<RoomNode> rooms = new List<RoomNode>();
        private readonly List<Image> connections = new List<Image>();
        private readonly HashSet<int> visitedRoomIds = new HashSet<int>();
        private readonly HashSet<int> revealedRoomIds = new HashSet<int>();

        private int currentRoomId = -1;
        private int startRoomId = -1;
        private int bossRoomId = -1;

        private void Start()
        {
            GenerateMap();
        }

        [ContextMenu("Generate Map")]
        public void GenerateMap()
        {
            if (mapContent == null || roomPrefab == null || connectionPrefab == null)
            {
                Debug.LogError("MapUI의 UI 참조를 모두 설정해 주세요.", this);
                return;
            }

            if (roomCount < 2)
            {
                Debug.LogWarning("방 개수는 최소 2개 이상이어야 합니다.");
                return;
            }

            ClearMap();

            Random.InitState(seed == 0 ? Environment.TickCount : seed);

            CreateRooms();
            ConnectRooms();
            CreateConnectionUI();
            CreateRoomUI();

            currentRoomId = startRoomId;
            visitedRoomIds.Add(startRoomId);
            revealedRoomIds.Add(startRoomId);
            revealedRoomIds.Add(bossRoomId);
            RevealConnectedRooms(startRoomId);
            UpdateRoomState();

            UpdateRoomState();
        }

        private void CreateRooms()
        {
            Rect rect = mapContent.rect;

            float minX = rect.xMin + mapPadding;
            float maxX = rect.xMax - mapPadding;
            float minY = rect.yMin + mapPadding;
            float maxY = rect.yMax - mapPadding;

            if (minX >= maxX || minY >= maxY)
            {
                Debug.LogError("MapContent 크기가 너무 작습니다. 크기와 Map Padding을 확인해 주세요.", this);
                return;
            }

            startRoomId = CreateRoom(new Vector2(minX, Random.Range(minY, maxY)), RoomType.Start);
            bossRoomId = CreateRoom(new Vector2(maxX, Random.Range(minY, maxY)), RoomType.Boss);

            int attempts = 0;
            int maxAttempts = roomCount * 100;

            while (rooms.Count < roomCount && attempts < maxAttempts)
            {
                attempts++;

                Vector2 position = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));

                if (!IsPositionAvailable(position))
                    continue;

                CreateRoom(position, GetRandomRoomType());
            }

            if (rooms.Count < roomCount)
            {
                Debug.LogWarning($"공간이 부족하여 {rooms.Count}개의 방만 생성했습니다. MapContent 크기를 늘리거나 Min Room Distance를 줄여 주세요.", this);
            }
        }

        private int CreateRoom(Vector2 position, RoomType type)
        {
            RoomNode room = new RoomNode
            {
                id = rooms.Count,
                type = type,
                position = position,
                connections = new List<int>(),
                ui = null
            };

            rooms.Add(room);
            return room.id;
        }

        private bool IsPositionAvailable(Vector2 position)
        {
            foreach (RoomNode room in rooms)
            {
                if (Vector2.Distance(room.position, position) < minRoomDistance)
                    return false;
            }

            return true;
        }

        private RoomType GetRandomRoomType()
        {
            int value = Random.Range(0, 100);

            if (value < 60)
                return RoomType.Normal;

            if (value < 75)
                return RoomType.Elite;

            if (value < 88)
                return RoomType.Event;

            return RoomType.Shop;
        }

        private void ConnectRooms()
        {
            List<int> connectedRooms = new List<int> { startRoomId };
            HashSet<int> remainingRooms = new HashSet<int>();

            foreach (RoomNode room in rooms)
            {
                if (room.id != startRoomId)
                    remainingRooms.Add(room.id);
            }

            while (remainingRooms.Count > 0)
            {
                int nearestFrom = -1;
                int nearestTo = -1;
                float nearestDistance = float.MaxValue;

                foreach (int fromId in connectedRooms)
                {
                    RoomNode from = rooms[fromId];

                    foreach (int toId in remainingRooms)
                    {
                        RoomNode to = rooms[toId];
                        float distance = Vector2.Distance(from.position, to.position);

                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestFrom = fromId;
                            nearestTo = toId;
                        }
                    }
                }

                if (nearestFrom == -1 || nearestTo == -1)
                    break;

                AddConnection(nearestFrom, nearestTo);

                connectedRooms.Add(nearestTo);
                remainingRooms.Remove(nearestTo);
            }

            List<(int from, int to)> candidates = new List<(int from, int to)>();

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    if (rooms[i].connections.Contains(j))
                        continue;

                    float distance = Vector2.Distance(rooms[i].position, rooms[j].position);

                    if (distance > minRoomDistance * 3.5f)
                        continue;

                    candidates.Add((i, j));
                }
            }

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (candidates[i], candidates[randomIndex]) = (candidates[randomIndex], candidates[i]);
            }

            int connectionCount = Mathf.Min(extraConnectionCount, candidates.Count);

            for (int i = 0; i < connectionCount; i++)
            {
                AddConnection(candidates[i].from, candidates[i].to);
            }
        }

        private void AddConnection(int fromId, int toId)
        {
            if (fromId == toId)
                return;

            RoomNode from = rooms[fromId];
            RoomNode to = rooms[toId];

            if (from.connections.Contains(toId))
                return;

            from.connections.Add(toId);
            to.connections.Add(fromId);

            rooms[fromId] = from;
            rooms[toId] = to;
        }

        private void CreateConnectionUI()
        {
            foreach (RoomNode room in rooms)
            {
                foreach (int connectedRoomId in room.connections)
                {
                    if (room.id >= connectedRoomId)
                        continue;

                    RoomNode connectedRoom = rooms[connectedRoomId];
                    CreateConnection(room.position, connectedRoom.position);
                }
            }
        }

        private void CreateConnection(Vector2 start, Vector2 end)
        {
            Image connection = Instantiate(connectionPrefab, mapContent);
            RectTransform rect = connection.rectTransform;
            Vector2 direction = end - start;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = start + direction * 0.5f;
            rect.sizeDelta = new Vector2(direction.magnitude, connectionWidth);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);

            connections.Add(connection);
        }

        private void CreateRoomUI()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomNode room = rooms[i];
                MapRoomUI roomUI = Instantiate(roomPrefab, mapContent);

                roomUI.Initialize(room.id, this);

                RectTransform rect = roomUI.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = room.position;

                room.ui = roomUI;
                rooms[i] = room;
            }
        }

        private void RevealConnectedRooms(int roomId)
        {
            if (roomId < 0 || roomId >= rooms.Count)
                return;

            foreach (int connectedRoomId in rooms[roomId].connections)
            {
                revealedRoomIds.Add(connectedRoomId);
            }
        }

        public void SelectRoom(int roomId)
        {
            if (roomId < 0 || roomId >= rooms.Count)
                return;

            if (currentRoomId == -1)
                return;

            RoomNode currentRoom = rooms[currentRoomId];

            if (!currentRoom.connections.Contains(roomId))
                return;

            currentRoomId = roomId;
            visitedRoomIds.Add(roomId);
            revealedRoomIds.Add(roomId);
            RevealConnectedRooms(roomId);
            UpdateRoomState();
        }

        private void UpdateRoomState()
        {
            for (int i = 0; i < rooms.Count; i++)
            {
                RoomNode room = rooms[i];

                if (room.ui == null)
                    continue;

                bool isCurrent = room.id == currentRoomId;
                bool canSelect = currentRoomId != -1 && room.id != currentRoomId && rooms[currentRoomId].connections.Contains(room.id);
                bool isVisited = visitedRoomIds.Contains(room.id);
                bool isRevealed = revealedRoomIds.Contains(room.id);

                room.ui.SetState(isCurrent, canSelect, isVisited, isRevealed);
            }
        }

        private void ClearMap()
        {
            foreach (Transform child in mapContent)
            {
                Destroy(child.gameObject);
            }

            rooms.Clear();
            connections.Clear();
            visitedRoomIds.Clear();
            revealedRoomIds.Clear();

            currentRoomId = -1;
            startRoomId = -1;
            bossRoomId = -1;
        }

        public RoomType GetRoomType(int roomId)
        {
            if (roomId < 0 || roomId >= rooms.Count)
                return RoomType.Normal;

            return rooms[roomId].type;
        }
    }
}