using Newtonsoft.Json;
using NLog;
using NLog.Targets;
using RestSharp;

namespace JIRAbot;

public class JiraClient
{
    private readonly string _jiraUrl;
    private readonly string _email;
    private readonly string _apiToken;
    private readonly string _projectKey;
    private readonly string _customField;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public class JiraChangelog
    {
        public List<JiraChangelogHistory> histories { get; set; }
    }

    public class JiraChangelogHistory
    {
        public string created { get; set; }
        public List<JiraChangelogItem> items { get; set; }
    }

    public class JiraChangelogItem
    {
        public string field { get; set; }
        public string toString { get; set; }
    }

    public JiraClient(string jiraUrl, string email, string apiToken, string projectKey, string customField)
    {
        _jiraUrl = jiraUrl;
        _email = email;
        _apiToken = apiToken;
        _projectKey = projectKey;
        _customField = customField;
    }

    public async Task<string> CreateIssueAsync(string summary, string description, string channel)
    {
        try
        {
            Logger.Info("Creating Jira issue with summary: {0}", summary);

            var client = new RestClient(_jiraUrl);
            var request = new RestRequest("rest/api/2/issue", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization",
                $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");

            var fields = new Dictionary<string, object>
            {
                { "project", new { key = _projectKey } },
                { "summary", summary },
                { "description", description },
                { "issuetype", new { name = "Task" } },
                { _customField, new { value = channel } }
            };
            var issueBody = new
            {
                fields
            };

            request.AddJsonBody(issueBody);


            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                dynamic responseBody = Newtonsoft.Json.JsonConvert.DeserializeObject(response.Content);
                Logger.Info("Jira issue created with key: {0}", responseBody.key);
                return responseBody.key;
            }
            else
            {
                Logger.Error("Failed to create Jira issue: {0}", response.Content);
                return null;
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception while creating Jira issue");
            throw;
        }
    }

    public async Task AttachFileToIssueAsync(string issueKey, string filePath)
    {
        try
        {
            Logger.Info("Attaching file {0} to Jira issue {1}", filePath, issueKey);

            var client = new RestClient(_jiraUrl);
            var request = new RestRequest($"rest/api/2/issue/{issueKey}/attachments", Method.Post);
            request.AddHeader("X-Atlassian-Token", "no-check");
            request.AddHeader("Authorization",
                $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");

            request.AddFile("file", filePath);

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                Logger.Info("File {0} successfully attached to Jira issue {1}", filePath, issueKey);
            }
            else
            {
                Logger.Error("Failed to attach file: {0}", response.Content);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception while attaching file to Jira issue");
            throw;
        }
    }

    public async Task AddCommentToIssueAsync(string issueKey, string comment)
    {
        try
        {
            Logger.Info("Adding comment to Jira issue {0}", issueKey);

            var client = new RestClient(_jiraUrl);
            var request = new RestRequest($"rest/api/2/issue/{issueKey}/comment", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("Authorization",
                $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");
            request.AddJsonBody(new { body = comment });

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                Logger.Info("Comment added to Jira issue {0}", issueKey);
            }
            else
            {
                Logger.Error("Failed to add comment: {0}", response.Content);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception while adding comment to Jira issue");
            throw;
        }
    }

    public async Task<List<JiraTicket>> GetIssuesUpdatedAfterAsync(DateTime lastSyncDate)
    {
        var allIssues = new List<JiraTicket>();
        int startAt = 0; // Начало страницы (fallback)
        int maxResults = 100; // Максимальное количество задач за запрос
        int page = 0;
        const int maxPages = 1000; // страховка от бесконечного цикла
        string nextPageToken = null; // курсор пагинации

        while (true)
        {
            Logger.Info("Fetching Jira issues updated after: {0}, startAt: {1}, nextPageToken: {2}", lastSyncDate, startAt, nextPageToken ?? "<null>");

            var client = new RestClient(_jiraUrl);
            var request = new RestRequest("rest/api/3/search/jql", Method.Get);

            // Формируем JQL запрос
            var adjustedSyncDate = lastSyncDate.AddHours(3);
            var jql = $"project={_projectKey} AND updated >= '{adjustedSyncDate:yyyy-MM-dd HH:mm}'";
            Logger.Info("JQL Query: {0}", jql);
            request.AddParameter("jql", jql, ParameterType.QueryString);
            request.AddParameter("maxResults", maxResults, ParameterType.QueryString); // Лимит задач
            if (string.IsNullOrEmpty(nextPageToken))
            {
                request.AddParameter("startAt", startAt, ParameterType.QueryString); // Смещение (только до появления курсора)
            }
            else
            {
                request.AddParameter("nextPageToken", nextPageToken, ParameterType.QueryString); // Курсор следующей страницы
            }
            request.AddParameter("fields", $"key,summary,description,status,assignee,created,resolutiondate,updated,{_customField}", ParameterType.QueryString);
            request.AddHeader("Authorization",
                $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                dynamic responseBody = JsonConvert.DeserializeObject(response.Content);
                var issues = new List<JiraTicket>();
                
                var responseObject = Newtonsoft.Json.Linq.JObject.Parse(response.Content);
                bool isLastPage = responseObject.Value<bool?>("isLast") ?? false;
                int returnedCount = (responseObject["issues"] as Newtonsoft.Json.Linq.JArray)?.Count ?? 0;
                string nextTokenFromResponse = responseObject.Value<string>("nextPageToken");
                Logger.Info("Response contains {0} issues (isLast={1}, nextPageToken={2})", returnedCount, isLastPage, nextTokenFromResponse ?? "<null>");

                foreach (var issue in responseBody.issues)
                {
                    if (issue == null)
                    {
                        Logger.Error("Issue is null in response");
                        continue;
                    }
                    
                    if (issue.fields == null)
                    {
                        Logger.Error("Issue fields are null for issue: {0}", issue.key?.ToString() ?? "unknown");
                        continue;
                    }

                    string descriptionText = "no description";

                    if (issue.fields.description != null)
                    {
                        try
                        {
                            // Проверяем, является ли описание JSON объектом или простой строкой
                            if (issue.fields.description is Newtonsoft.Json.Linq.JObject)
                            {
                                descriptionText =
                                    issue.fields.description.SelectToken("content[0].content[0].text")?.ToString() ??
                                    "no description";
                            }
                            else
                            {
                                descriptionText = issue.fields.description.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error parsing description: {0}", ex.Message);
                            descriptionText = "no description";
                        }
                    }

                    // Извлечение ClientName из кастомного поля с учетом разных форматов (object/array/string)
                    string clientName = null;
                    try
                    {
                        var cfToken = issue.fields[_customField] as Newtonsoft.Json.Linq.JToken;
                        if (cfToken == null)
                        {
                            clientName = null;
                        }
                        else if (cfToken.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                        {
                            var obj = (Newtonsoft.Json.Linq.JObject)cfToken;
                            clientName = (string)(obj["value"] ?? obj["name"] ?? obj["displayName"] ?? obj["id"]) ?? obj.ToString();
                        }
                        else if (cfToken.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                        {
                            var first = cfToken.First;
                            if (first != null)
                            {
                                clientName = (string)(first["value"] ?? first["name"] ?? first["displayName"] ?? first["id"]) ?? first.ToString();
                            }
                        }
                        else
                        {
                            clientName = cfToken.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error parsing custom field {0}: {1}", _customField, ex.Message);
                    }

                    var ticket = new JiraTicket
                    {
                        JiraKey = issue.key?.ToString(),
                        ClientName = clientName,
                        Assignee = issue.fields.assignee != null && issue.fields.assignee is Newtonsoft.Json.Linq.JObject
                            ? issue.fields.assignee["displayName"]?.ToString() ?? "not assigned"
                            : "not assigned",
                        Summary = issue.fields.summary?.ToString(),
                        Description = descriptionText,
                        Status = issue.fields.status != null && issue.fields.status is Newtonsoft.Json.Linq.JObject
                            ? issue.fields.status["name"]?.ToString()
                            : issue.fields.status?.ToString(),
                        CreatedAt = issue.fields.created != null
                            ? DateTime.Parse(issue.fields.created.ToString()).ToUniversalTime()
                            : DateTime.MinValue,
                        ClosedAt = issue.fields.resolutiondate != null
                            ? DateTime.Parse(issue.fields.resolutiondate.ToString()).ToUniversalTime()
                            : (DateTime?)null
                    };

                    issues.Add(ticket);
                }

                allIssues.AddRange(issues);

                Logger.Info("Fetched {0} issues in this page", issues.Count);

                // Проверяем, есть ли еще задачи для обработки
                if (isLastPage || returnedCount == 0)
                {
                    break; // Последняя страница или пустой ответ
                }

                // Курсорная пагинация имеет приоритет
                if (!string.IsNullOrEmpty(nextTokenFromResponse))
                {
                    nextPageToken = nextTokenFromResponse;
                }
                else
                {
                    // Fallback к offset-пагинации
                    startAt += returnedCount;
                }

                page++;
                if (page >= maxPages)
                {
                    Logger.Error("Pagination aborted: exceeded max pages {0}. startAt={1}, nextPageToken={2}", maxPages, startAt, nextPageToken ?? "<null>");
                    break;
                }
            }
            else
            {
                Logger.Error("Failed to fetch Jira issues: {0}", response.Content);
                break; // Выходим из цикла в случае ошибки
            }
        }

        Logger.Info("Total fetched issues: {0}", allIssues.Count);
        return allIssues;
    }

    public async Task<JiraChangelog> GetIssueChangelogAsync(string issueKey)
    {
        try
        {
            var client = new RestClient(_jiraUrl);
            var request = new RestRequest($"rest/api/2/issue/{issueKey}/changelog", Method.Get);
            request.AddHeader("Authorization",
                $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                return JsonConvert.DeserializeObject<JiraChangelog>(response.Content);
            }

            Logger.Error("Failed to fetch changelog for issue {0}: {1}", issueKey, response.Content);
            return null;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Exception while fetching changelog for issue");
            throw;
        }
    }

}
        
