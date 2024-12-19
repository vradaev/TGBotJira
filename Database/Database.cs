namespace JIRAbot
{
    public class Client
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }

        public ICollection<Group> Groups { get; set; } = new List<Group>();
    }

    public class Group
    {
        public int Id { get; set; }
        public int ClientId { get; set; } 
        public string Name { get; set; }
        public string GroupId { get; set; }
        public DateTime CreatedAt { get; set; }

        public Client Client { get; set; }
        public List<Request> Requests { get; set; } = new List<Request>();
    }

    public class RequestType
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class Request
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public Group Group { get; set; }
        public int TypeId { get; set; }
        public RequestType Type { get; set; }
        public string MessageId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public List<JiraTask> JiraTasks { get; set; } = new List<JiraTask>();
        public List<RequestStatusHistory> StatusHistories { get; set; } = new List<RequestStatusHistory>();
        public List<Comment> Comments { get; set; } = new List<Comment>();
    }

    public class JiraTask
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public Request Request { get; set; }
        public string JiraKey { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Status { get; set; }
    }

    public class RequestStatusHistory
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public Request Request { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public DateTime ChangedAt { get; set; }
    }

    public class Comment
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public Request Request { get; set; }
        public string MessageId { get; set; }
        public string CommentText { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    
    public class DutyOfficer
    {
        public int Id { get; set; }
        public string PhoneNumber { get; set; }
        public string DutyType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class Setting
    {
        public int Id { get; set; }
        public string KeyName { get; set; }
        public string Value { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
    public class JiraTicket
    {
        public int Id { get; set; }
        public string JiraKey { get; set; }
        public string ClientName { get; set; }
        public string Assignee { get; set; }
        public string CategoryId { get; set; }
        public string Status { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? FirstRespondAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
    
}