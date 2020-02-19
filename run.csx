#r "Newtonsoft.Json"

using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;

public enum AlertTypes
{
    ServiceBus,
    Database,
    Infrastructure,
    Application,
}

public class CommonAlert
{
    public string schemaId { get; set; }
    public CommonAlertData data { get; set; }
}


public class CommonAlertData
{
    public JObject essentials { get; set; }
    public JObject alertContext { get; set; }
}

private static Dictionary<AlertTypes, string> _alertUrls = new Dictionary<AlertTypes, string>
{
    { AlertTypes.ServiceBus,     "https://hooks.slack.com/services/<WEBHOOK-VALUE>" },
    { AlertTypes.Database,       "https://hooks.slack.com/services/<WEBHOOK-VALUE>" },
    { AlertTypes.Infrastructure, "https://hooks.slack.com/services/<WEBHOOK-VALUE>" },
    { AlertTypes.Application, "https://hooks.slack.com/services/<WEBHOOK-VALUE>" },
};

private static Dictionary<string, string[]> _exclude = new Dictionary<string, string[]>
{
	{
		nameof(CommonAlertData.essentials), new[]
		{
			"originAlertId",
			"alertId",
            "essentialsVersion",
            "alertContextVersion",
            "firedDateTime",
		}
	},
	{
		nameof(CommonAlertData.alertContext), new string[]
		{
			"authorization",
			"claims",
            "submissionTimestamp",
            "correlationId",
            "eventDataId",
            "operationId",
            "condition",
            "properties",
            "conditionType",
            "SearchIntervalDurationMin",
            "SearchIntervalInMinutes",
            "Operator",
            "SearchIntervalStartTimeUtc",
            "SearchIntervalEndtimeUtc",
            "WorkspaceId",
            "SearchQuery",
            "caller",
            "subStatus",
            "channels",
            "level",
            "httpRequest",
            "ResourceType",
            "eventSource",
            "SearchResults",
            "Threshold",
		}
	}
};

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

//    string name = req.Query["name"];


    var contentString = await new StreamReader(req.Body).ReadToEndAsync();
    var alert = JsonConvert.DeserializeObject<CommonAlert>(contentString);

    var attachments = JObjectToAttachments(alert.data.essentials, "#1a72b1", nameof(CommonAlertData.essentials), _exclude[nameof(CommonAlertData.essentials)])
        .Concat(JObjectToAttachments(alert.data.alertContext, "#e76024", nameof(CommonAlertData.alertContext), _exclude[nameof(CommonAlertData.alertContext)]));

    var type = GetAlertType(alert, req.Query["type"]);
    var slackContent = new
    {
        text = "",
        mrkdwn = true,
        attachments,
    };
    var content = new StringContent(JsonConvert.SerializeObject(slackContent), System.Text.Encoding.UTF8, "application/json");

    using(var httpClient = new HttpClient())
    {

        await httpClient.PostAsync(_alertUrls[type], content);
    }


    //return req.CreateResponse(HttpStatusCode.OK);
    var name = "Scott";
    return name != null
        ? (ActionResult)new OkObjectResult($"Hello, {name}")
        : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
}

private static AlertTypes GetAlertType(CommonAlert alert, string type)
{
    if (Enum.TryParse<AlertTypes>(type, /*ignoreCase:*/true, out var value))
    {
        return value;
    }
   
    throw new ArgumentException($"Expected alert type, got {type}");
} 

private static IEnumerable<object> JObjectToAttachments(JObject value, string color, string title, string[] exclude)
{
	return value.Properties()
        .Where(p => !exclude.Contains(p.Name))
        .Select((p, i) => new
        {
            title = p.Name,
            value = ToSlackString(p.Value),
        })
        .OrderBy(p => p.value.Length)
        .Select((p, i) => new
        {
            i = i / 2, // hack to pair up consecutive items
            p.title,
            p.value,
            @short = true,//p.title.Length < 16,
        })
        .GroupBy(g => g.i)
        .Select(g => new
        {
            pretext = g.Key == 0 ? $"`{title}`" : null,
            color,
            fields = g.Select(p => new { p.title, p.value, p.@short }),
        });
}

private static string ToSlackString(JToken token)
{
    var value = token.ToString();
    if (!string.IsNullOrEmpty(value) && Uri.TryCreate(value, UriKind.Absolute, out var uri))
    {
        return $"<{value}|link>";
    }
    return value;
}
