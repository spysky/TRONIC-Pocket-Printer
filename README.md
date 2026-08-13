# TRONIC Pocket Printer

A standalone Windows (WPF) application to print images, PDFs and text notes on the
**TRONIC / Karsten International "Mini Pocket Printer" (model 5890, IAN 508705_2507)**
thermal printer over **Bluetooth Classic SPP** (a virtual COM port).

It is a Windows-native alternative to the official Android "Pocket Printer" app.

---

## Features

- **Open & print images** — `.jpg`, `.jpeg`, `.png`, `.bmp`, `.webp`
- **Open & print PDFs** — render each page and print current page or all pages
- **Notes** — a simple notepad that renders typed text and prints it
- **Print orientation** — Portrait or **Landscape** (uses the continuous roll length)
- **Live preview** — Original and monochrome *Print Preview* (384 px) with zoom
- **Image adjustments** — Brightness, Contrast, Threshold, Invert, Rotate, Fit Width
- **Dithering** — `Threshold` (default) or `Floyd–Steinberg` for photos
- **Trim white margins** — removes excessive white space from PDF pages
- **Automatic COM port detection** — finds the printer by its Bluetooth MAC in the
  PnP instance id (does not assume a fixed COM number)
- **Diagnostics window** — ports, connection test, test print, feed, last job stats
- **Settings** — persisted to `%LOCALAPPDATA%\TronicPocketPrinter\settings.json`
- **Logging** — `%LOCALAPPDATA%\TronicPocketPrinter\Logs`
- **Drag & drop** files, `Ctrl+O` to open, `Ctrl+P` to print
- Single physical print job at a time (serialized), COM port always closed on error

---

## Requirements

- **Windows 10/11 x64** with Bluetooth
- The printer paired in Windows as **"Mini Pocket Printer"** (Bluetooth **Classic**, not `_BLE`)
- No .NET installation required for the published self-contained build

---

## Getting started (end users)

1. Turn on the printer and pair it in **Windows ? Bluetooth & devices ? Add device ?
   Bluetooth ? "Mini Pocket Printer"**. Windows creates a *Standard Serial over
   Bluetooth link (COMx)* port.
2. Run `TRONIC Pocket Printer.exe`.
3. **Open** a file (or **Notes** for text) ? check the **Print Preview** ? **Print**.

> The COM number may differ per PC. The app auto-detects the port by the printer's
> MAC address; if detection fails, pick the port manually in **Settings ? Refresh Ports**.

---

## Build from source

```powershell
dotnet build "TRONIC Pocket Printer.sln" -c Debug
dotnet test  "Tests\TronicPocketPrinter.Tests.csproj"
```

### Publish a self-contained single-file executable (x64)

```powershell
dotnet publish "TRONIC Pocket Printer.csproj" -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false
```

Output:

```
bin\Release\net9.0-windows\win-x64\publish\TRONIC Pocket Printer.exe
```

The resulting `.exe` bundles the .NET runtime, WPF, SkiaSharp and PDFium — copy the
single file to any Windows x64 machine and run it (no installation needed).

---

## Project structure

```
TRONIC Pocket Printer/          WPF app (UI, windows, view logic)
  MainWindow / SettingsWindow / DiagnosticsWindow / NotesWindow
Core/                           Class library (TronicPocketPrinter.Core)
  Configuration/PrinterSettings.cs
  Imaging/RasterConverter.cs    GS v 0 raster packing (validated)
  Imaging/ImageProcessor.cs     resize, adjustments, threshold / Floyd-Steinberg
  Imaging/PdfRenderer.cs        PDFium rendering
  Imaging/TextRenderer.cs       text -> bitmap (Notes)
  Imaging/TestPatternGenerator.cs
  Printing/TronicPrinterProtocol.cs
  Printing/SerialPrinterTransport.cs
  Printing/PrinterService.cs
  Printing/PortDetector.cs
  Logging/FileLogger.cs
Tests/                          xUnit unit tests
```

### NuGet packages

| Package | Purpose |
| --- | --- |
| SkiaSharp | Image decoding/processing (incl. WebP), resizing |
| PDFtoImage | PDF rendering via PDFium |
| System.IO.Ports | Serial (Bluetooth SPP) communication |
| System.Management | COM port / MAC detection via WMI |
| xunit + Microsoft.NET.Test.Sdk | Unit tests |

---

## Printer protocol

Extracted from the official Android SDK
(`com.luckprinter.sdk_new ... MiniPocketPrinter ? DP_D1`) and **validated on real
hardware** (384×16 all-black test print). Print width is **384 dots** (48 bytes/line),
~203 dpi, 56 mm thermal roll.

| Step | Bytes |
| --- | --- |
| Enable printer | `10 FF F1 03` |
| Wakeup | `00` × 12 |
| Raster header (GS v 0) | `1D 76 30 00 xL xH yL yH` |
| Feed *n* dots | `1B 4A n` (default `1B 4A 50`) |
| Stop print job | `10 FF F1 45` |

Raster packing: 1 bit/pixel, 8 pixels/byte, MSB = left-most pixel, **black = 1**,
white = 0.

Job sequence: open COM ? Enable ? wait ~100 ms ? Wakeup ? wait ~100 ms ? raster ?
Feed ? Stop ? close COM. For multi-page PDFs the port opens once, feeds after each
page, and stops only at the end.

> ?? The protocol constants are confirmed against hardware — do not change them
> without a documented reason.

---

## Configuration (`settings.json`)

| Key | Default |
| --- | --- |
| `PrinterName` | `TRONIC Mini Pocket Printer` |
| `PortName` | `COM6` |
| `BaudRate` | `115200` |
| `PrintWidthPixels` | `384` |
| `Threshold` | `128` |
| `FeedDots` | `80` |
| `ChunkSize` | `1024` |
| `ChunkDelayMilliseconds` | `15` |
| `AutoDetectPort` | `true` |
| `DitherMode` | `Threshold` |

---

## Troubleshooting

- **"Could not open COMx" / "Bluetooth printer may be disconnected."** — make sure the
  printer is on and paired; try **Diagnostics ? Test Connection**.
- **"Printer not found."** — use **Settings ? Refresh Ports** and select the port
  manually, or enable `AutoDetectPort`.
- **Wrong orientation** — set **Orientation** to *Landscape* (available in the main
  window and in **Notes**).

---

## License

For personal/hobby use with the TRONIC Mini Pocket Printer. No affiliation with
TRONIC, Karsten International or the original SDK authors.
