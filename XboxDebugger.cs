using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using XDevkit;

namespace XboxDebugManager
{
    public class XboxDebugger : IDisposable
    {
        #region Events

        /// <summary>
        /// Raised when any debug event occurs
        /// </summary>
        public event EventHandler<DebugEventArgs> DebugEventOccurred;

        /// <summary>
        /// Raised for execution breaks
        /// </summary>
        public event EventHandler<DebugEventArgs> ExecutionBreak;

        /// <summary>
        /// Raised for debug output strings
        /// </summary>
        public event EventHandler<DebugEventArgs> DebugString;

        /// <summary>
        /// Raised when execution state changes (connection, disconnection, reboot etc)
        /// </summary>
        public event EventHandler<DebugEventArgs> ExecutionStateChanged;

        /// <summary>
        /// Raised when an exceptions occur on the console
        /// </summary>
        public event EventHandler<DebugEventArgs> ExceptionOccurred;

        /// <summary>
        /// Raised when assertions fail
        /// </summary>
        public event EventHandler<DebugEventArgs> AssertionFailed;

        /// <summary>
        /// Raised when data breakpoints are hit
        /// </summary>
        public event EventHandler<DebugEventArgs> DataBreakpoint;

        /// <summary>
        /// Raised for RIP (rest in piece) errors/fatal errors
        /// </summary>
        public event EventHandler<DebugEventArgs> RIPError;

        /// <summary>
        /// Raised for connection status changes
        /// </summary>
        public event EventHandler<bool> ConnectionStatusChanged;

        #endregion

        #region Private Fields

        private readonly XboxManager _xboxManager;
        private XboxConsole _xboxConsole;
        private IXboxDebugTarget _debugTarget;
        private XboxEvents_OnStdNotifyEventHandler _stdNotifyEventHandler;
        private bool _isConnected;
        private readonly string _xboxName;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        private readonly Dictionary<uint, bool> _frozenThreads = new Dictionary<uint, bool>();

        #endregion

        #region Public Properties

        public bool IsConnected => _isConnected;

        /// <summary>
        /// Gets the name of the connected console/debugger
        /// </summary>
        public string XboxName => _xboxName;

        #endregion

        #region Constructor and Initialization

        /// <summary>
        /// Initializes a new instance of the XboxDebugger class
        /// </summary>
        /// <param name="xboxName">The name or IP address of the Xbox to connect to</param>
        public XboxDebugger(string xboxName)
        {
            _xboxName = xboxName;
            _xboxManager = new XboxManager();
            _isConnected = false;
            _isDisposed = false;
        }

        /// <summary>
        /// Connects to the Xbox and initializes the debugger
        /// </summary>
        /// <returns>True if connection was successful, false otherwise</returns>
        public bool Connect()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("XboxDebugger");

            if (_isConnected)
                return true;

            try
            {
                lock (_lockObject)
                {
                    _xboxConsole = _xboxManager.OpenConsole(_xboxName);
                    _stdNotifyEventHandler = new XboxEvents_OnStdNotifyEventHandler(OnStdNotify);
                    _xboxConsole.OnStdNotify += _stdNotifyEventHandler;

                    _debugTarget = _xboxConsole.DebugTarget;
                    _debugTarget.ConnectAsDebugger(null, XboxDebugConnectFlags.Force);

                    return true;
                }
            }
            catch (Exception ex)
            {
                OnDebugEventOccurred(new DebugEventArgs(
                    XboxDebugEventType.DebugString,
                    $"Failed to connect to Xbox: {ex.Message}"));

                CleanupConnection();
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the Xbox and cleans up resources
        /// </summary>
        public void Disconnect()
        {
            if (!_isConnected)
                return;

            CleanupConnection();

            OnConnectionStatusChanged(false);
            _isConnected = false;
        }

        #endregion

        #region Event Handlers

        private void OnStdNotify(XboxDebugEventType eventCode, IXboxEventInfo eventInformation)
        {
            try
            {
                // Handles events on a task to avoid blocking the notification thread
                Task.Run(() => ProcessEvent(eventCode, eventInformation));
            }
            catch (Exception ex)
            {
                OnDebugEventOccurred(new DebugEventArgs(
                    XboxDebugEventType.DebugString,
                    $"Error in event handler: {ex.Message}"));
            }
        }

        private void ProcessEvent(XboxDebugEventType eventCode, IXboxEventInfo eventInformation)
        {
            try
            {
                var info = eventInformation.Info;
                var isThreadStopped = info.IsThreadStopped != 0;
                var threadId = info.Thread != null ? info.Thread.ThreadId : 0;

                // Tracks the thread state
                if (info.Thread != null)
                {
                    if (isThreadStopped)
                        _frozenThreads[threadId] = true;
                }

                // Create general event args to pass to handlers
                var eventArgs = new DebugEventArgs(
                    eventCode,
                    info.Message ?? string.Empty,
                    info.Address,
                    threadId,
                    eventCode == XboxDebugEventType.ExecStateChange ? (XboxExecutionState?)info.ExecState : null,
                    eventCode == XboxDebugEventType.Exception ? (uint?)info.Code : null
                );

                // Raisees the general event first
                OnDebugEventOccurred(eventArgs);

                // Processes specific event types
                switch (eventCode)
                {
                    case XboxDebugEventType.ExecutionBreak:
                        OnExecutionBreak(eventArgs);
                        break;

                    case XboxDebugEventType.DebugString:
                        OnDebugString(eventArgs);
                        break;

                    case XboxDebugEventType.ExecStateChange:
                        if (!_isConnected && info.ExecState != XboxExecutionState.Rebooting &&
                            info.ExecState != XboxExecutionState.RebootingTitle)
                        {
                            _isConnected = true;
                            OnConnectionStatusChanged(true);
                        }
                        else if (info.ExecState == XboxExecutionState.Rebooting ||
                                 info.ExecState == XboxExecutionState.RebootingTitle)
                        {
                            _isConnected = false;
                            OnConnectionStatusChanged(false);
                        }

                        OnExecutionStateChanged(eventArgs);
                        break;

                    case XboxDebugEventType.Exception:
                        if (info.Flags == XboxExceptionFlags.FirstChance && info.Code != 1080890248U)
                        {
                            OnExceptionOccurred(eventArgs);
                        }
                        break;

                    case XboxDebugEventType.AssertionFailed:
                        OnAssertionFailed(eventArgs);
                        break;

                    case XboxDebugEventType.DataBreak:
                        OnDataBreakpoint(eventArgs);
                        break;

                    case XboxDebugEventType.RIP:
                        OnRIPError(eventArgs);
                        break;
                }

                // Clean up of COM objects
                CleanupEventInfo(info);
                ReleaseComObject(eventInformation);
            }
            catch (Exception ex)
            {
                OnDebugEventOccurred(new DebugEventArgs(
                    XboxDebugEventType.DebugString,
                    $"Error processing event: {ex.Message}"));
            }
        }

        #endregion

        #region Helper Methods
        private bool EnsureConnected()
        {
            if (_isDisposed)
                throw new ObjectDisposedException("XboxDebugger");

            if (!_isConnected)
            {
                OnDebugEventOccurred(new DebugEventArgs(
                    XboxDebugEventType.DebugString,
                    "Not connected to Xbox. Call Connect() first."));
                return false;
            }

            return true;
        }

        private void CleanupConnection()
        {
            lock (_lockObject)
            {
                try
                {
                    if (_debugTarget != null)
                    {
                        try { _debugTarget.DisconnectAsDebugger(); } catch { }
                        ReleaseComObject(_debugTarget);
                        _debugTarget = null;
                    }

                    if (_xboxConsole != null)
                    {
                        if (_stdNotifyEventHandler != null)
                        {
                            try { _xboxConsole.OnStdNotify -= _stdNotifyEventHandler; } catch { }
                            _stdNotifyEventHandler = null;
                        }

                        ReleaseComObject(_xboxConsole);
                        _xboxConsole = null;
                    }

                    _frozenThreads.Clear();
                }
                catch (Exception ex)
                {
                    OnDebugEventOccurred(new DebugEventArgs(
                        XboxDebugEventType.DebugString,
                        $"Error during cleanup: {ex.Message}"));
                }
            }
        }

        private void CleanupEventInfo(XBOX_EVENT_INFO info)
        {
            if (info.Module != null) ReleaseComObject(info.Module);
            if (info.Section != null) ReleaseComObject(info.Section);
            if (info.Thread != null) ReleaseComObject(info.Thread);
        }

        private void ReleaseComObject(object obj)
        {
            if (obj != null)
            {
                try
                {
                    int count;
                    do
                    {
                        count = Marshal.ReleaseComObject(obj);
                    } while (count > 0);
                }
                catch { }
            }
        }

        #endregion

        #region Event Invocation Methods

        protected virtual void OnDebugEventOccurred(DebugEventArgs e)
        {
            DebugEventOccurred?.Invoke(this, e);
        }

        protected virtual void OnExecutionBreak(DebugEventArgs e)
        {
            ExecutionBreak?.Invoke(this, e);
        }

        protected virtual void OnDebugString(DebugEventArgs e)
        {
            DebugString?.Invoke(this, e);
        }

        protected virtual void OnExecutionStateChanged(DebugEventArgs e)
        {
            ExecutionStateChanged?.Invoke(this, e);
        }

        protected virtual void OnExceptionOccurred(DebugEventArgs e)
        {
            ExceptionOccurred?.Invoke(this, e);
        }

        protected virtual void OnAssertionFailed(DebugEventArgs e)
        {
            AssertionFailed?.Invoke(this, e);
        }

        protected virtual void OnDataBreakpoint(DebugEventArgs e)
        {
            DataBreakpoint?.Invoke(this, e);
        }

        protected virtual void OnRIPError(DebugEventArgs e)
        {
            RIPError?.Invoke(this, e);
        }

        protected virtual void OnConnectionStatusChanged(bool isConnected)
        {
            ConnectionStatusChanged?.Invoke(this, isConnected);
        }

        #endregion

        #region IDisposables

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            if (disposing)
            {
                Disconnect();

                if (_xboxManager != null)
                {
                    ReleaseComObject(_xboxManager);
                }
            }

            _isDisposed = true;
        }

        ~XboxDebugger()
        {
            Dispose(false);
        }

        #endregion
    }
}
