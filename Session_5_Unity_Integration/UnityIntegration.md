# 🎮 Unity Integration - 완전 가이드

## 📚 목차

- [개요](#개요)
- [프로젝트 구조](#프로젝트-구조)
- [Class 38: 프로젝트 설정](#class-38-프로젝트-설정)
- [Class 39: NetworkManager](#class-39-networkmanager)
- [Class 40: 플레이어 관리](#class-40-플레이어-관리)
- [Class 41: 패킷 핸들러](#class-41-패킷-핸들러)
- [실행 가이드](#실행-가이드)
- [트러블슈팅](#트러블슈팅)

---

## 개요

### 🎯 목표
C# 게임 서버와 Unity 클라이언트를 연동하여 멀티플레이어 게임 구현

### ✨ 기능
- ✅ 플레이어 입장/퇴장
- ✅ 실시간 이동 동기화
- ✅ 멀티플레이어 지원
- ✅ JobQueue 기반 안전한 처리

### 🏗️ 기술 스택
- **Server**: C# .NET Core, ServerCore 라이브러리
- **Client**: Unity 2020.3+
- **Protocol**: TCP/IP, 커스텀 패킷

---

## 프로젝트 구조

```
Solution: MMO_Game
│
├── ServerCore (Class Library)
│   ├── Session.cs
│   ├── PacketSession.cs
│   ├── RecvBuffer.cs
│   ├── SendBuffer.cs
│   ├── Listener.cs
│   └── Connector.cs
│
├── Common (Class Library)
│   ├── Packet.cs (자동 생성)
│   ├── PacketManager.cs
│   └── IPacket.cs
│
├── Server (Console App)
│   ├── Program.cs
│   ├── GameRoom.cs
│   ├── ClientSession.cs
│   └── PacketHandler.cs
│
└── UnityClient (Unity Project)
    └── Assets
        └── Scripts
            ├── NetworkManager.cs
            ├── ObjectManager.cs
            ├── PacketHandler.cs
            ├── Controllers
            │   ├── MyPlayerController.cs
            │   └── RemotePlayerController.cs
            └── Packet (Common에서 복사)
```

---

## Class 38: 프로젝트 설정

### 📝 개념

#### Unity와 서버 통신 구조
```
Unity Client (Main Thread)
    ↓ Send (C_Move)
    ↓
C# Server (JobQueue)
    ↓ Broadcast (S_BroadcastMove)
    ↓
Unity Clients (Network Thread → Main Thread)
```

#### 멀티스레드 문제
```csharp
// ❌ 잘못된 방법
void OnRecvPacket(S_BroadcastMove pkt) {
    player.transform.position = ...;  // 네트워크 스레드에서 호출!
}

// ✅ 올바른 방법
void OnRecvPacket(S_BroadcastMove pkt) {
    mainThreadQueue.Enqueue(() => {
        player.transform.position = ...;  // 메인 스레드에서 실행
    });
}
```

---

### 📄 Server/Program.cs

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using ServerCore;

namespace Server
{
    class ClientSession : PacketSession
    {
        public int SessionId { get; set; }
        public GameRoom Room { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }

        public override void OnConnected()
        {
            Console.WriteLine($"[Server] 클라이언트 연결: Session {SessionId}");
            Program.Room.Push(() => Program.Room.Enter(this));
        }

        public override void OnRecvPacket(ArraySegment<byte> buffer)
        {
            PacketManager.Instance.OnRecvPacket(this, buffer);
        }

        public override void OnSend(int numOfBytes)
        {
            // Console.WriteLine($"[Server] 전송: {numOfBytes} bytes");
        }

        public override void OnDisconnected()
        {
            Console.WriteLine($"[Server] 연결 종료: Session {SessionId}");
            Program.Room.Push(() => Program.Room.Leave(this));
        }
    }

    class Program
    {
        static Listener _listener = new Listener();
        public static GameRoom Room = new GameRoom();

        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════╗");
            Console.WriteLine("║     MMO 게임 서버 (Unity 연동)         ║");
            Console.WriteLine("╚════════════════════════════════════════╝");
            Console.WriteLine();

            // 패킷 매니저 등록
            PacketManager.Instance.Register();

            // 서버 시작
            string host = "127.0.0.1";
            int port = 7777;
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(host), port);

            _listener.Init(endPoint, () => {
                ClientSession session = new ClientSession();
                session.SessionId = SessionManager.Instance.Generate();
                SessionManager.Instance.Add(session);
                return session;
            });

            _listener.StartAccept();

            Console.WriteLine($"서버 시작: {host}:{port}");
            Console.WriteLine("명령어: quit (종료)\n");

            // GameRoom Update 스레드
            System.Threading.Thread updateThread = new System.Threading.Thread(() => {
                while (true)
                {
                    Room.Update();
                    System.Threading.Thread.Sleep(10);  // 100 FPS
                }
            });
            updateThread.IsBackground = true;
            updateThread.Start();

            // 명령어 처리
            while (true)
            {
                string cmd = Console.ReadLine();
                if (cmd == "quit")
                    break;
            }

            _listener.Stop();
            Console.WriteLine("\n서버 종료");
        }
    }

    class SessionManager
    {
        static SessionManager _instance = new SessionManager();
        public static SessionManager Instance { get { return _instance; } }

        int _sessionId = 0;
        Dictionary<int, ClientSession> _sessions = new Dictionary<int, ClientSession>();
        object _lock = new object();

        public int Generate()
        {
            lock (_lock)
            {
                return ++_sessionId;
            }
        }

        public void Add(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Add(session.SessionId, session);
            }
        }

        public void Remove(ClientSession session)
        {
            lock (_lock)
            {
                _sessions.Remove(session.SessionId);
            }
        }
    }
}
```

---

### 📄 Server/GameRoom.cs

```csharp
using System;
using System.Collections.Generic;
using ServerCore;

namespace Server
{
    class GameRoom : IJobQueue
    {
        List<ClientSession> _sessions = new List<ClientSession>();
        JobQueue _jobQueue = new JobQueue();
        Dictionary<int, ClientSession> _sessionDict = new Dictionary<int, ClientSession>();

        public void Push(Action job)
        {
            _jobQueue.Push(job);
        }

        public void Update()
        {
            // JobQueue는 자동 Flush
        }

        public void Enter(ClientSession session)
        {
            Push(() => {
                _sessions.Add(session);
                _sessionDict.Add(session.SessionId, session);
                session.Room = this;

                Console.WriteLine($"[GameRoom] Session {session.SessionId} 입장 (총 {_sessions.Count}명)");

                // 기존 플레이어 목록 전송
                S_PlayerList players = new S_PlayerList();
                foreach (ClientSession s in _sessions)
                {
                    S_PlayerList.Player p = new S_PlayerList.Player();
                    p.isSelf = (s == session);
                    p.playerId = s.SessionId;
                    p.posX = s.PosX;
                    p.posY = s.PosY;
                    p.posZ = s.PosZ;
                    players.players.Add(p);
                }

                session.Send(players.Write());

                // 입장 브로드캐스트
                S_BroadcastEnterGame enter = new S_BroadcastEnterGame();
                enter.playerId = session.SessionId;
                enter.posX = 0;
                enter.posY = 0;
                enter.posZ = 0;

                Broadcast(enter.Write(), session);
            });
        }

        public void Leave(ClientSession session)
        {
            Push(() => {
                if (session.Room == null)
                    return;

                _sessions.Remove(session);
                _sessionDict.Remove(session.SessionId);
                session.Room = null;

                Console.WriteLine($"[GameRoom] Session {session.SessionId} 퇴장 (총 {_sessions.Count}명)");

                // 퇴장 브로드캐스트
                S_BroadcastLeaveGame leave = new S_BroadcastLeaveGame();
                leave.playerId = session.SessionId;

                Broadcast(leave.Write());
            });
        }

        public void Move(ClientSession session, C_Move movePacket)
        {
            Push(() => {
                if (session.Room == null)
                    return;

                // 위치 업데이트
                session.PosX = movePacket.posX;
                session.PosY = movePacket.posY;
                session.PosZ = movePacket.posZ;

                // 이동 브로드캐스트
                S_BroadcastMove move = new S_BroadcastMove();
                move.playerId = session.SessionId;
                move.posX = movePacket.posX;
                move.posY = movePacket.posY;
                move.posZ = movePacket.posZ;

                Broadcast(move.Write());
            });
        }

        public void Broadcast(ArraySegment<byte> packet, ClientSession exclude = null)
        {
            foreach (ClientSession s in _sessions)
            {
                if (s == exclude)
                    continue;

                s.Send(packet);
            }
        }
    }

    interface IJobQueue
    {
        void Push(Action job);
    }

    class JobQueue
    {
        Queue<Action> _jobQueue = new Queue<Action>();
        object _lock = new object();
        bool _flushing = false;

        public void Push(Action job)
        {
            bool flush = false;

            lock (_lock)
            {
                _jobQueue.Enqueue(job);
                if (_flushing == false)
                {
                    _flushing = true;
                    flush = true;
                }
            }

            if (flush)
            {
                Flush();
            }
        }

        void Flush()
        {
            while (true)
            {
                Action job = null;

                lock (_lock)
                {
                    if (_jobQueue.Count == 0)
                    {
                        _flushing = false;
                        break;
                    }

                    job = _jobQueue.Dequeue();
                }

                job.Invoke();
            }
        }
    }
}
```

---

### 📄 Server/PacketHandler.cs

```csharp
using ServerCore;
using System;

namespace Server
{
    class PacketHandler
    {
        public static void C_MoveHandler(PacketSession session, IPacket packet)
        {
            C_Move movePacket = packet as C_Move;
            ClientSession clientSession = session as ClientSession;

            if (clientSession.Room == null)
                return;

            // Console.WriteLine($"[C_Move] Session {clientSession.SessionId}: ({movePacket.posX}, {movePacket.posY}, {movePacket.posZ})");

            clientSession.Room.Move(clientSession, movePacket);
        }
    }
}
```

---

## Class 39: NetworkManager

### 📝 개념

#### Unity 싱글톤 패턴
```csharp
public class NetworkManager : MonoBehaviour
{
    static NetworkManager _instance;
    public static NetworkManager Instance { get { return _instance; } }
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
```

#### 메인 스레드 큐
```
Network Thread          Main Thread (Unity)
    │                        │
    ├─ OnRecvPacket()       │
    │  └─ PushMainThread() ─┼→ Enqueue(action)
    │                        │
    │                        ├─ Update()
    │                        │  └─ Dequeue() → action.Invoke()
```

---

### 📄 UnityClient/Assets/Scripts/NetworkManager.cs

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using ServerCore;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    static NetworkManager _instance;
    public static NetworkManager Instance { get { return _instance; } }

    ServerSession _session = new ServerSession();
    Queue<Action> _mainThreadQueue = new Queue<Action>();
    object _lock = new object();

    public void Send(ArraySegment<byte> packet)
    {
        _session.Send(packet);
    }

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 패킷 매니저 등록
        PacketManager.Instance.Register();

        // 서버 연결
        string host = "127.0.0.1";
        int port = 7777;
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(host), port);

        Connector connector = new Connector();
        connector.Connect(endPoint, () => { return _session; }, 1);

        Debug.Log($"서버 연결 시도: {host}:{port}");
    }

    void Update()
    {
        // 메인 스레드 큐 처리
        List<Action> actions = new List<Action>();

        lock (_lock)
        {
            while (_mainThreadQueue.Count > 0)
            {
                actions.Add(_mainThreadQueue.Dequeue());
            }
        }

        foreach (Action action in actions)
        {
            action.Invoke();
        }
    }

    public void PushMainThread(Action action)
    {
        lock (_lock)
        {
            _mainThreadQueue.Enqueue(action);
        }
    }

    void OnDestroy()
    {
        if (_session != null)
        {
            _session.Disconnect();
        }
    }
}

class ServerSession : PacketSession
{
    public override void OnConnected()
    {
        Debug.Log("[Client] 서버 연결 성공!");
    }

    public override void OnRecvPacket(ArraySegment<byte> buffer)
    {
        PacketManager.Instance.OnRecvPacket(this, buffer);
    }

    public override void OnSend(int numOfBytes)
    {
        // Debug.Log($"[Client] 전송: {numOfBytes} bytes");
    }

    public override void OnDisconnected()
    {
        Debug.Log("[Client] 서버 연결 종료");
    }
}
```

---

## Class 40: 플레이어 관리

### 📝 개념

#### ObjectManager 역할
```
ObjectManager
    │
    ├─ MyPlayer (로컬 플레이어)
    │   ├─ MyPlayerController
    │   └─ 키보드 입력
    │
    └─ OtherPlayers (원격 플레이어)
        ├─ RemotePlayerController
        └─ 서버 위치로 이동
```

#### 생성 흐름
```
1. S_PlayerList 수신
   └─> 기존 플레이어 모두 생성

2. S_BroadcastEnterGame 수신
   └─> 새 플레이어 생성

3. S_BroadcastLeaveGame 수신
   └─> 플레이어 제거
```

---

### 📄 UnityClient/Assets/Scripts/Managers/ObjectManager.cs

```csharp
using System.Collections.Generic;
using UnityEngine;

public class ObjectManager : MonoBehaviour
{
    static ObjectManager _instance;
    public static ObjectManager Instance { get { return _instance; } }

    public GameObject MyPlayer { get; set; }
    Dictionary<int, GameObject> _players = new Dictionary<int, GameObject>();

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public GameObject Add(int playerId, bool isSelf)
    {
        if (_players.ContainsKey(playerId))
            return _players[playerId];

        // 프리팹 로드
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Player");
        if (prefab == null)
        {
            Debug.LogError("Player prefab을 찾을 수 없습니다!");
            return null;
        }

        GameObject go = Instantiate(prefab);
        go.name = $"Player_{playerId}";

        if (isSelf)
        {
            // MyPlayer
            MyPlayer = go;
            go.AddComponent<MyPlayerController>();
            
            // 색상 변경
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.green;
            
            // 카메라 설정
            if (Camera.main != null)
            {
                Camera.main.transform.SetParent(go.transform);
                Camera.main.transform.localPosition = new Vector3(0, 5, -5);
                Camera.main.transform.localRotation = Quaternion.Euler(45, 0, 0);
            }

            Debug.Log($"[ObjectManager] MyPlayer 생성: {playerId}");
        }
        else
        {
            // OtherPlayer
            go.AddComponent<RemotePlayerController>();
            
            // 색상 변경
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Color.red;

            Debug.Log($"[ObjectManager] OtherPlayer 생성: {playerId}");
        }

        _players.Add(playerId, go);

        return go;
    }

    public void Remove(int playerId)
    {
        if (_players.ContainsKey(playerId) == false)
            return;

        GameObject go = _players[playerId];
        _players.Remove(playerId);

        if (go == MyPlayer)
            MyPlayer = null;

        Debug.Log($"[ObjectManager] Player 제거: {playerId}");
        Destroy(go);
    }

    public GameObject Find(int playerId)
    {
        if (_players.ContainsKey(playerId))
            return _players[playerId];

        return null;
    }

    public void Clear()
    {
        foreach (GameObject go in _players.Values)
        {
            Destroy(go);
        }

        _players.Clear();
        MyPlayer = null;
    }
}
```

---

### 📄 UnityClient/Assets/Scripts/Controllers/MyPlayerController.cs

```csharp
using UnityEngine;

public class MyPlayerController : MonoBehaviour
{
    [SerializeField] float _speed = 5.0f;
    Vector3 _lastPosition;
    float _sendInterval = 0.1f;  // 100ms마다 전송
    float _sendTimer = 0;

    void Start()
    {
        _lastPosition = transform.position;
    }

    void Update()
    {
        // 입력 처리
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        Vector3 dir = new Vector3(h, 0, v).normalized;

        if (dir.magnitude > 0.1f)
        {
            transform.position += dir * _speed * Time.deltaTime;
        }

        // 주기적으로 전송
        _sendTimer += Time.deltaTime;

        if (_sendTimer >= _sendInterval)
        {
            _sendTimer = 0;

            // 위치가 변경되었으면 전송
            if (Vector3.Distance(transform.position, _lastPosition) > 0.01f)
            {
                _lastPosition = transform.position;
                SendMove();
            }
        }
    }

    void SendMove()
    {
        C_Move movePacket = new C_Move();
        movePacket.posX = transform.position.x;
        movePacket.posY = transform.position.y;
        movePacket.posZ = transform.position.z;

        NetworkManager.Instance.Send(movePacket.Write());
    }
}
```

---

### 📄 UnityClient/Assets/Scripts/Controllers/RemotePlayerController.cs

```csharp
using UnityEngine;

public class RemotePlayerController : MonoBehaviour
{
    Vector3 _targetPosition;
    [SerializeField] float _smoothSpeed = 10.0f;

    void Start()
    {
        _targetPosition = transform.position;
    }

    void Update()
    {
        // 목표 위치로 부드럽게 이동
        transform.position = Vector3.Lerp(
            transform.position, 
            _targetPosition, 
            Time.deltaTime * _smoothSpeed
        );
    }

    public void SetTargetPosition(Vector3 position)
    {
        _targetPosition = position;
    }
}
```

---

## Class 41: 패킷 핸들러

### 📝 개념

#### 패킷 처리 흐름
```
1. Network Thread: OnRecvPacket()
   ↓
2. PacketManager: OnRecvPacket()
   ↓
3. PacketHandler: S_XXXHandler()
   ↓
4. PushMainThread()
   ↓
5. Main Thread: Update()
   ↓
6. Unity API 호출 (GameObject, Transform, etc.)
```

#### 각 패킷의 역할
- **S_PlayerList**: 입장 시 기존 플레이어 목록
- **S_BroadcastEnterGame**: 새 플레이어 입장
- **S_BroadcastLeaveGame**: 플레이어 퇴장
- **S_BroadcastMove**: 플레이어 이동

---

### 📄 UnityClient/Assets/Scripts/Packet/PacketHandler.cs

```csharp
using ServerCore;
using UnityEngine;

class PacketHandler
{
    public static void S_BroadcastEnterGameHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastEnterGame pkt = packet as S_BroadcastEnterGame;

        NetworkManager.Instance.PushMainThread(() => {
            Debug.Log($"[S_BroadcastEnterGame] PlayerId: {pkt.playerId}");

            GameObject go = ObjectManager.Instance.Add(pkt.playerId, false);
            if (go != null)
            {
                go.transform.position = new Vector3(pkt.posX, pkt.posY, pkt.posZ);
            }
        });
    }

    public static void S_BroadcastLeaveGameHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastLeaveGame pkt = packet as S_BroadcastLeaveGame;

        NetworkManager.Instance.PushMainThread(() => {
            Debug.Log($"[S_BroadcastLeaveGame] PlayerId: {pkt.playerId}");

            ObjectManager.Instance.Remove(pkt.playerId);
        });
    }

    public static void S_PlayerListHandler(PacketSession session, IPacket packet)
    {
        S_PlayerList pkt = packet as S_PlayerList;

        NetworkManager.Instance.PushMainThread(() => {
            Debug.Log($"[S_PlayerList] Count: {pkt.players.Count}");

            foreach (S_PlayerList.Player p in pkt.players)
            {
                GameObject go = ObjectManager.Instance.Add(p.playerId, p.isSelf);
                if (go != null)
                {
                    go.transform.position = new Vector3(p.posX, p.posY, p.posZ);
                }
            }
        });
    }

    public static void S_BroadcastMoveHandler(PacketSession session, IPacket packet)
    {
        S_BroadcastMove pkt = packet as S_BroadcastMove;

        NetworkManager.Instance.PushMainThread(() => {
            GameObject go = ObjectManager.Instance.Find(pkt.playerId);
            if (go == null)
                return;

            // MyPlayer는 무시 (자기 자신)
            if (go == ObjectManager.Instance.MyPlayer)
                return;

            RemotePlayerController controller = go.GetComponent<RemotePlayerController>();
            if (controller != null)
            {
                Vector3 targetPos = new Vector3(pkt.posX, pkt.posY, pkt.posZ);
                controller.SetTargetPosition(targetPos);
            }
        });
    }
}
```

---

## 실행 가이드

### 1️⃣ 패킷 정의 (PDL.xml)

```xml
<?xml version="1.0" encoding="utf-8" ?>
<PDL>
  <!-- Client → Server -->
  <packet name="C_Move">
    <float name="posX"/>
    <float name="posY"/>
    <float name="posZ"/>
  </packet>

  <!-- Server → Client -->
  <packet name="S_BroadcastEnterGame">
    <int name="playerId"/>
    <float name="posX"/>
    <float name="posY"/>
    <float name="posZ"/>
  </packet>

  <packet name="S_BroadcastLeaveGame">
    <int name="playerId"/>
  </packet>

  <packet name="S_PlayerList">
    <list name="players">
      <bool name="isSelf"/>
      <int name="playerId"/>
      <float name="posX"/>
      <float name="posY"/>
      <float name="posZ"/>
    </list>
  </packet>

  <packet name="S_BroadcastMove">
    <int name="playerId"/>
    <float name="posX"/>
    <float name="posY"/>
    <float name="posZ"/>
  </packet>
</PDL>
```

---

### 2️⃣ 패킷 생성

```bash
# PacketGenerator 실행
cd PacketGenerator
dotnet run

# 생성된 파일을 Common 프로젝트로 복사
cp Packet.cs ../Common/
cp PacketManager.cs ../Common/
```

---

### 3️⃣ Unity 프로젝트 설정

#### Scene 구성
1. **빈 GameObject 생성**
    - 이름: `@Managers`
    - NetworkManager 컴포넌트 추가
    - ObjectManager 컴포넌트 추가

2. **Ground 생성**
    - 3D Object → Plane
    - Position: (0, 0, 0)
    - Scale: (10, 1, 10)

3. **Player Prefab 생성**
   ```
   1. 3D Object → Capsule 생성
   2. 이름: Player
   3. Position: (0, 1, 0)
   4. Resources/Prefabs 폴더 생성
   5. Player를 Prefab으로 저장
   6. Hierarchy에서 Player 삭제
   ```

#### 폴더 구조
```
Assets
├── Resources
│   └── Prefabs
│       └── Player.prefab
├── Scripts
│   ├── NetworkManager.cs
│   ├── Managers
│   │   └── ObjectManager.cs
│   ├── Controllers
│   │   ├── MyPlayerController.cs
│   │   └── RemotePlayerController.cs
│   └── Packet
│       ├── Packet.cs (Common에서 복사)
│       ├── PacketManager.cs (Common에서 복사)
│       ├── PacketHandler.cs
│       └── ServerCore (DLL 복사)
```

---

### 4️⃣ ServerCore DLL 복사

```bash
# ServerCore 빌드
cd ServerCore
dotnet build -c Release

# DLL을 Unity로 복사
cp bin/Release/netstandard2.1/ServerCore.dll ../UnityClient/Assets/Scripts/Packet/ServerCore/
```

또는 Unity에서 **ServerCore 소스 코드 직접 추가**:
```
Assets/Scripts/ServerCore/
├── Session.cs
├── PacketSession.cs
├── RecvBuffer.cs
├── SendBuffer.cs
├── Listener.cs
└── Connector.cs
```

---

### 5️⃣ 서버 실행

```bash
cd Server
dotnet run
```

출력:
```
╔════════════════════════════════════════╗
║     MMO 게임 서버 (Unity 연동)         ║
╚════════════════════════════════════════╝

서버 시작: 127.0.0.1:7777
명령어: quit (종료)
```

---

### 6️⃣ Unity 클라이언트 실행

1. **Unity Editor에서 실행**
    - Play 버튼 클릭
    - Console에서 "서버 연결 성공!" 확인
    - WASD로 이동

2. **여러 클라이언트 테스트**
   ```bash
   # 빌드
   File → Build Settings → Build
   
   # 빌드된 실행 파일 실행
   ./Build/MMOGame.exe
   ```

3. **동시 실행**
    - Unity Editor: 1개
    - Standalone Build: 1개 이상
    - 서로의 움직임이 동기화됨

---

## 트러블슈팅

### ❌ 문제 1: "Player prefab을 찾을 수 없습니다"

**원인**: Resources/Prefabs/Player.prefab이 없음

**해결**:
```
1. Resources 폴더 생성
2. Prefabs 폴더 생성
3. Player Capsule을 Prefab으로 저장
```

---

### ❌ 문제 2: "서버 연결 실패"

**원인**: 서버가 실행 중이 아님

**해결**:
```bash
# 서버 실행 확인
cd Server
dotnet run
```

**확인사항**:
- 방화벽 설정
- 포트 7777 사용 가능 여부
- IP 주소 (127.0.0.1)

---

### ❌ 문제 3: "플레이어가 움직이지 않음"

**원인**: 패킷이 전송/수신되지 않음

**해결**:
```csharp
// MyPlayerController.cs의 SendMove()에 로그 추가
void SendMove()
{
    Debug.Log("SendMove 호출!");
    // ...
}

// PacketHandler.cs에 로그 추가
public static void S_BroadcastMoveHandler(...)
{
    Debug.Log($"Move 수신: {pkt.playerId}");
    // ...
}
```

---

### ❌ 문제 4: "Unity API가 네트워크 스레드에서 호출됨"

**증상**:
```
UnityException: get_transform can only be called from the main thread
```

**원인**: Unity API를 네트워크 스레드에서 직접 호출

**해결**:
```csharp
// ❌ 잘못된 방법
void OnRecvPacket(S_BroadcastMove pkt) {
    player.transform.position = ...;
}

// ✅ 올바른 방법
void OnRecvPacket(S_BroadcastMove pkt) {
    NetworkManager.Instance.PushMainThread(() => {
        player.transform.position = ...;
    });
}
```

---

### ❌ 문제 5: "패킷이 파싱되지 않음"

**원인**: Common 프로젝트의 Packet.cs와 Unity의 Packet.cs가 다름

**해결**:
```bash
# Common/Packet.cs를 Unity로 복사
cp Common/Packet.cs UnityClient/Assets/Scripts/Packet/

# PacketManager도 동일하게 복사
cp Common/PacketManager.cs UnityClient/Assets/Scripts/Packet/
```

---

## 📊 성능 최적화

### 1. 패킷 전송 주기 조절

```csharp
// MyPlayerController.cs
float _sendInterval = 0.1f;  // 100ms (기본)

// 최적화
float _sendInterval = 0.05f;  // 50ms (부드러움)
float _sendInterval = 0.2f;   // 200ms (대역폭 절약)
```

---

### 2. 보간 속도 조절

```csharp
// RemotePlayerController.cs
float _smoothSpeed = 10.0f;  // 기본

// 최적화
float _smoothSpeed = 20.0f;  // 빠름
float _smoothSpeed = 5.0f;   // 느림
```

---

### 3. 서버 Update 주기

```csharp
// Server/Program.cs
System.Threading.Thread.Sleep(10);  // 100 FPS (기본)

// 최적화
System.Threading.Thread.Sleep(16);  // 60 FPS
System.Threading.Thread.Sleep(5);   // 200 FPS
```

---

## 🎯 핵심 정리

### Class 38: 프로젝트 설정
- ✅ GameRoom (JobQueue 기반)
- ✅ ClientSession (패킷 수신)
- ✅ 입장/퇴장/이동 처리

### Class 39: NetworkManager
- ✅ Unity 싱글톤 패턴
- ✅ 메인 스레드 큐
- ✅ ServerSession 구현

### Class 40: 플레이어 관리
- ✅ ObjectManager (GameObject 관리)
- ✅ MyPlayer vs OtherPlayer
- ✅ 프리팹 인스턴스화

### Class 41: 패킷 핸들러
- ✅ 메인 스레드에서 Unity API 호출
- ✅ 플레이어 동기화
- ✅ 입장/퇴장/이동 처리

---

## 🚀 다음 단계

### 추가 기능 구현
1. **채팅 시스템**
   ```csharp
   <packet name="C_Chat">
     <string name="message"/>
   </packet>
   
   <packet name="S_BroadcastChat">
     <int name="playerId"/>
     <string name="message"/>
   </packet>
   ```

2. **공격 시스템**
   ```csharp
   <packet name="C_Attack">
     <int name="targetId"/>
   </packet>
   
   <packet name="S_BroadcastAttack">
     <int name="attackerId"/>
     <int name="targetId"/>
     <int name="damage"/>
   </packet>
   ```

3. **HP 시스템**
   ```csharp
   <packet name="S_UpdateHp">
     <int name="playerId"/>
     <int name="hp"/>
   </packet>
   ```

4. **애니메이션**
    - Animator Controller 추가
    - 이동/공격 애니메이션
    - 상태 동기화

5. **아이템 시스템**
    - 아이템 드랍
    - 인벤토리
    - 장비 착용

---

## 📚 참고 자료

### ServerCore 라이브러리
- Session.cs
- PacketSession.cs
- RecvBuffer.cs
- SendBuffer.cs
- Listener.cs
- Connector.cs

### Unity API
- MonoBehaviour
- GameObject
- Transform
- Instantiate
- Resources.Load
- DontDestroyOnLoad

### C# 개념
- async/await
- Thread Safety
- JobQueue Pattern
- Singleton Pattern

---