using NLog;
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
                request.AddHeader("Authorization", $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");
                
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
                request.AddHeader("Authorization", $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");

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
                request.AddHeader("Authorization", $"Basic {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_email}:{_apiToken}"))}");
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
}