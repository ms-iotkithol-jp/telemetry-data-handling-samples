# How to setup UpdateTwinGraphByIoTDevice  
VS Code、もしくは、Visual Studio で、[UpdateTwinGraphByIoTDevice.csproj](./UpdateTwinGraphByIoTDevice.csproj) を開き、
[local.settings.json](./local.settings.json) の、<b>Variables</b> の、  
- AzureWebJobsStorage  
- calculated_telemetry_listen_EVENTHUB  
- ADT_SERVICE_URL  

の各文字列内の &lt;- -&gt;で囲まれた部分を、各自の Azure Resource の設定に合わせて編集する。  

## ローカルデバッグ時  
[UpdateTwinGraphByIoTDevice.cs](UpdateTwinGraphByIoTDevice.cs) の一行目の  
```cs
#define LOCAL_DEBUG
```
を有効にする。  
これにより、Azure Digital Twins へのアクセス認証が、  
```cs
                    var credential = new DefaultAzureCredential();
```
を使う設定になる。  


## Azure へのデプロイ時  
[UpdateTwinGraphByIoTDevice.cs](UpdateTwinGraphByIoTDevice.cs) の一行目を  
```cs
// #define LOCAL_DEBUG
```
の様に無効にする。

これにより、Azure Digital Twins へのアクセス認証が、  
```cs
                    var credential = new ManagedIdentityCredential("https://digitaltwins.azure.net");
```
を使う設定になる。  
