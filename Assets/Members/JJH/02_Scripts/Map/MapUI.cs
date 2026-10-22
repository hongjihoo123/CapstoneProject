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

        private struct ConnectionCandidate
        {
            public int from;
            public int to;
            public float distance;

            public ConnectionCandidate(int from, int to, float distance)
            {
                this.from = from;
                this.to = to;
                this.distance = distance;
            }
        }

        [Serializable]
        public struct RoomNode
        {
            public int id;
            public RoomType type;
            public Vector2 position;
            public Vector2Int gridPosition;
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

        #region Generate Map

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
                Debug.LogError("MapContent 크기와 Map Padding을 확인해 주세요.", this);
                return;
            }

            List<Vector2> positions = new List<Vector2>();
            int maxAttempts = 1000;

            for (int i = 0; i < roomCount; i++)
            {
                bool placed = false;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    Vector2 position = new Vector2(Random.Range(minX, maxX), Random.Range(minY, maxY));
                    bool valid = true;

                    foreach (Vector2 existing in positions)
                    {
                        if (Vector2.Distance(position, existing) < minRoomDistance)
                        {
                            valid = false;
                            break;
                        }
                    }

                    if (!valid)
                        continue;

                    positions.Add(position);
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    Debug.LogWarning("방을 배치할 공간이 부족합니다. MapContent 크기를 늘리거나 Min Room Distance를 줄여 주세요.", this);
                    return;
                }
            }

            bool horizontal = Random.value < 0.5f;

            if (horizontal)
                positions.Sort((a, b) => a.x.CompareTo(b.x));
            else
                positions.Sort((a, b) => a.y.CompareTo(b.y));

            if (Random.value < 0.5f)
                positions.Reverse();

            for (int i = 0; i < positions.Count; i++)
            {
                RoomType type = i == 0 ? RoomType.Start :
                                i == positions.Count - 1 ? RoomType.Boss :
                                GetRandomRoomType();

                CreateRoom(positions[i], type, Vector2Int.zero);
            }

            startRoomId = 0;
            bossRoomId = rooms.Count - 1;
        }

        private int CreateRoom(Vector2 position, RoomType type, Vector2Int gridPosition)
        {
            RoomNode room = new RoomNode
            {
                id = rooms.Count,
                type = type,
                position = position,
                gridPosition = gridPosition,
                connections = new List<int>(),
                ui = null
            };

            rooms.Add(room);
            return room.id;
        }

        private void ConnectRooms()
        {
            List<ConnectionCandidate> candidates = new List<ConnectionCandidate>();

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    float distance = Vector2.Distance(rooms[i].position, rooms[j].position);
                    candidates.Add(new ConnectionCandidate(i, j, distance));
                }
            }

            candidates.Sort((a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < rooms.Count; i++)
            {
                for (int j = i + 1; j < rooms.Count; j++)
                {
                    Vector2Int a = rooms[i].gridPosition;
                    Vector2Int b = rooms[j].gridPosition;

                    if (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) == 1)
                        AddConnection(i, j);
                }
            }

            foreach (ConnectionCandidate candidate in candidates)
            {
                if (rooms[candidate.from].connections.Count >= 2 &&
                    rooms[candidate.to].connections.Count >= 2)
                    continue;

                if (rooms[candidate.from].connections.Contains(candidate.to))
                    continue;

                if (WouldCrossConnection(candidate.from, candidate.to) ||
                        WouldOverlapRoom(candidate.from, candidate.to))
                    continue;

                AddConnection(candidate.from, candidate.to);
            }

            int addedConnections = 0;

            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                (candidates[i], candidates[randomIndex]) = (candidates[randomIndex], candidates[i]);
            }

            foreach (ConnectionCandidate candidate in candidates)
            {
                if (addedConnections >= extraConnectionCount)
                    break;

                if (rooms[candidate.from].connections.Contains(candidate.to))
                    continue;

                if (WouldCrossConnection(candidate.from, candidate.to))
                    continue;

                AddConnection(candidate.from, candidate.to);
                addedConnections++;
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

        private bool WouldCrossConnection(int fromId, int toId)
        {
            Vector2 a = rooms[fromId].position;
            Vector2 b = rooms[toId].position;

            foreach (RoomNode room in rooms)
            {
                foreach (int connectedId in room.connections)
                {
                    if (room.id >= connectedId)
                        continue;

                    if (room.id == fromId || room.id == toId ||
                        connectedId == fromId || connectedId == toId)
                        continue;

                    Vector2 c = rooms[room.id].position;
                    Vector2 d = rooms[connectedId].position;

                    if (SegmentsIntersect(a, b, c, d))
                        return true;
                }
            }

            return false;
        }

        private bool WouldOverlapRoom(int fromId, int toId)
        {
            Vector2 start = rooms[fromId].position;
            Vector2 end = rooms[toId].position;

            foreach (RoomNode room in rooms)
            {
                if (room.id == fromId || room.id == toId)
                    continue;

                RectTransform rect = roomPrefab.GetComponent<RectTransform>();
                Vector2 size = Vector2.Scale(rect.rect.size, rect.localScale);

                Rect roomRect = new Rect(room.position - size * 0.5f, size);

                if (SegmentIntersectsRect(start, end, roomRect))
                    return true;
            }

            return false;
        }

        private bool SegmentIntersectsRect(Vector2 start, Vector2 end, Rect rect)
        {
            if (rect.Contains(start) || rect.Contains(end))
                return true;

            Vector2 topLeft = new Vector2(rect.xMin, rect.yMax);
            Vector2 topRight = new Vector2(rect.xMax, rect.yMax);
            Vector2 bottomLeft = new Vector2(rect.xMin, rect.yMin);
            Vector2 bottomRight = new Vector2(rect.xMax, rect.yMin);

            return SegmentsIntersect(start, end, topLeft, topRight) ||
                   SegmentsIntersect(start, end, topRight, bottomRight) ||
                   SegmentsIntersect(start, end, bottomRight, bottomLeft) ||
                   SegmentsIntersect(start, end, bottomLeft, topLeft);
        }

        private bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float abC = Cross(b - a, c - a);
            float abD = Cross(b - a, d - a);
            float cdA = Cross(d - c, a - c);
            float cdB = Cross(d - c, b - c);

            return abC * abD < 0f && cdA * cdB < 0f;
        }

        private float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        #endregion

        #region Generate UI

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

        #endregion

        public void SelectRoom(int roomId)
        {
            if (roomId < 0 || roomId >= rooms.Count)
                return;

            if (currentRoomId == -1 || roomId == currentRoomId)
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

        public RoomType GetRoomType(int roomId)
        {
            if (roomId < 0 || roomId >= rooms.Count)
                return RoomType.Normal;

            return rooms[roomId].type;
        }
    }
}