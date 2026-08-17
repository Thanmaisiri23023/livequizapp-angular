using LiveQuizApp.Models;
using Microsoft.AspNetCore.SignalR;

namespace LiveQuizApp.Hubs;

public class QuizHub : Hub
{
    // Rooms keyed by a short join code, shared across all connections.
    // In-memory is fine for a classroom demo; swap for Redis/a DB to
    // survive server restarts or scale across multiple server instances.
    private static readonly Dictionary<string, QuizRoom> _rooms = new();
    private static readonly Random _rng = new();

    // ---- Host flow ----

    public async Task<string> CreateRoom()
    {
        string code;
        do { code = _rng.Next(1000, 9999).ToString(); }
        while (_rooms.ContainsKey(code));

        var room = new QuizRoom
        {
            RoomCode = code,
            HostConnectionId = Context.ConnectionId,
            Questions = SampleQuestions.Get()
        };
        _rooms[code] = room;

        await Groups.AddToGroupAsync(Context.ConnectionId, code);
        return code;
    }

    public async Task StartQuiz(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return;
        await NextQuestion(roomCode);
    }

    public async Task NextQuestion(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return;

        room.CurrentQuestionIndex++;
        if (room.CurrentQuestionIndex >= room.Questions.Count)
        {
            var finalScores = room.Players.Values
                .OrderByDescending(p => p.Score)
                .Select(p => new { p.Name, p.Score })
                .ToList();
            await Clients.Group(roomCode).SendAsync("QuizEnded", finalScores);
            return;
        }

        foreach (var p in room.Players.Values) p.AnsweredCurrentQuestion = false;

        var q = room.Questions[room.CurrentQuestionIndex];
        room.CurrentQuestionStartedAt = DateTime.UtcNow;
        room.IsAcceptingAnswers = true;

        // Push the question to every connected client (host + all students)
        // the moment it starts - this is the real-time push, not a poll.
        await Clients.Group(roomCode).SendAsync("QuestionStarted",
            room.CurrentQuestionIndex + 1, room.Questions.Count, q.Text, q.Options, q.TimeLimitSeconds);

        // Server-side timer closes the question automatically when time's up,
        // even if some students never answer.
        _ = CloseQuestionAfterDelay(roomCode, room.CurrentQuestionIndex, q.TimeLimitSeconds);
    }

    private async Task CloseQuestionAfterDelay(string roomCode, int questionIndex, int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));

        if (!_rooms.TryGetValue(roomCode, out var room)) return;
        if (room.CurrentQuestionIndex != questionIndex || !room.IsAcceptingAnswers) return;

        await CloseQuestion(roomCode);
    }

    private async Task CloseQuestion(string roomCode)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return;
        room.IsAcceptingAnswers = false;

        var q = room.Questions[room.CurrentQuestionIndex];
        var leaderboard = room.Players.Values
            .OrderByDescending(p => p.Score)
            .Select(p => new { p.Name, p.Score })
            .ToList();

        // Every client gets the correct answer and updated standings at once
        await Clients.Group(roomCode).SendAsync("QuestionEnded", q.CorrectIndex, leaderboard);
    }

    // ---- Student flow ----

    public async Task<bool> JoinRoom(string roomCode, string playerName)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return false;

        room.Players[Context.ConnectionId] = new Player
        {
            ConnectionId = Context.ConnectionId,
            Name = playerName
        };

        await Groups.AddToGroupAsync(Context.ConnectionId, roomCode);

        // Let the host (and everyone) see the roster grow live as students join
        await Clients.Group(roomCode).SendAsync("PlayerJoined", room.Players.Values.Select(p => p.Name).ToList());
        return true;
    }

    public async Task SubmitAnswer(string roomCode, int selectedIndex)
    {
        if (!_rooms.TryGetValue(roomCode, out var room)) return;
        if (!room.IsAcceptingAnswers) return;
        if (!room.Players.TryGetValue(Context.ConnectionId, out var player)) return;
        if (player.AnsweredCurrentQuestion) return;

        player.AnsweredCurrentQuestion = true;
        var q = room.Questions[room.CurrentQuestionIndex];

        if (selectedIndex == q.CorrectIndex)
        {
            // Faster correct answers score higher - rewards speed, like Kahoot
            var elapsed = (DateTime.UtcNow - room.CurrentQuestionStartedAt).TotalSeconds;
            var speedBonus = Math.Max(0, q.TimeLimitSeconds - elapsed) / q.TimeLimitSeconds;
            player.Score += 500 + (int)(500 * speedBonus);
        }

        // Let the host see answer counts tick up live
        var answeredCount = room.Players.Values.Count(p => p.AnsweredCurrentQuestion);
        await Clients.Group(roomCode).SendAsync("AnswerCountUpdated", answeredCount, room.Players.Count);

        // If everyone has answered, close the question early instead of waiting for the timer
        if (answeredCount == room.Players.Count)
        {
            await CloseQuestion(roomCode);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var room in _rooms.Values)
        {
            room.Players.Remove(Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }
}

public static class SampleQuestions
{
    // Placeholder question set covering topics from the training program
    // (C# / .NET fundamentals) - swap this for questions loaded from a
    // database or JSON file for real use.
    public static List<Question> Get() => new()
    {
        new Question
        {
            Text = "Which keyword makes a class unable to be inherited?",
            Options = new() { "static", "sealed", "readonly", "const" },
            CorrectIndex = 1,
            TimeLimitSeconds = 20
        },
        new Question
        {
            Text = "What does async/await primarily help with?",
            Options = new() { "Memory management", "Non-blocking I/O", "Faster loops", "Type safety" },
            CorrectIndex = 1,
            TimeLimitSeconds = 20
        },
        new Question
        {
            Text = "Which collection guarantees unique elements?",
            Options = new() { "List<T>", "HashSet<T>", "Queue<T>", "Stack<T>" },
            CorrectIndex = 1,
            TimeLimitSeconds = 15
        },
        new Question
        {
            Text = "In ASP.NET Core Minimal APIs, which method maps a GET endpoint?",
            Options = new() { "app.Get()", "app.MapGet()", "app.Route()", "app.OnGet()" },
            CorrectIndex = 1,
            TimeLimitSeconds = 15
        }
    };
}
