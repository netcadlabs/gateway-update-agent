using System;

namespace Netcad.NDU.GUA.Utils
{
    public class GUAException : Exception
    {
        public string Type { get; set; }
        public string UUID { get; set; }
        public GUAException(string type, string uuid, string message) : base(message)
        {
            this.Type = type;
            this.UUID = uuid;
        }
    }
}