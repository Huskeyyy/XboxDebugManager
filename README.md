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

## 🚀 Getting Started

### Requirements

- .NET Framework or .NET Core/5+/6+
- Reference to **xDevkit.dll** (Can be found in the Xbox 360 SDK directory)

---

### 📥 Installation

Clone the repository or add the `XboxDebugManager.cs` and `DebugEventArgs.cs` to your project, alternatively, you can download the compiled .dll from the [Releases](https://github.com/Huskeyyy/XboxDebugManager/releases/) page and reference it in your project.

```bash
git clone https://github.com/Huskeyyy/XboxDebugManager.git
