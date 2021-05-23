using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperScreenShotterVR.Remote
{
    class ScreenshotMessage
    {
        public string nonce = "";
        public int delay = 0;
        public string tag = "";
        public SuperSocket.WebSocket.WebSocketSession session = null;
    }
}
