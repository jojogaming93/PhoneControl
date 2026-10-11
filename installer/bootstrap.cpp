// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

// Self-contained Win32 bootstrap: no .NET, PowerShell, winget or admin rights.
// Inno's documented /SILENT mode shows progress without requiring wizard clicks.
#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shellapi.h>
#include <objbase.h>
#include <string>
#include <vector>

static std::wstring Quote(const std::wstring& value) {
    std::wstring result = L"\""; size_t slashes = 0;
    for (wchar_t ch : value) {
        if (ch == L'\\') { ++slashes; continue; }
        result.append(slashes * (ch == L'"' ? 2 : 1), L'\\'); slashes = 0;
        if (ch == L'"') result += L'\\';
        result += ch;
    }
    result.append(slashes * 2, L'\\'); return result + L'"';
}

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
    std::wstring directory, payload; HANDLE file = INVALID_HANDLE_VALUE;
    int result = 1; bool directoryCreated = false;
    try {
        int count = 0; LPWSTR* arguments = CommandLineToArgvW(GetCommandLineW(), &count);
        if (!arguments) throw L"Windows could not read the installer command line.";
        std::vector<std::wstring> forwarded;
        bool silent = false;
        for (int i = 1; i < count; ++i) {
            std::wstring arg = arguments[i];
            if (_wcsicmp(arg.c_str(), L"/SILENT") == 0 || _wcsicmp(arg.c_str(), L"/VERYSILENT") == 0) silent = true;
            forwarded.push_back(arg);
        }
        LocalFree(arguments);
        std::vector<wchar_t> temp(32768);
        DWORD length = GetTempPathW(static_cast<DWORD>(temp.size()), temp.data());
        if (!length || length >= temp.size()) throw L"Windows could not locate a temporary folder.";
        GUID guid{}; if (FAILED(CoCreateGuid(&guid))) throw L"Windows could not create an installer identifier.";
        wchar_t identifier[40]; StringFromGUID2(guid, identifier, 40);
        directory = std::wstring(temp.data()) + L"PhoneControl-" + identifier;
        if (!CreateDirectoryW(directory.c_str(), nullptr)) throw L"Windows could not create the installer temporary folder.";
        directoryCreated = true;
        payload = directory + L"\\PhoneControl-Payload.exe";
        HRSRC resource = FindResourceW(instance, MAKEINTRESOURCEW(101), RT_RCDATA);
        HGLOBAL loaded = resource ? LoadResource(instance, resource) : nullptr;
        void* data = loaded ? LockResource(loaded) : nullptr;
        DWORD size = resource ? SizeofResource(instance, resource) : 0;
        if (!data || !size) throw L"The installer download is incomplete. Download PhoneControl again.";
        file = CreateFileW(payload.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
        DWORD written = 0;
        if (file == INVALID_HANDLE_VALUE || !WriteFile(file, data, size, &written, nullptr) || written != size)
            throw L"Windows could not unpack PhoneControl. Check available disk space.";
        CloseHandle(file); file = INVALID_HANDLE_VALUE;
        std::wstring command = Quote(payload) + L" /SILENT /NORESTART";
        if (!silent) command += L" /AUTOLAUNCH";
        for (const auto& arg : forwarded) command += L" " + Quote(arg);
        STARTUPINFOW startup{}; startup.cb = sizeof(startup); PROCESS_INFORMATION process{};
        if (!CreateProcessW(payload.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, directory.c_str(), &startup, &process))
            throw L"Windows could not start PhoneControl setup.";
        CloseHandle(process.hThread);
        DWORD wait = WaitForSingleObject(process.hProcess, INFINITE), exitCode = 1;
        if (wait == WAIT_OBJECT_0) GetExitCodeProcess(process.hProcess, &exitCode);
        CloseHandle(process.hProcess); result = static_cast<int>(exitCode);
    } catch (const wchar_t* message) {
        MessageBoxW(nullptr, message, L"PhoneControl installation needs attention", MB_OK | MB_ICONERROR);
    } catch (...) {
        MessageBoxW(nullptr, L"PhoneControl setup could not finish. Download the installer again or contact support.", L"PhoneControl", MB_OK | MB_ICONERROR);
    }
    if (file != INVALID_HANDLE_VALUE) CloseHandle(file);
    if (!payload.empty()) DeleteFileW(payload.c_str());
    if (directoryCreated) RemoveDirectoryW(directory.c_str());
    return result;
}
