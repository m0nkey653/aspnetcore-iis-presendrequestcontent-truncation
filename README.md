# IIS in-process: response truncated after first flush when a System.Web module subscribes to PreSendRequestContent

An ASP.NET Core app hosted in-process under IIS returns `200 OK` with only the first flushed chunk of its body when a .NET Framework `IHttpModule` with a no-op `PreSendRequestContent` handler is in the pipeline and the app pool's .NET CLR version is `v4.0`.

## Contents

| Path | What it is |
|---|---|
| `App/Program.cs` | Minimal API. `GET /` writes 64 KB as 16 x 4 KB chunks with `FlushAsync()` after each |
| `App/web.config` | Standard in-process config, plus one `<modules>` entry for the module below |
| `PreSendModule/Module.cs` | .NET Framework 4.8 `IHttpModule` whose only code is `app.PreSendRequestContent += (s, e) => { };` |

## Prerequisites

- Windows with IIS, including the ASP.NET 4.8 feature (`IIS-ASPNET45`)
- .NET 10 SDK and the .NET 10 Hosting Bundle

## Steps

From an elevated PowerShell prompt in the repository root:

```powershell
# 1. Publish the app, and build the module into the site's bin folder
dotnet publish App -c Release -o C:\inetpub\PreSendRepro
dotnet build PreSendModule -c Release -o C:\inetpub\PreSendRepro\bin

# 2. App pool with .NET CLR v4.0 (the IIS default) and a site on port 8086
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
& $appcmd add apppool /name:PreSendRepro /managedRuntimeVersion:v4.0 /managedPipelineMode:Integrated
& $appcmd add site /name:PreSendRepro /bindings:"http/*:8086:" /physicalPath:C:\inetpub\PreSendRepro
& $appcmd set app "PreSendRepro/" /applicationPool:PreSendRepro

# 3. Request the 64 KB response
curl.exe -s -o NUL -w "%{http_code} %{size_download}`n" http://localhost:8086/
```

**Expected:** `200 65536`

**Actual:** `200 4096` on every request. The Application event log gets one entry per request.

```
Unexpected exception in IISHttpServer.OnAsyncCompletion.
System.NullReferenceException: Object reference not set to an instance of an object.
   at Microsoft.AspNetCore.Server.IIS.Core.IO.AsyncIOEngine.NotifyCompletion(Int32 hr, Int32 bytes)
   at Microsoft.AspNetCore.Server.IIS.Core.IISHttpServer.OnAsyncCompletion(IntPtr pvManagedHttpContext, Int32 hr, Int32 bytes)
```

## Controls

Each of these alone makes the response complete (`200 65536`):

```powershell
# a) App pool set to "No Managed Code"
& $appcmd set apppool PreSendRepro /managedRuntimeVersion:""
& $appcmd recycle apppool PreSendRepro

# b) Or, with the pool back on v4.0, change the module to subscribe to EndRequest instead of
#    PreSendRequestContent (edit Module.cs, rebuild step 1's second command, recycle the pool)
```

## Cleanup

```powershell
& $appcmd delete site PreSendRepro
& $appcmd delete apppool PreSendRepro
Remove-Item C:\inetpub\PreSendRepro -Recurse
```
