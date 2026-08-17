"use strict";

const screens = {
    landing: document.getElementById("landing"),
    hostLobby: document.getElementById("host-lobby"),
    studentLobby: document.getElementById("student-lobby"),
    quiz: document.getElementById("quiz-screen"),
    results: document.getElementById("results-screen")
};

function showScreen(name) {
    Object.values(screens).forEach(s => s.classList.add("hidden"));
    screens[name].classList.remove("hidden");
}

let isHost = false;
let roomCode = "";
let countdownInterval = null;

const connection = new signalR.HubConnectionBuilder()
    .withUrl("/quizHub")
    .withAutomaticReconnect()
    .build();

// ---- Server -> client events ----

connection.on("PlayerJoined", (names) => {
    if (!isHost) return;
    const list = document.getElementById("host-player-list");
    list.innerHTML = "";
    names.forEach(n => {
        const li = document.createElement("li");
        li.textContent = n;
        list.appendChild(li);
    });
});

connection.on("QuestionStarted", (num, total, text, options, timeLimit) => {
    showScreen("quiz");
    document.getElementById("q-progress").textContent = `Question ${num} of ${total}`;
    document.getElementById("q-text").textContent = text;
    document.getElementById("answer-status").textContent = isHost ? "Waiting for answers..." : "";

    const grid = document.getElementById("q-options");
    grid.innerHTML = "";
    options.forEach((opt, idx) => {
        const btn = document.createElement("button");
        btn.className = "option-btn";
        btn.textContent = opt;
        if (isHost) {
            btn.disabled = true;
        } else {
            btn.addEventListener("click", () => selectAnswer(idx, btn));
        }
        grid.appendChild(btn);
    });

    startCountdown(timeLimit);
});

connection.on("AnswerCountUpdated", (answered, totalPlayers) => {
    if (isHost) {
        document.getElementById("answer-status").textContent = `${answered} / ${totalPlayers} answered`;
    }
});

connection.on("QuestionEnded", (correctIndex, leaderboard) => {
    clearInterval(countdownInterval);

    document.querySelectorAll(".option-btn").forEach((btn, idx) => {
        if (idx === correctIndex) btn.classList.add("correct");
        else if (btn.classList.contains("selected")) btn.classList.add("incorrect");
    });

    setTimeout(() => {
        showScreen("results");
        document.getElementById("results-title").textContent = "Standings";
        renderLeaderboard(leaderboard);
        document.getElementById("host-next-btn").classList.toggle("hidden", !isHost);
    }, 1500);
});

connection.on("QuizEnded", (finalScores) => {
    showScreen("results");
    document.getElementById("results-title").textContent = "Final results";
    renderLeaderboard(finalScores);
    document.getElementById("host-next-btn").classList.add("hidden");
});

// ---- UI actions ----

document.getElementById("host-btn").addEventListener("click", async () => {
    isHost = true;
    if (connection.state !== "Connected") await connection.start();
    roomCode = await connection.invoke("CreateRoom");
    document.getElementById("room-code-display").textContent = roomCode;
    showScreen("hostLobby");
});

document.getElementById("join-btn").addEventListener("click", async () => {
    const name = document.getElementById("join-name").value.trim();
    const code = document.getElementById("join-code").value.trim();
    if (!name || !code) return;

    isHost = false;
    if (connection.state !== "Connected") await connection.start();
    const ok = await connection.invoke("JoinRoom", code, name);
    if (!ok) {
        alert("Room not found. Check the code and try again.");
        return;
    }
    roomCode = code;
    showScreen("studentLobby");
});

document.getElementById("start-quiz-btn").addEventListener("click", () => {
    connection.invoke("StartQuiz", roomCode);
});

document.getElementById("host-next-btn").addEventListener("click", () => {
    connection.invoke("NextQuestion", roomCode);
});

let hasAnsweredThisRound = false;

function selectAnswer(idx, btnEl) {
    if (hasAnsweredThisRound) return;
    hasAnsweredThisRound = true;
    document.querySelectorAll(".option-btn").forEach(b => b.disabled = true);
    btnEl.classList.add("selected");
    document.getElementById("answer-status").textContent = "Answer submitted";
    connection.invoke("SubmitAnswer", roomCode, idx);
}

function startCountdown(seconds) {
    hasAnsweredThisRound = false;
    let remaining = seconds;
    const el = document.getElementById("q-timer");
    el.textContent = `${remaining}s`;
    clearInterval(countdownInterval);
    countdownInterval = setInterval(() => {
        remaining--;
        el.textContent = `${Math.max(remaining, 0)}s`;
        if (remaining <= 0) clearInterval(countdownInterval);
    }, 1000);
}

function renderLeaderboard(entries) {
    const list = document.getElementById("leaderboard");
    list.innerHTML = "";
    entries.forEach((e, i) => {
        const li = document.createElement("li");
        li.innerHTML = `<span>${i + 1}. ${escapeHtml(e.name ?? e.Name)}</span><span>${e.score ?? e.Score} pts</span>`;
        list.appendChild(li);
    });
}

function escapeHtml(str) {
    const div = document.createElement("div");
    div.textContent = str;
    return div.innerHTML;
}
