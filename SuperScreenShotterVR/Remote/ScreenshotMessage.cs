﻿using SuperSocket.WebSocket.Server;

namespace SuperScreenShotterVR.Remote
{
    internal class ScreenshotMessage
    {
        public string Nonce = "";
        public int Delay = 0;
        public string Tag = "";
        internal WebSocketSession? Session = null;
    }
}