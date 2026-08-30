# Dynamic Reeling 🎣

**DynamicReeling** is a MelonLoader mod for the game *How to Fish*. It replaces the default fishing mechanics with a modern, responsive, and aesthetically pleasing minigame inspired by *Web Fishing* and *Stardew Valley*.

![DynamicReeling Preview](https://via.placeholder.com/800x400?text=DynamicReeling+UI+Preview) *(Replace with an actual screenshot)*

## ✨ Features

- **Modern Native UI:** Built natively on Unity's UI Canvas (`RectTransform`, procedural 9-slice rounded corners, custom themes) without using legacy `OnGUI`.
- **Dynamic Catch Progression:** Reel in fish by keeping your marker inside the moving safe capsule zone.
- **Tension Mechanics:** Monitor line tension with visual warnings and smooth color feedback.
- **Standalone & Lightweight:** No external mod manager dependencies required.
- **Pause & Menu Friendly:** Automatically halts and hides when the pause menu (`ESC`) is open.
- **Smooth Animations:** Includes fade and scale UI opening/closing transitions.

## ⚙️ Controls

| Action | Key / Input |
| :--- | :--- |
| **Reel In / Hold Zone** | `Mouse Left Click` or `E` key |
| **Toggle Mod On/Off** | `F3` key |

## 📥 Installation

1. Make sure you have **[MelonLoader](https://github.com/LavaGang/MelonLoader)** (v0.6.0+) installed for *How to Fish*.
2. Download the latest `DynamicReeling.dll` release from the [Releases](../../releases) tab.
3. Drop `DynamicReeling.dll` into your game's `Mods` folder:
   `...\Steam\steamapps\common\How to Fish\How to Fish\Mods\`
4. Launch the game and enjoy!

## 🛠️ Building from Source

### Prerequisites
- Visual Studio 2022 / .NET SDK
- Target Framework: `.NET Framework 4.7.2`

### Game Assemblies Required
Ensure your `.csproj` points to the correct paths for the following assemblies located in `How to Fish_Data/Managed` and `MelonLoader/net472`:

- `MelonLoader.dll` & `0Harmony.dll`
- `Assembly-CSharp.dll`
- `UnityEngine.dll`, `UnityEngine.CoreModule.dll`, `UnityEngine.UI.dll`, `UnityEngine.UIModule.dll`, `UnityEngine.TextRenderingModule.dll`, `UnityEngine.InputLegacyModule.dll`

1. Clone the repository:
   ```bash
   git clone [https://github.com/cinar59101/DynamicReeling.git](https://github.com/cinar59101/DynamicReeling.git)
