# CLAUDE.md

This file provides guidance to coding agents when working with code in this repository.

## Project Overview

Animate! is a WPF application that allows users to animate sketches from image files. The application provides tools to select animation frames, adjust origins, and export sprites with transparent backgrounds.

## Build and Development Commands

**Build the application:**
```bash
dotnet build
```

**Run the application:**
```bash
dotnet run
```

**Build release version:**
```bash
dotnet build -c Release
```

**Create self-contained executable:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true
```

**Tag and release:**
```bash
git tag v?.?.?
git push origin v?.?.? # or git push --tags
```

## Architecture Overview

### Core Components

- **MainWindow.xaml/.cs**: Main application window containing the primary UI and logic
  - Handles image loading, frame selection, animation playback
  - Manages sprite sheet display with zoom/pan functionality
  - Contains image processing for background removal and frame adjustment

- **Frame class**: Represents individual animation frames with position, size, origin, and bitmap data

- **Animation.cs**: Basic animation class (appears to be legacy/unused in current implementation)

- **FileChangeNotification.cs**: Real-time file monitoring using Windows API
  - Watches for changes to loaded images and triggers automatic reload
  - Uses Win32 API (`FindFirstChangeNotification`, `WaitForSingleObject`)

- **ExportWindow.xaml/.cs**: Dialog for configuring sprite export settings

### Key Technologies

- **WPF (.NET 9.0)**: Main UI framework
- **Emgu.CV**: Computer vision library for image processing
  - Background removal using color detection
  - Contour detection for auto-adjusting frame bounds
  - Center of mass calculation for origin adjustment
- **System.Drawing**: Image manipulation for export functionality

### Image Processing Pipeline

1. **Load**: Images loaded as BitmapImage with transparency detection
2. **Background Removal**: Automatic background removal for non-transparent images using color sampling and distance calculation
3. **Frame Detection**: Manual frame selection via mouse interaction
4. **Auto-Adjustment**: Automatic frame boundary and origin adjustment using OpenCV contour detection
5. **Export**: Sprite export with configurable dimensions and centering

### File Organization

- Main application files are in the root `Animate/` directory
- Resources include `sprites.bmp` (default animation) and `Icon.ico`
- `Properties/` contains launch settings and publish profiles
- Support files include various converter classes for UI binding

### Real-time Features

- **File Monitoring**: Automatic detection and reloading of image changes
- **Animation Playback**: Timer-based frame cycling with configurable intervals
- **Live Preview**: Real-time updates when editing source images

### UI Features

- **Zoom/Pan**: Mouse wheel zoom and middle-click panning
- **Frame Management**: Drag-and-drop reordering of animation frames  
- **Origin Visualization**: Toggle display of frame boundaries and origins
- **Windows 11 Theme**: Automatic light/dark theme support

## Development Notes

- The application supports BMP, JPG, and PNG files
- Command line argument support for loading images on startup
- Temporary folder usage for sprite exports with automatic cleanup
- Thread-safe file monitoring with proper cancellation handling