#define UNICODE
#define _UNICODE
#include <windows.h>
#include <shellapi.h>
#include <string>
#include <vector>
static std::wstring target; static HWND lastHost=nullptr; static HANDLE stopEvent=nullptr;
static bool codex(HWND w) {
    if(!w||!IsWindowVisible(w)||IsIconic(w))return false;
    DWORD pid=0;GetWindowThreadProcessId(w,&pid);HANDLE p=OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,FALSE,pid);if(!p)return false;
    wchar_t b[32768];DWORD n=32768;bool ok=QueryFullProcessImageName(p,0,b,&n)!=0;CloseHandle(p);if(!ok)return false;
    std::wstring s(b);for(auto& c:s)c=towlower(c);
    return s.size()>=10 && (s.substr(s.size()-10)==L"\\codex.exe" || (s.find(L"\\windowsapps\\openai.codex_")!=std::wstring::npos && s.substr(s.size()-12)==L"\\chatgpt.exe"));
}
static bool launch(const std::wstring& file,const std::wstring& args){std::wstring command=L"\""+file+L"\" "+args;STARTUPINFOW si={sizeof(si)};PROCESS_INFORMATION pi={};bool ok=CreateProcessW(file.c_str(),&command[0],nullptr,nullptr,FALSE,CREATE_NO_WINDOW,nullptr,nullptr,&si,&pi)!=0;if(ok){CloseHandle(pi.hThread);CloseHandle(pi.hProcess);}return ok;}
static bool replace(const std::wstring& source,const std::wstring& destination){auto next=destination+L".next";if(!CopyFileW(source.c_str(),next.c_str(),FALSE))return false;return MoveFileExW(next.c_str(),destination.c_str(),MOVEFILE_REPLACE_EXISTING|MOVEFILE_WRITE_THROUGH)!=0;}
static void check(){HWND host=GetForegroundWindow();if(!codex(host)) {lastHost=nullptr;return;}if(host==lastHost)return;lastHost=host;HANDLE m=OpenMutex(SYNCHRONIZE,FALSE,L"Local\\CodexUsageCardLocal");if(m){CloseHandle(m);return;}launch(target,L"--follow");}
static void CALLBACK event(HWINEVENTHOOK,DWORD,HWND,LONG,LONG,DWORD,DWORD){check();}
int WINAPI wWinMain(HINSTANCE,HINSTANCE,LPWSTR,int){int count=0;auto argv=CommandLineToArgvW(GetCommandLineW(),&count);if(!argv)return 2;
    std::vector<std::wstring> a;for(int i=0;i<count;i++)a.push_back(argv[i]);LocalFree(argv);
    if(count>=6&&a[1]==L"--apply"){
        // Launched from a verified staging folder. Do not touch configuration/assets.
        HANDLE p=OpenProcess(SYNCHRONIZE,FALSE,wcstoul(a[4].c_str(),nullptr,10));if(p){DWORD wait=WaitForSingleObject(p,30000);CloseHandle(p);if(wait!=WAIT_OBJECT_0)return 3;}
        auto stop=OpenEvent(EVENT_MODIFY_STATE,FALSE,L"Local\\CodexRailWatcherStop");if(stop){SetEvent(stop);CloseHandle(stop);Sleep(500);}
        std::wstring exe=a[3],backup=exe+L".previous";
        if(!CopyFileW(exe.c_str(),backup.c_str(),FALSE))return 4;
        if(!replace(a[2],exe)){launch(exe,L"");return 5;}
        std::wstring folder=exe.substr(0,exe.find_last_of(L"\\/"));
        std::wstring helper=folder+L"\\CodexRail.Watcher.exe";
        if(!replace(a[0],helper)){replace(backup,exe);launch(exe,L"");return 6;}
        if(!launch(exe,a[5]==L"follow"?L"--follow":L"")){replace(backup,exe);launch(exe,L"");return 7;}
        if(count>=7&&a[6]==L"watch")launch(helper,L"--watch \""+exe+L"\"");return 0;
    }
    if(count<3||a[1]!=L"--watch")return 2; target=a[2];if(GetFileAttributesW(target.c_str())==INVALID_FILE_ATTRIBUTES)return 2;
    HANDLE mutex=CreateMutexW(nullptr,TRUE,L"Local\\CodexRailWatcher");if(GetLastError()==ERROR_ALREADY_EXISTS){CloseHandle(mutex);return 0;}
    stopEvent=CreateEventW(nullptr,TRUE,FALSE,L"Local\\CodexRailWatcherStop");
    ResetEvent(stopEvent);
    HWINEVENTHOOK hook=SetWinEventHook(EVENT_SYSTEM_FOREGROUND,EVENT_SYSTEM_FOREGROUND,nullptr,event,0,0,WINEVENT_OUTOFCONTEXT|WINEVENT_SKIPOWNPROCESS);
    check();SetTimer(nullptr,1,5000,nullptr);MSG msg;
    while(WaitForSingleObject(stopEvent,0)!=WAIT_OBJECT_0){auto wait=MsgWaitForMultipleObjects(1,&stopEvent,FALSE,INFINITE,QS_ALLINPUT);if(wait==WAIT_OBJECT_0)break;while(PeekMessageW(&msg,nullptr,0,0,PM_REMOVE)){if(msg.message==WM_TIMER)check();TranslateMessage(&msg);DispatchMessageW(&msg);}}
    if(hook)UnhookWinEvent(hook);KillTimer(nullptr,1);CloseHandle(stopEvent);CloseHandle(mutex);return 0;
}
