namespace JIRAbot;

public class Clients
{
    public int Id { get; set; }
    public string Name { get; set; }
}
public class Group
{
    public int Id { get; set; }
    public int Сlient_id { get; set; }
    public string Name { get; set; }
    public string GroupId { get; set; }
    public List<Request> Requests { get; set; }
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
    public List<JiraTask> JiraTasks { get; set; }
    public List<RequestStatusHistory> StatusHistories { get; set; }
    public List<Comment> Comments { get; set; }
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

