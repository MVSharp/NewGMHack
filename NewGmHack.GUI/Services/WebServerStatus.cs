using System;

namespace NewGmHack.GUI.Services
{
    public interface IWebServerStatus
    {
        string BaseUrl { get; set; }
    }

    public class WebServerStatus : IWebServerStatus
    {
        public string BaseUrl { get; set; } = "http://localhost:5000"; // Default fallback
    }
}
