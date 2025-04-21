using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XDevkit;

namespace XboxDebugManager
{
    public class DebugEventArgs : EventArgs
    {
        public XboxDebugEventType EventType
        {
            get;
        }
        public string Message
        {
            get;
        }
        public uint Address
        {
            get;
        }
        public uint ThreadId
        {
            get;
        }
        public XboxExecutionState? ExecState
        {
            get;
        }
        public uint? ExceptionCode
        {
            get;
        }

        public DebugEventArgs(XboxDebugEventType eventType, string message = "", uint address = 0,
            uint threadId = 0, XboxExecutionState? execState = null, uint? exceptionCode = null)
        {
            EventType = eventType;
            Message = message;
            Address = address;
            ThreadId = threadId;
            ExecState = execState;
            ExceptionCode = exceptionCode;
        }
    }
}
