# IIS in-process: response truncated after first flush when a System.Web module subscribes to PreSendRequestContent

An ASP.NET Core app hosted in-process under IIS returns `200 OK` with only the first flushed chunk of its body when a .NET Framework `IHttpModule` with a no-op `PreSendRequestContent` handler is in the pipeline and the app pool's .NET CLR version is `v4.0`.

## Contents

| Path | What it is |
|---|---|
| `App/Program.cs` | Minimal API. `GET /` writes `first`, flushes, writes `second`, flushes |
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

# 3. Request the page, printing the body and then the status code
curl.exe -s -w "%{http_code}`n" http://localhost:8086/
```

**Expected:**

```
first
second
200
```

**Actual,** on every request:

```
first
200
```

The Application event log gets one entry per request.

```
Unexpected exception in IISHttpServer.OnAsyncCompletion.
System.NullReferenceException: Object reference not set to an instance of an object.
   at Microsoft.AspNetCore.Server.IIS.Core.IO.AsyncIOEngine.NotifyCompletion(Int32 hr, Int32 bytes)
   at Microsoft.AspNetCore.Server.IIS.Core.IISHttpServer.OnAsyncCompletion(IntPtr pvManagedHttpContext, Int32 hr, Int32 bytes)
```

## Controls

Each of these alone makes the response complete (`first`, `second`, `200`). If this is a new PowerShell window, define `$appcmd` again first.

```powershell
$appcmd = "$env:windir\System32\inetsrv\appcmd.exe"
```

**a) App pool set to "No Managed Code".** The module stays registered in `web.config`.

```powershell
& $appcmd set apppool PreSendRepro /managedRuntimeVersion:""
& $appcmd recycle apppool PreSendRepro
curl.exe -s -w "%{http_code}`n" http://localhost:8086/
```

Restore the pool afterwards with `& $appcmd set apppool PreSendRepro /managedRuntimeVersion:v4.0`.

**b) Module subscribes to `EndRequest` instead.** The module is rebuilt with `EndRequest` in place of `PreSendRequestContent`, then reverted.

```powershell
& $appcmd set apppool PreSendRepro /managedRuntimeVersion:v4.0
& $appcmd stop apppool PreSendRepro      # releases the lock on bin\PreSendModule.dll
(Get-Content PreSendModule\Module.cs) -replace 'app\.PreSendRequestContent', 'app.EndRequest' | Set-Content PreSendModule\Module.cs
dotnet build PreSendModule -c Release -o C:\inetpub\PreSendRepro\bin
& $appcmd start apppool PreSendRepro
curl.exe -s -w "%{http_code}`n" http://localhost:8086/
git checkout PreSendModule/Module.cs
```

## Cleanup

```powershell
& $appcmd delete site PreSendRepro
& $appcmd delete apppool PreSendRepro
Remove-Item C:\inetpub\PreSendRepro -Recurse
```
