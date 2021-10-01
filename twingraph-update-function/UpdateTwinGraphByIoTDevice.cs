#define LOCAL_DEBUG
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EG.ADTSamples
{
    public static class UpdateTwinGraphByIoTDevice
    {
        static DigitalTwinsClient twinsClient;
        static readonly HttpClient httpClient = new HttpClient();
        static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        [Function("UpdateTwinGraphByIoTDevice")]
        public static async Task Run([EventHubTrigger("calculated_telemetry", Connection = "calculated_telemetry_listen_EVENTHUB")] string[] input, FunctionContext context)
        {
            var logger = context.GetLogger("UpdateTwinGraphByIoTDevice");
            logger.LogInformation($"First Event Hubs triggered message: {input[0]}");
            var exceptions = new List<Exception>();
            if (twinsClient == null)
            {
                try
                {
                    if (string.IsNullOrEmpty(adtInstanceUrl)) logger.LogInformation("Application setting \"ADT_SERVICE_URL\" not set.");
                    else logger.LogInformation($"Got {adtInstanceUrl}");
#if LOCAL_DEBUG
                    var credential = new DefaultAzureCredential();
#else
                    var credential = new ManagedIdentityCredential("https://digitaltwins.azure.net");
#endif
                    twinsClient = new DigitalTwinsClient(
                        new Uri(adtInstanceUrl),
                        credential,
                        new DigitalTwinsClientOptions
                        {
                            Transport = new Azure.Core.Pipeline.HttpClientTransport(httpClient)
                        }
                    );
                    logger.LogInformation($"ADT service client connection created - {adtInstanceUrl}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger.LogWarning($"ADT Exception - {ex.Message}");
                }
            }
            foreach (var msg in input)
            {
                try
                {
                    string msgContent = msg;
                    if (msg.StartsWith("[") && msg.EndsWith("]"))
                    {
                        msgContent = msg.Substring(1, msg.Length - 2);
                    }
                    dynamic jsonMsg = Newtonsoft.Json.JsonConvert.DeserializeObject(msgContent);
                    string deviceId = jsonMsg["deviceId"];

                    string query = $"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('dtmi:embeddedgeorge:sample:Equipment;1') AND deviceId = '{deviceId}'";
                    var queryResponse = twinsClient.QueryAsync<BasicDigitalTwin>(query);
                    BasicDigitalTwin tscTwin = null;
                    await foreach (var t in queryResponse)
                    {
                        tscTwin = t;
                        break;
                    }
                    if (tscTwin != null)
                    {
                        double tempAVG = jsonMsg["TempAVG"];
                        double tempMAX = jsonMsg["TempMax"];
                        double tempMIN = jsonMsg["TempMin"];
                        double tempSTD = jsonMsg["TempSTD"];
                        var newProps = new Dictionary<string, object>();
                        newProps.Add("TempAVG", tempAVG);
                        newProps.Add("TempMAX", tempMAX);
                        newProps.Add("TempMIN", tempMIN);
                        newProps.Add("TempSTD", tempSTD);
                        var patch = new JsonPatchDocument();
                        foreach (var key in newProps.Keys)
                        {
                            if (tscTwin.Contents.ContainsKey(key))
                            {
                                patch.AppendReplace($"/{key}", newProps[key]);
                            }
                            else
                            {
                                patch.AppendAdd($"/{key}", newProps[key]);
                            }
                        }
                        await twinsClient.UpdateDigitalTwinAsync(tscTwin.Id, patch);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    logger.LogWarning(ex.Message);
                }
            }
        }
    }
}