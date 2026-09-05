# BootScope
 A lightweight Windows startup diagnostics app that monitors CPU, RAM, disk, and network usage, identifies resource-heavy processes, and provides safe recommendations for managing non-essential applications.

## What it does

BootScope is a Windows desktop application (C# / .NET 8 / WPF) designed to launch automatically
after you log into Windows and help you understand what is causing high CPU, RAM, disk, or
network usage right after startup. It shows a live, sortable, filterable table of running
processes with CPU/RAM/disk usage, publisher information, and a safety classification, plus
plain-language recommendations for each process.

## Safety principles

BootScope is diagnostic-only by default:

- It **never** automatically terminates a process, disables a Windows service, or changes
  system configuration.
- It **always requires explicit user confirmation** before stopping a process, and shows a
  strong warning before stopping anything classified as a critical Windows process (e.g.
  `System`, `Registry`, `smss.exe`, `csrss.exe`, `wininit.exe`, `services.exe`, `lsass.exe`,
  `svchost.exe`, `dwm.exe`, `explorer.exe`).
- It runs with **normal user permissions** by default and does not request administrator
  elevation at launch.
- Every process is labelled as a **Critical Windows process**, **Windows background process**,
  **Third-party application**, **Startup application**, or **Unknown process**, together with a
  recommendation (*Safe to close*, *Close only if unused*, *Do not close*, *Needs investigation*).

## Project structure

```
BootScope/
  Models/        Plain data models (ProcessInfo, AppSettings, SystemUsageSnapshot, ...)
  ViewModels/     MVVM view models (MainViewModel, SettingsViewModel)
  Views/          WPF windows (MainWindow, SettingsWindow)
  Services/       Process monitoring, system usage, startup detection, safety classification,
                  settings persistence, session logging, and process actions
  Helpers/        MVVM base classes (ObservableObject, RelayCommand) and Win32 P/Invoke helpers
  Resources/      Shared WPF styles for the dashboard UI
  App.xaml(.cs)   Application entry point / composition root
```

## Building

Requires the .NET 8 SDK (Windows Desktop workload) and Windows to run:

```
dotnet build BootScope.slnx
```
