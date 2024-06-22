using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SuperScreenShotterVR.Remote
{
    class ScreenshotResponse
    {
        public string Nonce = "";
        public string Image = "";
        public int Width = 0;
        public int Height = 0;
        public string Message = "";
        public string Error = "";
        
        public static ScreenshotResponse Create(string nonce, string image, int width, int height)
        {
            return new ScreenshotResponse()
            {
                Nonce = nonce,
                Image = image,
                Width = width,
                Height = height
            };
        }
        
        public static ScreenshotResponse Create(string nonce, string message, string error)
        {
            return new ScreenshotResponse()
            {
                Nonce = nonce,
                Message = message,
                Error = error
            };
        }
    }
}
