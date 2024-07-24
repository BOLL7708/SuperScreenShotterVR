﻿namespace SuperScreenShotterVR.Remote
{
    class ScreenshotResponse
    {
        public string Nonce = "";
        public string Image = "";
        public int Width = 0;
        public int Height = 0;
        public string FilePath = "";
        public string Message = "";
        public string Error = "";
        
        public static ScreenshotResponse Create(string nonce, string image, int width, int height, string filePath)
        {
            return new ScreenshotResponse()
            {
                Nonce = nonce,
                Image = image,
                Width = width,
                Height = height,
                FilePath = filePath
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
