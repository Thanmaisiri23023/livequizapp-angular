# Live Quiz App (ASP.NET Core + SignalR)

A real-time, multiplayer quiz — one person hosts, others join with a room
code from their own device, and everyone sees questions, live answer
counts, and the leaderboard update the instant something happens. Useful
for study group review sessions, TA-led revision, or as a portfolio
project that actually demonstrates real-time architecture.

## How a round works

1. Host clicks **Host a quiz** → gets a 4-digit room code.
2. Students open the same URL on their own device, enter their name and
   the code, and land in a waiting room.
3. Host clicks **Start quiz** → the question, options, and a countdown
   timer are **pushed** to every connected device at once.
4. Students tap an answer. The host's screen updates the "X / Y answered"
   count live as each answer comes in.
5. When time runs out (or everyone's answered, whichever is first), the
   correct answer and updated leaderboard are pushed to everyone
   simultaneously. Faster correct answers score more points.
6. Host clicks **Next question** and the cycle repeats; after the last
   question everyone sees final standings.

## Project structure

```
LiveQuizApp/
├── Program.cs
├── Hubs/
│   └── QuizHub.cs            # all real-time logic + scoring + timers
├── Models/
│   └── QuizModels.cs         # Question, Player, QuizRoom
├── wwwroot/
│   ├── index.html             # one page, both host and student UI
│   ├── css/site.css
│   └── js/quiz.js
└── LiveQuizApp.csproj
```

## Prerequisites

[.NET 10 SDK](https://dotnet.microsoft.com/download)

## Run it

```bash
cd LiveQuizApp
dotnet run
```

Open the printed URL in one tab as the host, and in one or more other
tabs (or other devices on the same network) as students, using the room
code shown on the host screen.

## Where the sample questions live

`SampleQuestions.Get()` in `QuizHub.cs` has 4 starter questions on C#/.NET
basics. Swap that out for questions loaded from a JSON file or database
to reuse it for other subjects.

## Extending it for real use

- **Persist questions** — load `Question` objects from a JSON file or a
  database instead of the hardcoded sample set.
- **Multiple rooms at once** — already supported; each room is isolated
  by its SignalR group and room code.
- **Auth** — add `[Authorize]` and real accounts if you want to track
  scores across sessions per student.
- **Scale beyond one server** — add a SignalR backplane (Redis or Azure
  SignalR Service); right now all room state lives in server memory.
