# XboxDebugManager

`XboxDebugManager` is a C# debugging utility for interracting with RGH/XDK/Jtag consoles. It allows you to connect, monitor, freeze/unfreeze, and handle debug events in real-time.

---

![image](https://github.com/user-attachments/assets/5b0ba7bd-1e8b-48dd-ac02-0f02ed10d0ca)

## Features

- Connect to and monitor modified 360 consoles  
- Freeze and resume console execution  
- Listen for debug events, breakpoints, exceptions, RIPs, and more  
- Handles connection/reboot state changes  
- Clean COM-based resource management  

---

## Example usage

```c#
 static async Task Main()
    {
        // Create and manage the debug controller
        using var debugController = new DebugMonitorController();

        // Subscribe to log output events
        debugController.LogMessageReceived += (s, msg) => Console.WriteLine($"[Log] {msg}");

        // Subscribe to debugger event notifications (e.g. breakpoints, exceptions)
        debugController.DebugEventOccurred += (s, e) => Console.WriteLine($"[Debug] {e.Message}");

        // Start the debugger and connect to the console
        await debugController.StartDebuggingAsync();

        // Freeze the console (e.g. pause execution)
        await debugController.FreezeConsoleAsync();

        // Unfreeze the console (resume execution)
        await debugController.UnfreezeConsoleAsync();

        // Stop debugging and clean up connections
        await debugController.StopDebuggingAsync();
    }
```

---

## 🚀 Getting Started

### Requirements

- .NET Framework or .NET Core/5+/6+
- Reference to **xDevkit.dll** (Can be found in the Xbox 360 SDK directory)

---

### 📥 Installation

Clone the repository or add the `XboxDebugManager.cs` and `DebugEventArgs.cs` to your project, alternatively, you can download the compiled .dll from the [Releases](https://github.com/Huskeyyy/XboxDebugManager/releases/) page and reference it in your project.

```bash
git clone https://github.com/Huskeyyy/XboxDebugManager.git
