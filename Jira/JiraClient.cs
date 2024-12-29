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
        int startAt = 0; // Начало страницы
        int maxResults = 100; // Максимальное количество задач за запрос

        while (true)
        {
            Logger.Info("Fetching Jira issues updated after: {0}, startAt: {1}", lastSyncDate, startAt);

            var client = new RestClient(_jiraUrl);
            var request = new RestRequest("rest/api/3/search", Method.Get);

            // Формируем JQL запрос
            var adjustedSyncDate = lastSyncDate.AddHours(3);
            var jql = $"project={_projectKey} AND updated >= '{adjustedSyncDate:yyyy-MM-dd HH:mm}'";
            request.AddParameter("jql", jql, ParameterType.QueryString);
            request.AddParameter("maxResults", maxResults, ParameterType.QueryString); // Лимит задач
            request.AddParameter("startAt", startAt, ParameterType.QueryString); // Смещение
            request.AddHeader("Authorization",
                $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");

            var response = await client.ExecuteAsync(request);

            if (response.IsSuccessful)
            {
                dynamic responseBody = JsonConvert.DeserializeObject(response.Content);
                var issues = new List<JiraTicket>();

                foreach (var issue in responseBody.issues)
                {
                    if (issue == null || issue.fields == null)
                    {
                        Logger.Error("Issue or fields are null in response");
                        continue;
                    }

                    string descriptionText = "no description";

                    if (issue.fields.description != null)
                    {
                        try
                        {
                            descriptionText =
                                issue.fields.description.SelectToken("content[0].content[0].text")?.ToString() ??
                                "no description";
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("description is null in response");
                            continue;
                        }
                    }

                    var ticket = new JiraTicket
                    {
                        JiraKey = issue.key?.ToString(),
                        ClientName = issue.fields[_customField]?.value?.ToString(),
                        Assignee = issue.fields.assignee != null
                            ? issue.fields.assignee["displayName"]?.ToString()
                            : "not assigned",
                        Summary = issue.fields.summary?.ToString(),
                        Description = descriptionText,
                        Status = issue.fields.status?.name?.ToString(),
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
                if (issues.Count < maxResults)
                {
                    break; // Если задач меньше `maxResults`, значит, это последняя страница
                }

                startAt += maxResults; // Увеличиваем смещение для следующей страницы
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
        
