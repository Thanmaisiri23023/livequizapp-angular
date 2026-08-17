namespace LiveQuizApp.Models;

public class Question
{
    public string Text { get; set; } = "";
    public List<string> Options { get; set; } = new();
    public int CorrectIndex { get; set; }
    public int TimeLimitSeconds { get; set; } = 20;
}

public class Player
{
    public string ConnectionId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Score { get; set; }
    public bool AnsweredCurrentQuestion { get; set; }
}

// Tracks the quiz's live state on the server. One instance per room code.
public class QuizRoom
{
    public string RoomCode { get; set; } = "";
    public string HostConnectionId { get; set; } = "";
    public List<Question> Questions { get; set; } = new();
    public int CurrentQuestionIndex { get; set; } = -1;
    public Dictionary<string, Player> Players { get; set; } = new();
    public DateTime CurrentQuestionStartedAt { get; set; }
    public bool IsAcceptingAnswers { get; set; }
}
