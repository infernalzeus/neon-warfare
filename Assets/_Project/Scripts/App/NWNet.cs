using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace NW.Net
{
    /// <summary>
    /// Lightweight TCP peer-to-peer bridge for competitive 1v1.
    ///
    /// Protocol: framed messages — 2-byte big-endian length prefix + payload.
    /// Message payload[0] = MsgType. All I/O on a background thread;
    /// received messages are dispatched on the Unity main thread via Update().
    ///
    /// Usage:
    ///   Host:   NWNet.Inst.StartHost()  → waits for one connection
    ///   Client: NWNet.Inst.JoinHost(ip) → connects to host
    ///   Both:   subscribe OnConnected, OnCrossoverReceived, OnOpponentDead
    /// </summary>
    public sealed class NWNet : MonoBehaviour
    {
        // ── singleton ────────────────────────────────────────────────────────────
        public static NWNet Inst { get; private set; }

        // ── state ────────────────────────────────────────────────────────────────
        public bool   IsActive    { get; private set; }
        public bool   IsHost      { get; private set; }
        public bool   IsConnected { get; private set; }
        public string StatusText  { get; private set; } = "Disconnected";

        // ── events (fired on the Unity main thread) ───────────────────────────
        public event Action                     OnConnected;
        public event Action                     OnDisconnected;
        public event Action<IncomingCrossover>  OnCrossoverReceived;
        public event Action                     OnOpponentDead;

        public readonly struct IncomingCrossover
        {
            public readonly NW.App.CrossoverKind Kind;
            public readonly int                  Lane;
            public readonly float                Damage;
            public IncomingCrossover(NW.App.CrossoverKind k, int l, float d)
            { Kind = k; Lane = l; Damage = d; }
        }

        // ── constants ────────────────────────────────────────────────────────────
        public const int DefaultPort = 7777;

        enum MsgType : byte { Handshake = 0, Crossover = 1, CoreDead = 2 }

        // ── internals ────────────────────────────────────────────────────────────
        TcpListener   _listener;
        TcpClient     _socket;
        NetworkStream _stream;
        Thread        _bgThread;

        readonly ConcurrentQueue<byte[]> _recvQ = new();
        volatile bool _pendingConnected;
        volatile bool _pendingDisconnected;

        // ── lifecycle ────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Inst != null && Inst != this) { Destroy(gameObject); return; }
            Inst = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            ShutdownInternal();
            if (Inst == this) Inst = null;
        }

        void Update()
        {
            if (_pendingConnected)
            {
                _pendingConnected = false;
                IsConnected = true;
                StatusText  = "Connected";
                OnConnected?.Invoke();
            }

            while (_recvQ.TryDequeue(out var data))
                DispatchMsg(data);

            if (_pendingDisconnected)
            {
                _pendingDisconnected = false;
                bool wasConn = IsConnected;
                IsConnected  = false;
                StatusText   = "Disconnected";
                if (wasConn) OnDisconnected?.Invoke();
            }
        }

        // ── public API ───────────────────────────────────────────────────────────

        public void StartHost(int port = DefaultPort)
        {
            ShutdownInternal();
            IsActive = true; IsHost = true;
            StatusText = $"Hosting :{port} — waiting…";
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _bgThread = new Thread(() => AcceptThenRecv()) { IsBackground = true, Name = "NWNet-host" };
            _bgThread.Start();
            Debug.Log($"[NWNet] Hosting on :{port}");
        }

        public void JoinHost(string ip, int port = DefaultPort)
        {
            ShutdownInternal();
            IsActive = true; IsHost = false;
            StatusText = $"Connecting to {ip}:{port}…";
            _bgThread = new Thread(() => ConnectThenRecv(ip, port)) { IsBackground = true, Name = "NWNet-client" };
            _bgThread.Start();
        }

        public void Disconnect()
        {
            ShutdownInternal();
            StatusText = "Disconnected";
        }

        public void SendCrossover(NW.App.CrossoverKind kind, int lane, float damage)
        {
            if (!IsConnected) return;
            var p = new byte[7];
            p[0] = (byte)MsgType.Crossover;
            p[1] = (byte)kind;
            p[2] = (byte)(lane + 1);  // +1 so lane -1 encodes as 0
            Buffer.BlockCopy(BitConverter.GetBytes(damage), 0, p, 3, 4);
            SendFrame(p);
        }

        public void SendCoreDead()
        {
            if (!IsConnected) return;
            SendFrame(new byte[] { (byte)MsgType.CoreDead });
        }

        // ── background threads ───────────────────────────────────────────────────

        void AcceptThenRecv()
        {
            try
            {
                _socket = _listener.AcceptTcpClient();
                _stream = _socket.GetStream();
                SendFrame(new byte[] { (byte)MsgType.Handshake });
                _pendingConnected = true;
                RecvLoop();
            }
            catch (Exception e)
            {
                if (IsActive) Debug.LogWarning($"[NWNet] AcceptThenRecv: {e.Message}");
                _pendingDisconnected = true;
            }
        }

        void ConnectThenRecv(string ip, int port)
        {
            try
            {
                _socket = new TcpClient();
                _socket.Connect(ip, port);
                _stream = _socket.GetStream();
                SendFrame(new byte[] { (byte)MsgType.Handshake });
                _pendingConnected = true;
                RecvLoop();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NWNet] ConnectThenRecv: {e.Message}");
                _pendingDisconnected = true;
            }
        }

        void RecvLoop()
        {
            var lenBuf = new byte[2];
            try
            {
                while (IsActive && _stream != null)
                {
                    int got = 0;
                    while (got < 2)
                    {
                        int n = _stream.Read(lenBuf, got, 2 - got);
                        if (n == 0) return;
                        got += n;
                    }
                    int len = (lenBuf[0] << 8) | lenBuf[1];
                    if (len <= 0 || len > 1024) continue;

                    var payload = new byte[len];
                    got = 0;
                    while (got < len)
                    {
                        int n = _stream.Read(payload, got, len - got);
                        if (n == 0) return;
                        got += n;
                    }
                    _recvQ.Enqueue(payload);
                }
            }
            catch (Exception e)
            {
                if (IsActive) Debug.LogWarning($"[NWNet] RecvLoop: {e.Message}");
                _pendingDisconnected = true;
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        void SendFrame(byte[] payload)
        {
            if (_stream == null) return;
            try
            {
                int len = payload.Length;
                var frame = new byte[2 + len];
                frame[0] = (byte)(len >> 8);
                frame[1] = (byte)(len & 0xFF);
                Buffer.BlockCopy(payload, 0, frame, 2, len);
                lock (_stream) { _stream.Write(frame, 0, frame.Length); _stream.Flush(); }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NWNet] SendFrame: {e.Message}");
                _pendingDisconnected = true;
            }
        }

        void DispatchMsg(byte[] data)
        {
            if (data.Length == 0) return;
            switch ((MsgType)data[0])
            {
                case MsgType.Handshake:
                    break;

                case MsgType.Crossover when data.Length >= 7:
                    var kind  = (NW.App.CrossoverKind)data[1];
                    int lane  = data[2] - 1;
                    float dmg = BitConverter.ToSingle(data, 3);
                    OnCrossoverReceived?.Invoke(new IncomingCrossover(kind, lane, dmg));
                    break;

                case MsgType.CoreDead:
                    OnOpponentDead?.Invoke();
                    break;
            }
        }

        void ShutdownInternal()
        {
            IsActive    = false;
            IsConnected = false;
            try { _stream?.Close(); }  catch { }
            try { _socket?.Close(); }  catch { }
            try { _listener?.Stop(); } catch { }
            _stream   = null;
            _socket   = null;
            _listener = null;
        }
    }
}
