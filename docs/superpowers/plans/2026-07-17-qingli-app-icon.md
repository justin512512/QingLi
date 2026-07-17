# QingLi App Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create and integrate the approved calendar-and-crescent icon across QingLi executables, windows, shortcuts, taskbar, and tray, then publish v0.1.2.

**Architecture:** Keep one transparent high-resolution PNG as the visual source and generate one multi-resolution Windows ICO from it. Embed the ICO through MSBuild for executable and WPF application identity, and load that same embedded resource for the notification-area icon so every Windows surface uses one brand asset.

**Tech Stack:** OpenAI built-in image generation, PNG/ICO, Pillow, .NET 8, WPF, Windows Forms `NotifyIcon`, xUnit, PowerShell packaging, GitHub Releases.

---

## File Structure

- Create `src/QingLi.Windows/Assets/Brand/qingli-app-icon-source.png`: approved transparent 1024×1024 source artwork.
- Create `src/QingLi.Windows/Assets/Brand/QingLi.ico`: Windows icon containing 16, 20, 24, 32, 40, 48, 64, 128, and 256 pixel layers.
- Modify `src/QingLi.Windows/QingLi.Windows.csproj`: embed and assign the application icon.
- Modify `src/QingLi.Recovery/QingLi.Recovery.csproj`: assign the same application icon through a linked resource.
- Modify `src/QingLi.Windows/Tray/QingLiTrayIcon.cs`: load the embedded ICO instead of drawing a placeholder bitmap.
- Modify `tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs`: verify resource-backed tray icon behavior and project icon declarations.
- Modify `Directory.Build.props` and `tests/QingLi.Core.Tests/SmokeTests.cs`: advance and verify version 0.1.2.

### Task 1: Generate and Validate the Brand Artwork

**Files:**
- Create: `src/QingLi.Windows/Assets/Brand/qingli-app-icon-source.png`

- [ ] **Step 1: Generate the approved artwork**

Use the built-in image generation tool with this exact prompt:

```text
Use case: logo-brand
Asset type: Windows 11 desktop application icon source
Primary request: Create a clean icon for a Chinese calendar application named QingLi, using no text.
Subject: a white tear-off calendar page with a dark navy binding header, combined with a warm golden crescent moon in the lower-right corner.
Style/medium: polished flat vector-friendly app icon, simple geometric shapes, crisp silhouette, subtle depth only.
Composition/framing: centered inside a rounded-square tile with generous safe padding; readable at 16×16 pixels.
Color palette: cyan-to-royal-blue-to-indigo background gradient, white calendar page, dark navy header, golden-yellow crescent.
Constraints: no letters, no Chinese characters, no numbers, no grid details, no watermark; strong contrast; perfectly flat solid #00ff00 chroma-key outside the rounded-square icon; do not use #00ff00 in the icon.
```

- [ ] **Step 2: Remove the chroma key**

Run:

```powershell
$generatedSource = (Get-ChildItem "$env:USERPROFILE\.codex\generated_images" -Recurse -Filter *.png | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
python "$env:USERPROFILE\.codex\skills\.system\imagegen\scripts\remove_chroma_key.py" --input $generatedSource --out src\QingLi.Windows\Assets\Brand\qingli-app-icon-source.png --auto-key border --soft-matte --transparent-threshold 12 --opaque-threshold 220 --despill
```

Expected: a 1024×1024 RGBA PNG with transparent corners and no green fringe.

- [ ] **Step 3: Visually validate large and small previews**

Inspect the source and 256, 32, and 16 pixel downscaled previews. Confirm the calendar page and crescent remain distinct on light and dark backgrounds. If one detail fails, regenerate once with only that detail clarified.

- [ ] **Step 4: Commit the source artwork**

```powershell
git add src/QingLi.Windows/Assets/Brand/qingli-app-icon-source.png
git commit -m "design: add QingLi calendar moon icon"
```

### Task 2: Build the Multi-Resolution Windows ICO

**Files:**
- Create: `src/QingLi.Windows/Assets/Brand/QingLi.ico`
- Create: `scripts/build-icon.py`
- Test: `tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs`

- [ ] **Step 1: Write a failing asset validation test**

Add a test that resolves `src/QingLi.Windows/Assets/Brand/QingLi.ico`, opens it with `System.Drawing.Icon`, and asserts both `Width` and `Height` are 256. Also assert the source PNG exists.

```csharp
[Fact]
public void Brand_icon_assets_exist_and_primary_frame_is_256_pixels()
{
    var root = GetRepositoryRoot();
    var source = Path.Combine(root, "src", "QingLi.Windows", "Assets", "Brand", "qingli-app-icon-source.png");
    var ico = Path.Combine(root, "src", "QingLi.Windows", "Assets", "Brand", "QingLi.ico");

    Assert.True(File.Exists(source));
    Assert.True(File.Exists(ico));
    using var icon = new Icon(ico, 256, 256);
    Assert.Equal(256, icon.Width);
    Assert.Equal(256, icon.Height);
}
```

- [ ] **Step 2: Run the test and verify it fails**

Run:

```powershell
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj --filter Brand_icon_assets_exist_and_primary_frame_is_256_pixels
```

Expected: FAIL because `QingLi.ico` does not exist.

- [ ] **Step 3: Create the deterministic ICO builder**

Create `scripts/build-icon.py` using Pillow. Open the transparent source as RGBA, resize with `Image.Resampling.LANCZOS`, apply slight unsharp masking only below 48 pixels, and save ICO frames at `[16, 20, 24, 32, 40, 48, 64, 128, 256]`.

```python
from pathlib import Path
from PIL import Image, ImageFilter

root = Path(__file__).resolve().parents[1]
source = root / "src/QingLi.Windows/Assets/Brand/qingli-app-icon-source.png"
output = root / "src/QingLi.Windows/Assets/Brand/QingLi.ico"
sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]

image = Image.open(source).convert("RGBA")
frames = []
for size in sizes:
    frame = image.resize((size, size), Image.Resampling.LANCZOS)
    if size < 48:
        frame = frame.filter(ImageFilter.UnsharpMask(radius=0.6, percent=120, threshold=2))
    frames.append(frame)

frames[-1].save(output, format="ICO", append_images=frames[:-1], sizes=[(s, s) for s in sizes])
```

- [ ] **Step 4: Generate the ICO and rerun the test**

```powershell
python scripts\build-icon.py
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj --filter Brand_icon_assets_exist_and_primary_frame_is_256_pixels
```

Expected: PASS.

- [ ] **Step 5: Commit the icon pipeline**

```powershell
git add scripts/build-icon.py src/QingLi.Windows/Assets/Brand/QingLi.ico tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs
git commit -m "build: generate multi-size Windows icon"
```

### Task 3: Embed the Icon in Both Executables

**Files:**
- Modify: `src/QingLi.Windows/QingLi.Windows.csproj`
- Modify: `src/QingLi.Recovery/QingLi.Recovery.csproj`
- Test: `tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs`

- [ ] **Step 1: Write failing project configuration assertions**

Add assertions that `QingLi.Windows.csproj` contains `<ApplicationIcon>Assets\Brand\QingLi.ico</ApplicationIcon>` and an embedded resource declaration, while `QingLi.Recovery.csproj` contains the linked icon and its own `ApplicationIcon`.

- [ ] **Step 2: Run and verify failure**

```powershell
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj --filter Project_files_assign_the_brand_icon
```

Expected: FAIL because neither project assigns an application icon.

- [ ] **Step 3: Configure `QingLi.Windows.csproj`**

Add:

```xml
<ApplicationIcon>Assets\Brand\QingLi.ico</ApplicationIcon>
```

and:

```xml
<Resource Include="Assets\Brand\QingLi.ico" />
```

- [ ] **Step 4: Configure `QingLi.Recovery.csproj`**

Add:

```xml
<ApplicationIcon>..\QingLi.Windows\Assets\Brand\QingLi.ico</ApplicationIcon>
```

No duplicate copy is needed because the compiler consumes the linked source file directly.

- [ ] **Step 5: Run the configuration test and build both projects**

```powershell
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj --filter Project_files_assign_the_brand_icon
E:\claude_data\.dotnet\dotnet.exe build src\QingLi.Windows\QingLi.Windows.csproj -c Release
E:\claude_data\.dotnet\dotnet.exe build src\QingLi.Recovery\QingLi.Recovery.csproj -c Release
```

Expected: all commands exit 0.

- [ ] **Step 6: Commit executable integration**

```powershell
git add src/QingLi.Windows/QingLi.Windows.csproj src/QingLi.Recovery/QingLi.Recovery.csproj tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs
git commit -m "feat: embed QingLi icon in Windows executables"
```

### Task 4: Use the Embedded Icon in the Notification Area

**Files:**
- Modify: `src/QingLi.Windows/Tray/QingLiTrayIcon.cs`
- Test: `tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs`

- [ ] **Step 1: Replace the old source-text assertion with a failing resource assertion**

Assert that `QingLiTrayIcon.Create()` returns an icon whose 32×32 bitmap contains non-transparent pixels and that the source no longer contains `FillRoundedRectangle` or `GetHicon`.

- [ ] **Step 2: Run and verify failure**

```powershell
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj --filter Tray_icon
```

Expected: FAIL while the runtime-drawn placeholder remains.

- [ ] **Step 3: Implement embedded-resource loading**

Replace the drawing implementation with:

```csharp
using System.Drawing;
using System.Windows;

namespace QingLi.Windows.Tray;

public static class QingLiTrayIcon
{
    public static Icon Create()
    {
        var resource = Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/Brand/QingLi.ico"))
            ?? throw new InvalidOperationException("The QingLi application icon resource is missing.");

        using (resource.Stream)
        using (var icon = new Icon(resource.Stream, 32, 32))
        {
            return (Icon)icon.Clone();
        }
    }
}
```

- [ ] **Step 4: Run tray tests**

```powershell
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Windows.Tests\QingLi.Windows.Tests.csproj --filter Tray
```

Expected: PASS.

- [ ] **Step 5: Commit tray integration**

```powershell
git add src/QingLi.Windows/Tray/QingLiTrayIcon.cs tests/QingLi.Windows.Tests/Tray/TrayIconServiceTests.cs
git commit -m "feat: unify tray and application icons"
```

### Task 5: Version, Verify, Package, and Publish v0.1.2

**Files:**
- Modify: `Directory.Build.props`
- Modify: `tests/QingLi.Core.Tests/SmokeTests.cs`
- Create: `artifacts/QingLi-0.1.2-win-x64-portable.zip`

- [ ] **Step 1: Write the version expectation first**

Change the smoke test to expect `new Version(0, 1, 2, 0)` and run it before changing production version metadata.

```powershell
E:\claude_data\.dotnet\dotnet.exe test tests\QingLi.Core.Tests\QingLi.Core.Tests.csproj --filter Core_assembly_has_expected_identity
```

Expected: FAIL with actual version 0.1.1.0.

- [ ] **Step 2: Advance production version metadata**

Set `Version`, `AssemblyVersion`, and `FileVersion` in `Directory.Build.props` to `0.1.2`.

- [ ] **Step 3: Run the full test suite**

```powershell
E:\claude_data\.dotnet\dotnet.exe test QingLi.sln -c Release
```

Expected: all tests pass with zero failures.

- [ ] **Step 4: Build and validate the portable package**

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\package.ps1 -Configuration Release -Runtime win-x64 -DotNetPath E:\claude_data\.dotnet\dotnet.exe -PortableOnly
```

Expected: `artifacts\QingLi-0.1.2-win-x64-portable.zip` exists and archive verification succeeds.

- [ ] **Step 5: Inspect the published executable icon**

Extract the ZIP, inspect `QingLi.Windows.exe` and a generated desktop shortcut at 256, 32, and 16 pixels, and confirm Windows Explorer, taskbar, title bar, and notification area display the approved icon. If Explorer still shows the old image, rebuild the shortcut or refresh the Windows icon cache before judging the asset.

- [ ] **Step 6: Commit and publish**

```powershell
git add Directory.Build.props tests/QingLi.Core.Tests/SmokeTests.cs
git commit -m "release: prepare QingLi 0.1.2"
git push origin HEAD:main
git tag v0.1.2
git push origin v0.1.2
gh release create v0.1.2 artifacts\QingLi-0.1.2-win-x64-portable.zip --repo justin512512/QingLi --title "轻历 v0.1.2" --notes "新增统一的日历与月牙应用图标，覆盖快捷方式、任务栏、托盘和程序窗口。"
```

Expected: public v0.1.2 release contains the verified ZIP.
