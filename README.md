# Mif – Image sequence to video / GIF

A small C# WPF app for Windows that makes videos or animated GIFs from a sequence of images, with GIF-style controls.

I'm not a programmer, so this whole thing was vibe-coded using [Cursor](https://cursor.com/home).

## Features

- **Frame stack**: Add multiple images (PNG, JPG, BMP, GIF, TIFF), reorder with ↑/↓, remove or clear.
- **Frame delay**: Set delay per frame in **1/100 of a second** (e.g. `50` = 0.5 s).
- **Export as GIF**: Saves an animated GIF using the current frame order and delay.
- **Export as video**: Saves an MP4 via FFmpeg (same delay = frame duration).

## Build and run

```bash
dotnet restore
dotnet build
dotnet run
```

Or run `bin\Debug\net8.0-windows\Mif.exe` after building.

## Video export (FFmpeg)

Video export uses **FFmpeg**. Either:

1. Install [FFmpeg](https://ffmpeg.org/download.html) and add it to your PATH, or
2. Place `ffmpeg.exe` in the same folder as `Mif.exe`.

Without FFmpeg, video export will show an error; GIF export does not need FFmpeg.

## Requirements

- Windows
- .NET 8.0 (or SDK for building)

## The name

- Mif (said as "Myth") is a combination of MP4 and GIF.

## Preview

![Mif3.2](https://github.com/NickIsOnYT/Mif/blob/main/Logo%20thing/Screenshot%20Mif.png)
