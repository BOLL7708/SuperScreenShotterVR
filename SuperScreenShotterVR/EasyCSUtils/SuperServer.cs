using SuperSocket.SocketBase.Config;
using SuperSocket.WebSocket;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace BOLL7708.EasyCSUtils
{
    /**
     * Add two components from Nuget:
     * 1. SuperSocket.Engine
     * 2. SuperSocket.WebSocket
     */
    class SuperServer
    {
        public enum ServerStatus
        {
            Connected,
            Disconnected,
            Error,
            ReceivedCount,
            DeliveredCount,
            SessionCount
        }

        private WebSocketServer _server;
        private ConcurrentDictionary<string, WebSocketSession> _sessions = new ConcurrentDictionary<string, WebSocketSession>(); // Was getting crashes when loading all sessions from _server directly
        private volatile int _deliveredCount = 0;
        private volatile int _receivedCount = 0;

        #region Actions
        public Action<ServerStatus, int> StatusAction;
        public Action<WebSocketSession, string> MessageReceievedAction;
        public Action<WebSocketSession, byte[]> DataReceievedAction;
        public Action<WebSocketSession, bool, string> StatusMessageAction;
        #endregion

        public SuperServer(int port = 0)
        {
            ResetActions();
            if (port != 0) Start(port);
        }

        #region Manage
        public void Start(int port)
        {
            // Stop in case of already running
            Stop();

            // Start
            var config = new ServerConfig
            {
                MaxRequestLength = 1024 * 1024,
                ReceiveBufferSize = 1024 * 1024,
                Port = port
            };
            _server = new WebSocketServer();
            _server.Setup(config);
            // _server.Setup(port);
            _server.NewSessionConnected += Server_NewSessionConnected;
            _server.NewMessageReceived += Server_NewMessageReceived;
            _server.NewDataReceived += Server_NewDataReceived;
            _server.SessionClosed += Server_SessionClosed;
            var result = _server.Start();
            StatusAction.Invoke(result ? ServerStatus.Connected : ServerStatus.Error, 0);
        }

        public void Stop()
        {
            if (_server != null)
            {
                _server.Dispose();
                _server.Stop();
            }
            StatusAction.Invoke(ServerStatus.Disconnected, 0);
        }

        public void ResetActions()
        {
            StatusAction = (status, value) =>
            {
                Debug.WriteLine($"SuperServer.StatusAction not set, missed status: {status} {value}");
            };
            MessageReceievedAction = (session, message) =>
            {
                Debug.WriteLine($"SuperServer.MessageReceivedAction not set, missed message: {message}");
            };
            DataReceievedAction = (session, data) =>
            {
                Debug.WriteLine($"SuperServer.DataReceivedAction not set, missed data: {data.Length}");
            };
            StatusMessageAction = (session, connected, message) =>
            {
                Debug.WriteLine($"SuperServer.StatusMessageAction not set, missed status: {connected} {message}");
            };
        }
        #endregion

        #region Listeners 
        private void Server_NewSessionConnected(WebSocketSession session)
        {
            _sessions[session.SessionID] = session;
            StatusMessageAction.Invoke(session, true, $"New session connected: {session.SessionID}");
            StatusAction(ServerStatus.SessionCount, _sessions.Count);
        }

        private void Server_NewMessageReceived(WebSocketSession session, string value)
        {
            MessageReceievedAction.Invoke(session, value);
            _receivedCount++;
            StatusAction(ServerStatus.ReceivedCount, _receivedCount);
        }

        private void Server_NewDataReceived(WebSocketSession session, byte[] value)
        {
            DataReceievedAction.Invoke(session, value);
        }

        private void Server_SessionClosed(WebSocketSession session, SuperSocket.SocketBase.CloseReason value)
        {
            _sessions.TryRemove(session.SessionID, out WebSocketSession oldSession);
            StatusMessageAction.Invoke(null, false, $"Session closed: {session.SessionID}");
            StatusAction(ServerStatus.SessionCount, _sessions.Count);
        }
        #endregion

        #region Send
        public void SendMessage(WebSocketSession session, string message)
        {
            if (_server.State != SuperSocket.SocketBase.ServerState.Running) return;
            if (session != null && session.Connected)
            {
                session.Send(message);
                _deliveredCount++;
                StatusAction(ServerStatus.DeliveredCount, _deliveredCount);
            }
            else SendMessageToAll(message);
        }
        public void SendMessageToAll(string message)
        {
            foreach (var session in _sessions.Values)
            {
                if (session != null) SendMessage(session, message);
            }
        }
        #endregion
    }
}
