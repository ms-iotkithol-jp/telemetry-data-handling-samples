# Storing Telemetry Data into Time Series Insight  
IoT Hub で受信した機器からの時系列データを、時系列データ保存用サービスの Time Series Insights で保存するサンプルを紹介する。  
作業は非常に簡単で、Time Series Insights を作成する際に、データソースとして Azure IoT Hub インスタンスを指定するだけでよい。  

![tsi event source](images/tsi-event-source.svg)  

IoT Hub は、サービス側のテレメトリーデータ受信エンドポイントで、Consumer Group を介して後段のサービスにデータを受け渡す。  
Time Series Insights インスタンスは以下の手順で作成していく。  
1. IoT Hub と同じリソースグループを選択する。  
1. <b>Environment Name</b> を入力し、IoT Hub と同じ <b>Location</b>(リージョン) を選択。<b>Tier</b> は <b>Gen2</b> を選択する。  
1. <b>Property name</b> は、IoT Hub から送られてくるデータの中のデバイスIDを示すプロパティ名、つまり、<b>'deviceid'</b> と入力する。  
1. <b>Storage account name</b> に、テレメトリーデータを格納する Blob Container 用の Storage Account のインスタンス名を入力する。  

一通り入力が終わったら、<b>'Next: Event Source >'</b> をクリックして、次に進む。  
※ <b>Property Name</b> で指定したプロパティ <b>deviceid</b> の値(IoT Hub のレジストリで IoT Device として登録されたデバイスの名前)は、Time Series Insights の Environment 上では、<b>Time Series ID</b> という名前で管理され、一度に複数のセンサー計測値（例えば、温度と湿度のセットや、3軸加速度、多種類のセンサー計測値のセット等）が送られてくる場合は、Time Series ID として束ねられる。

![tsi create 1](images/tsi-creating-1.svg)

次に、Time Series Insights の Event Source に IoT Hub インスタンスを指定する。  
1. <b>Source type</b> で <b>IoT Hub</b> を選択。  
1. <b>Name</b> に、'equipments' と入力。  
1. <b>IoT Hub name</b> で、シミュレーターアプリからデータを送っている IoT Hub インスタンスを選択。  
1. <b>IoT Hub access policy name</b> は、バックエンドサービスからのデータ受信用の接続なので、<b>service</b> を選択。  
1. <b>IoT Hub consumer group<b> は、予め IoT Hub 側で Time Series Insights 用の Consumer Group を作成済みならそれを選択、あるいは、未作成であれば、<b>New</b> をクリックして <b>tsi</b> と入力。  
1. <b>Timestamp</b> の <b>Property name</b> は、IoT Hub から送られてくるデータの中の計測時間を示すプロパティ名、つまり、<b>'timestamp'</b> と入力。  

一通り入力し終わったら、<b>Review + Create</b> をクリックした後、Time Series Insights インスタンスを作成する。  

![tsi create 2](images/tsi-creating-2.svg)

Time Series Insights インスタンスの作成が完了すると、シミュレーターアプリがデータを送信している場合は、自動的にデータを受信し、順次、Blob Container に、Parquet フォーマットで時系列データとして保管される。  

---

## モデル（階層）の定義  
Time Series Insights は、<b><u>プラント</u></b>に設置された<b><u>装置</u></b>という様な、現実世界の階層をモデル化できる。  

![tsi layered model](images/tsi-layered-model.svg)  

※ 図は、二階層のケースを表現しているが、プラント→フロアー→装置というような、3階層以上も表現可能である。  
※ Time Series Insights のモデルはあくまでも階層表現だけであり、生産工程における装置の使用順序等といった、より複雑な構成は表現できない。  

Time Series Insights では、このような階層を、<b>Hierachies</b> と言い、センサーデータを持つ最下層の要素（装置・機器・デバイス）を、<b>instance</b> という。  
モデルの定義は、
1. <b>instance</b> のセンサー計測値のデータ定義（<b>types</b>)  
1. <b>Hierachies</b> の定義  
1. <b>Instance</b> の定義  

の順番で行う。モデルの定義は、Time Series Insights Environment の Explorer の GUI を使って手作業でも行えるが、ここでは、JSONファイルをあらかじめ作成して登録していく方法を紹介する。    

### types の定義
このサンプルでは、<b>temperature</b> をデータとして送ってくるので、<b>types</b> の定義は以下の様な JSON となる。  
```json
{
  "put": [
    {
      "id": "D7DFE0A3-1062-48A4-8C81-89C1B1DE0E13",
      "name": "dtmi:embeddedgeorge:sample:thermostaticchamber;1",
      "description": "TSI sample",
      "variables": {
        "temperature": {
          "kind": "numeric",
          "value": {
            "tsx": "$event.[temperature].Double"
          },
          "aggregation": {
            "tsx": "avg($value)"
          }
        }
      }
    }
  ]
}
```
<b>id</b> は、既に定義されたデータ型（デフォルトで、<b>DefaultType</b>という名前が Time Series Insights Environment に定義されている）で使われている値とは異なる値を指定する。  
<b>name</b> は、ここでは、DTDL（Digital Twins Definition Language）の規定に従って、<b>thermostaticchamber</b> が送ってくるデータセットとしての名前を記述している。  
<b>variables</b> の中の、<b>temperature</b> は、Time Series Insights が認識するセンサーデータ項目名である。下位のプロパティの、<b>value.tsx</b>内で、IoT Hub から送られてくるテレメトリーデータの <b>temperature</b> プロパティで、かつ、値が数値で <b>Double</b> 型であることを示している。更に、<b>aggregation</b> で、<b>avg($value)</b> つまり、テレメトリーデータの <b>temperature</b> の平均値を表示するよう指定している。  
※ 送られてくるテレメトリーデータが複数のセンサー計測値を持っている場合は、<b>variables</b>の子要素として、<b>temperature</b> と同様な様式でそれぞれ追加していけばよい。  

JSON ファイルは以下の手順で Time Series Insights Environment の Explorer の GUI で登録を行う。  
1. 作成した Time Series Insights インスタンスを Azure Portal で開き、<b>Go to TSI Explorer</b> をクリックする。  
1. Explorer が表示されたら、<b>モデルアイコン</b>をクリックして、モデル定義の画面を表示する。  
1. <b>Types</b> をクリックして、データ型のページを開き、<b>Upload JSON</b> をクリックする。  
1. <b>Choose file</b> をクリックして、[models/tsi/types.json](models/tsi/types.json)を選択する。  
1. 内容が検証されて結果が表示されるので、<b>Upload</b> をクリックする。  

以上で、Time Series Insights Environment にデータ型が追加される。  

![tsi model define type](images/tsi-model-define-type.svg)

### Hierachies の定義  
次に、階層を定義する。このサンプルでは、<b>Plant</b> と <b>Equipment</b> の二階層を定義する。  
こちらも予め JSON ファイルを用意して登録する方法を紹介する。  
```json
{
  "put": [
    {
      "id": "00B8F0FA-230F-425D-AAA3-A83DCF39685C",
      "name": "PlantManagement",
      "source": {
        "instanceFieldNames": [
          "Plant",
          "Equipment"
        ]
      }
    }
  ]
}
```
<b>id</b> は、types と同様に一意の UUID を用いる。<b>name</b> は、定義する階層名で、このサンプルでは、<b>PlantManagement</b> としている。  
後は、<b>instanceFieldNames</b> の子要素として、階層の上から順に文字列をリストで定義すればよい。  
こちらも、types.json と同じような手順で登録する。  
1. モデル定義で、<b>Hierachies</b> を選択。  
1. <b>Upload JSON</b> をクリック。  
1. <b>Choose file</b> をクリックして、[models/tsi/hierachies.json](models/tsi/hierachies.json)を選択.
1. 内容が検証されて結果が表示されるので、<b>Upload</b> をクリック。  

以上で、階層情報が登録される。

![tsi model define hierachies](images/tsi-model-define-hierachies.svg)  

### Instance の定義  
Instance の定義は、登録したい Time Series ID のデータが蓄積されたもののみ定義できる。事前に登録しておくことはできない。  
IoT Hub を通じて、<b>thermostatic-chamber-1</b> からのデータが Time Series Insights に送信されている状態で、  
```json  
{
  "put": [
    {
      "timeSeriesId": [
        "thermostatic-chamber-1"
      ],
      "typeId": "D7DFE0A3-1062-48A4-8C81-89C1B1DE0E13",
      "name": "tsc-1",
      "description": "",
      "hierarchyIds": [
        "00B8F0FA-230F-425D-AAA3-A83DCF39685C"
      ],
      "instanceFields": {
        "Plant": "chiba-plant",
        "Equipment": "thermostatic-chamber-1"
      }
    }
  ]
}
```
<b>typeId</b> は、types.json で定義してあったデータ型の <b>id</b> を使用する。<b>hierachyIds は、hierachies.json で定義してあった <b>id</b> を指定する。  
階層は、ここでは、  
|Plant|Equipment|
|-|-|
|chiba-plant|thermostatic-chamber-1|

と便宜上定義してある。  
こちらも、types.json や hierachies.json と同様、JSON ファイルを以下の様に登録する。  

1. <b>Instanes</b> を選択。  
1. <b>Upload JSON</b> をクリック。  
1. <b>Choose file</b> をクリックして、[models/tsi/instance-tsc-1.json](models/tsi/instance-tsi-1.json)を選択.
1. 内容が検証されて結果が表示されるので、<b>Upload</b> をクリック。  

![tsi model define instance](images/tsi-model-define-instance.svg)  

instance の登録前に既に表示されていた、<b>thermostatic-chamber-1</b> の表示が変わっていることが確認できる。  
以上の作業を行った後、グラフ表示画面で、受信したテレメトリーデータの表示を行うと以下の様になる。  

![tsi env modeled graph](images/tsi-env-modeled-graph.svg)  

中々に、JSON ファイルの定義内容、IoT Hub レジストリとの関係など、判り難いので、関係を図に示しておく。  

![iothub tsi models relation](images/iothub-tsi-models-relation.svg)  

以上で、構成を伴うテレメトリーデータの保存方法を紹介した。一旦テレメトリーデータを Time Series Insights で Blob Container に Parquet フォーマットで保存してしまえば、https://docs.microsoft.com/rest/api/time-series-insights/ で解説されている REST API で、特定の装置の特定の日時の範囲の時系列データセットを取り出すことができる。  

---
## tsiclient sample - Time Series Insights JavaScript SDK   
また、 https://tsiclientsample.azurewebsites.net/ のサイトから、多数のグラフ表示のサンプルが紹介されているので、参考にして頂きたい。  
このサンプルを Windows PC 上で試す場合、以下の手順に従って環境を整える。  
適当なフォルダーを用意して、そのディレクトリで以下を実行する。  
```sh
git clone https://github.com/microsoft/tsiclient.git
```
このリポジトリの、pages/examples/index.html を開けば、サンプルセットが表示されるが、実際に動かすには、tsiclient JavaSDK client のインストールが必要であり、更に、npm でのインストールの際、python 2.x が必要らしいので、[Download the latest version for Windows](https://www.python.org/downloads/) から、最新の Python 2.x （2021/10/5時点では、Python 2.7.17)の MSI Installer(使っている PC の CPU Arch に合ったものを選択)をダウンロードしてインストールする。  
tsiclient のインストール中、どうやら、python2 というコマンド名で実行しようとするので、python 2.x がインストールされたフォルダー（Python 2.7.xなら、C:\Python27)に、以下の内容のバッチファイルをテキストエディタで、
```sh
C:\Python27\python.exe
```
という内容で、python2.cmd という名前で作成し、環境変数設定で Path に、C:\Python27 を追加してから、  
```sh
cd tsiclient
npm install tsiclient
```
と、Github のリポジトリをクローンしたフォルダーの直下で、SDK をインストールする。  
次いで、[pages/examples/boilerplate/head.js](https://github.com/microsoft/tsiclient/blob/master/pages/examples/boilerplate/head.js) の SDK 参照が微妙？（もしかすると、著者のSDKのインストール方法が不適切なのかも） なので、このファイルを開いて、
```js
window.addEventListener('DOMContentLoaded', function(){
    var sdkJs = document.createElement('script');
    // sdkJs.src = 'https://unpkg.com/tsiclient@1.3.0/tsiclient.js';
    // sdkJs.src = '../../../dist/tsiclient.js';  // for local testing
    sdkJs.src = '../../../node_modules/tsiclient/tsiclient.js';

    var promiseJs = document.createElement('script');
    promiseJs.src = 'https://cdnjs.cloudflare.com/ajax/libs/bluebird/3.3.4/bluebird.min.js';

    var sdkCss = document.createElement('link');
    sdkCss.rel = 'stylesheet';
    sdkCss.type = 'text/css';
    // sdkCss.href = 'https://unpkg.com/tsiclient@1.3.0/tsiclient.css';
    // sdkCss.href = '../../../dist/tsiclient.css';  // for local testing
    sdkCss.href = '../../../node_modules/tsiclient/tsiclient.css';  // for local testing
```
の様に、sdkJs.src と sdkCss.href への設定を変更する。  
※ https://unpkg.com/tsiclient@1.3.0/ を使ってもよい  

### pages/examples/noauth  
Time Series Insights Client SDK のグラフ描画、コンテキストメニュー、表示範囲などのサンプル。  
|HTML File|サンプル内容|
|-|-|
|[basiccharts.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/basiccharts.html)|グラフ描画ライブラリのサンプル。折れ線グラフ、棒グラフ、円グラフ、ヒートマップ、スキャッタープロットの基本的な使い方|
|[multipleseriestypes.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/multipleseriestypes.html) |カテゴリ値の表示や、数値グラフとカテゴリ値の組合せ方法|
|[contextmenu.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/contextmenu.html)|グラフにコンテキストメニューを追加する方法|
|[availabilityAndLinechart.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/availabilityAndLinechart.html)|表示範囲(availability)の指定と連動するグラフの表示方法|
|[chartOptions.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/chartOptions.html)|グラフ表示の設定変更方法|
|[dateTimeRangePicker.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/dateTimeRangePicker.html)|日時範囲指定方法|
|[swimlaneOptions.html](https://github.com/Microsoft/tsiclient/blob/master/pages/examples/noauth/swimlaneOptions.html)|グラフのスイムレーンの表示場所変更、ラベルの変更など


他にも、https://github.com/Azure-Samples/Azure-Time-Series-Insights で、Backed サービスのサンプルが提供されている。  

### pages/examples/withplatform  
Time Series Insight Client SDK による、Time Series Insights Environment からのデータ取得等のサンプル。デモ用にマイクロソフトが公開している Time Series Insights Environment のデータを用いて例示している。  
各自の Time Series Insights Environment へのアクセスには、Managed ID を使うか、Active Directory で Application を作成し適切な権限を付与する必要がある。  
詳細は、https://docs.microsoft.com/ja-jp/azure/time-series-insights/time-series-insights-authentication-and-authorization に記載されている。  
以下に、Application の場合の設定について、ドキュメントが判り難いので補足する。  
Azure Portal で、  
1. 左上の横三本線アイコンをクリックして、<b>Azure Active Directory</b> を選択  
2. <b>App Registrations</b> を選択  
3. <b>+ New Registration</b> をクリックして Application を作成  
で、アプリを作成した後の手順を図示する。  
![set ad application tsi api permission](images/set-ad-tsi-api-permission.svg)

※ Time Series Insights の API を探すとき、"Azure Time ..." で探すこと。  
ドキュメントの"[アプリケーションの登録](https://docs.microsoft.com/ja-jp/azure/time-series-insights/time-series-insights-authentication-and-authorization#application-registration)" を注意深く読むと、最後に、"[資格情報の追加](https://docs.microsoft.com/ja-jp/azure/active-directory/develop/quickstart-register-app#add-credentials)"というリンクがあり、アプリの方式に従って、やっておかなければならない。  
ドキュメントの"[ステップ 2: アクセス権を付与する](https://docs.microsoft.com/ja-jp/azure/time-series-insights/time-series-insights-authentication-and-authorization#step-2-grant-access) では、5つの設定情報が必要である。  
```sh
az tsi access-policy create --name "ap1" --environment-name "env1" --description "some description" --principal-object-id "aGuid" --roles Reader Contributor --resource-group "rg1"
```
|option|指定する値|
|-|-|
|name|適当な判りやすい文字列|
|environment-name|アクセスしたい Time Series Insights の名前|
|description|適当な判りやすい文字列|
|principal-object-id|上図の一番下の <b><u>Object ID</u></b>|
|resource-group|Time Series Insights が属しているリソースグループの名前|

の様に各オプションを指定する。  
※ <b>az tsi</b> は、Azure CLI のバージョンが古いと自動的にはインストールされないようなので、最新版にアップグレードすること。自動インストールされない場合は、  
```sh
az upgrade
az extension update --name timeseriesinsights
```
をやってみるとよい。  

Time Series Insights Environment へのアクセスは、https://github.com/microsoft/tsiclient/blob/master/docs/Server.md を参考にするとよい。  
Active Directory へのサインイン、Token の取得は、https://docs.microsoft.com/ja-jp/azure/active-directory/develop/msal-overview で紹介されている、Microsoft Authentication Library(MSAL) が良いらいしい。  
例として、ローカル環境でのテスト用の HTML ファイルを紹介しておく。Active Directory の Application に適切な Authentication を設定し、Client IDや、Tenant Id、TSI の FQDN を設定して試してみていただきたい。
```html
<head>
    <meta charset="UTF-8">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
  
    <script 
      type="text/javascript" 
      src="https://alcdn.msauth.net/browser/2.14.2/js/msal-browser.min.js">
    </script>
    <script
        type="text/javascript"
        src="https://unpkg.com/tsiclient@1.3.0/tsiclient.js">
    </script>
    <link rel='stylesheet' type='text/css' href='https://unpkg.com/tsiclient@1.3.0/tsiclient.css'/>
  </head>
  
  <div>
    <button id="loginButton">Login</button>
    <button id="logoutButton" style="visibility: hidden;">Logout</button>
    <button id="tokenButton" style="visibility: hidden;">Get Token</button>
  </div>
  
  <body>
    <script>
      const loginButton = document.getElementById("loginButton");
      const logoutButton = document.getElementById("logoutButton");
      const tokenButton = document.getElementById("tokenButton");
  
      const pca = new msal.PublicClientApplication({
          auth: {
              clientId: "<- your application client id ->",
              authority: "https://login.microsoftonline.com/<- your tenant id ->",
              redirectUri: "http://localhost:5500",
          },
          cache: {
              cacheLocation: "sessionStorage"
          },
          system: {
              loggerOptions: {
                  loggerCallback(loglevel, message, containsPii) {
                      console.log(message);
                  },
                  piiLoggingEnabled: false,
                  logLevel: msal.LogLevel.Verbose,
              }
          }
      });
  
      loginButton.addEventListener('click', () => {
          pca.loginPopup().then((response) => {
              console.log(`Hello ${response.account.username}!`);
  
              loginButton.style.visibility = "hidden";
              logoutButton.style.visibility = "visible";
              tokenButton.style.visibility = "visible";
          })
      });
  
      logoutButton.addEventListener('click', () => {
          pca.logoutPopup().then((response) => {
              window.location.reload();
          })
      });
  
      tokenButton.addEventListener('click', () => {
          pca.acquireTokenPopup({
              // scopes: ["User.Read"]
              scopes: ["https://api.timeseries.azure.com//.default"]
          }).then((response) => {
              console.log(response);
              var tsiClient = new TsiClient();
                              // create aggregate expressions, they are S1/S2 SKU query objects
                              var aggregateExpressions = [];
                var startDate = new Date('2017-04-14T13:00:00Z');
                var endDate = new Date(startDate.valueOf() + 1000*60*60*1);
                aggregateExpressions.push(new tsiClient.ux.AggregateExpression({predicateString: "Factory = 'Factory1'"}, {property: 'Pressure', type: "Double"}, ['avg', 'min', 'max'],
                    { from: startDate, to: endDate, bucketSize: '2m' }, {property: 'Station', type: 'String'}, 'pink', 'Factory1Pressure'));
                tsiClient.server.getAggregates(response.accessToken,
                    '<- your data access FQDN ->.env.timeseries.azure.com',
                    aggregateExpressions.map(function(ae){return ae.toTsx()})).then(function(result){
                        console.log(result);
                    });


          })
      });
    </script>
  </body>
  
  </html>
  ```

## C# による Time Series Insights Environment へのアクセス  
https://github.com/Azure-Samples/Azure-Time-Series-Insights/tree/master/gen2-sample/csharp-tsi-gen2-sample でサンプルが公開されている。  
動かすのに色々と手間がかかるが、DataPlaneClientSampleApp のビルドが完了したら、Power Shell上で exe ファイルを実行すると動作する。  
