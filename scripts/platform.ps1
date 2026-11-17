function Get-NativeRuntime {
    $architecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture
    if ($IsWindows -and $architecture -eq 'X64') { return 'win-x64' }
    if ($IsLinux -and $architecture -eq 'X64') { return 'linux-x64' }
    if ($IsMacOS -and $architecture -eq 'Arm64') { return 'osx-arm64' }
    throw 'Supported native build hosts: Windows x64, Linux x64 and macOS arm64.'
}
