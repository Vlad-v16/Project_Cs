using System;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ChessServer.Models;

namespace ChessServer
{
    // Клас для збереження інформації про підключеного гравця
    class PlayerInfo
    {
        public PieceColor Color { get; set; }
        public string Name { get; set; } = "Гравець";
        // Прапорець для ідентифікації глядача
        public bool IsSpectator { get; set; } = false; 
    }

    class Program
    {
        private static readonly ConcurrentDictionary<WebSocket, PlayerInfo> _players = new ConcurrentDictionary<WebSocket, PlayerInfo>();
        private static readonly Board gameBoard = new Board();

        static async Task Main(string[] args)
        {
            HttpListener listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/"); 
            listener.Start();
            Console.WriteLine("Сервер шахів успішно запущено на порту 8080...");

            while (true)
            {
                try
                {
                    HttpListenerContext context = await listener.GetContextAsync();
                    if (context.Request.IsWebSocketRequest)
                    {
                        _ = Task.Run(() => ProcessRequest(context));
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Помилка у головному циклі сервера: {ex.Message}");
                }
            }
        }

        private static async Task ProcessRequest(HttpListenerContext context)
        {
            WebSocket? ws = null;
            try
            {
                HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null);
                ws = wsContext.WebSocket;
                
                PieceColor assignedColor = PieceColor.White;
                bool isSpectator = false;

                // Рахуємо тільки реальних гравців, ігноруючи глядачів
                int currentActivePlayers = _players.Values.Count(p => !p.IsSpectator);

                if (currentActivePlayers == 0) 
                {
                    assignedColor = PieceColor.White;
                    Console.WriteLine("Приєднався Гравець 1 (БІЛІ)");
                } 
                else if (currentActivePlayers == 1) 
                {
                    // Перевіряємо, який колір вже зайнято
                    bool hasWhite = _players.Values.Any(p => !p.IsSpectator && p.Color == PieceColor.White);
                    assignedColor = hasWhite ? PieceColor.Black : PieceColor.White;
                    Console.WriteLine($"Приєднався Гравець 2 ({(assignedColor == PieceColor.White ? "БІЛІ" : "ЧОРНІ")})");
                } 
                else 
                {
                    isSpectator = true;
                    Console.WriteLine("Глядач підключився (тільки перегляд)");
                }

                // ДОДАЄМО ВСІХ (і гравців, і глядачів) у _players, щоб глядачі отримували розсилку ходів
                _players.TryAdd(ws, new PlayerInfo { 
                    Color = assignedColor, 
                    Name = isSpectator ? "Глядач" : "Гравець", 
                    IsSpectator = isSpectator 
                });

                var welcomeObj = new 
                { 
                    type = "assign_color", 
                    data = new { color = isSpectator ? "spectator" : assignedColor.ToString().ToLower() } 
                };
                string welcomeJson = JsonSerializer.Serialize(welcomeObj);
                await SendToClient(ws, welcomeJson);

                byte[] buffer = new byte[1024 * 4];

                while (ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Отримано: {message}");

                        using var doc = JsonDocument.Parse(message);
                        if (doc.RootElement.TryGetProperty("type", out var typeProp))
                        {
                            string? type = typeProp.GetString();

                            if (type == "player_connect")
                            {
                                string inputName = "Гравець";
                                if (doc.RootElement.TryGetProperty("data", out var dProp) && dProp.TryGetProperty("playerName", out var nameProp))
                                {
                                    inputName = nameProp.GetString() ?? "Гравець";
                                }

                                // Перевірка на унікальність імені
                                bool isNameTaken = false;
                                foreach (var pair in _players)
                                {
                                    if (pair.Key != ws && pair.Value.Name.Equals(inputName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        isNameTaken = true;
                                        break;
                                    }
                                }

                                if (isNameTaken)
                                {
                                    string errorResponse = JsonSerializer.Serialize(new {
                                        type = "connect_error",
                                        data = new { message = "NameOccupied" }
                                    });

                                    await SendToClient(ws, errorResponse);
                                    Console.WriteLine($"[Відхилено]: Спроба підключення із зайнятим ім'ям: {inputName}");

                                    _players.TryRemove(ws, out _);
                                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Name occupied", CancellationToken.None); } catch { }
                                    break; 
                                }

                                if (_players.TryGetValue(ws, out var pInfo))
                                {
                                    pInfo.Name = inputName;
                                    Console.WriteLine($"Клієнту присвоєно ім'я: {inputName}");
                                }

                                string response = JsonSerializer.Serialize(new {
                                    type = "connect_success",
                                    data = new {
                                        playerId = Guid.NewGuid().ToString(),
                                        color = isSpectator ? "spectator" : assignedColor.ToString().ToLower()
                                    }
                                });
                                await SendToClient(ws, response);
                                
                                // Надсилаємо повідомлення про хід тільки якщо це другий ГРАВЕЦЬ
                                int activePlayers = _players.Values.Count(p => !p.IsSpectator);
                                if (!isSpectator && activePlayers == 2)
                                {
                                    await BroadcastTurnNotification();
                                }
                            }
                            else if (type == "chess_move")
                            {
                                // Глядачі не можуть робити ходи
                                if (isSpectator) continue;

                                var data = doc.RootElement.GetProperty("data");
                                string from = data.GetProperty("from").GetString()!;
                                string to = data.GetProperty("to").GetString()!;

                                if (!_players.TryGetValue(ws, out var playerInfo)) continue;

                                if (playerInfo.Color != gameBoard.CurrentTurn)
                                {
                                    await SendToClient(ws, "{\"type\":\"invalid_move\", \"data\":\"Ви Глядач!\"}");
                                    continue;
                                }

                                var fromPos = ParseNotation(from);
                                var toPos = ParseNotation(to);
                                var moveResult = gameBoard.TryMove(fromPos.row, fromPos.col, toPos.row, toPos.col);

                                if (moveResult.Success)
                                {
                                    string checkStatus = "none";
                                    if (gameBoard.IsInCheck(PieceColor.White)) checkStatus = "white";
                                    else if (gameBoard.IsInCheck(PieceColor.Black)) checkStatus = "black";

                                    string gameStatus = "active"; 
                                    if (gameBoard.IsCheckmate(PieceColor.White)) gameStatus = "black_win"; 
                                    else if (gameBoard.IsCheckmate(PieceColor.Black)) gameStatus = "white_win"; 

                                    var movePayload = new {
                                        type = "move_made",
                                        data = new { from = from, to = to, check = checkStatus, status = gameStatus }
                                    };

                                    await BroadcastMessage(JsonSerializer.Serialize(movePayload));

                                    if (moveResult.IsCastling)
                                    {
                                        string rookFrom = $"{(char)('a' + moveResult.RookFromCol)}{8 - fromPos.row}";
                                        string rookTo = $"{(char)('a' + moveResult.RookToCol)}{8 - fromPos.row}";
                                        var rookPayload = new { type = "move_made", data = new { from = rookFrom, to = rookTo, check = "none", status = "active" } };
                                        await Task.Delay(50); 
                                        await BroadcastMessage(JsonSerializer.Serialize(rookPayload));
                                    }

                                    await BroadcastTurnNotification();
                                }
                                else
                                {
                                    await SendToClient(ws, "{\"type\":\"invalid_move\", \"data\":\"Хід неможливий!\"}");
                                }
                            }
                            else if (type == "get_valid_moves")
                            {
                                var data = doc.RootElement.GetProperty("data");
                                string position = data.GetProperty("position").GetString()!;
                                var (row, col) = ParseNotation(position);
                                List<(int targetRow, int targetCol)> validMoves = gameBoard.GetValidMovesForPiece(row, col);

                                var movesWithTypes = new List<object>();
                                foreach (var move in validMoves)
                                {
                                    string notation = ConvertToNotation(move.targetRow, move.targetCol);
                                    bool isCapture = gameBoard.Pieces[move.targetRow, move.targetCol] != null;
                                    movesWithTypes.Add(new { to = notation, isCapture = isCapture });
                                }

                                await SendToClient(ws, JsonSerializer.Serialize(new { type = "valid_moves", data = new { moves = movesWithTypes } }));
                            }
                            else if (type == "resign")
                            {
                                await HandleResignOrDisconnect(ws, isIntentional: true);
                            }
                        } 
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Помилка сесії клієнта: {ex.Message}");
            }
            finally
            {
                if (ws != null)
                {
                    await HandleResignOrDisconnect(ws, isIntentional: false);
                    _players.TryRemove(ws, out _);
                    ws.Dispose();
                }
            }
        }

        private static async Task HandleResignOrDisconnect(WebSocket leaverWs, bool isIntentional)
        {
            if (!_players.TryGetValue(leaverWs, out var leaverInfo)) return;

            // Якщо сервер покинув глядач - просто ігноруємо, гра триває
            if (leaverInfo.IsSpectator) return;

            WebSocket? winnerWs = null;
            PlayerInfo? winnerInfo = null;

            // Шукаємо суперника (тільки серед гравців)
            foreach (var pair in _players)
            {
                if (pair.Key != leaverWs && !pair.Value.IsSpectator)
                {
                    winnerWs = pair.Key;
                    winnerInfo = pair.Value;
                    break;
                }
            }

            string leaverName = leaverInfo.Name;
            string winnerName = winnerInfo != null ? winnerInfo.Name : "Суперник";

            var payload = new
            {
                type = "player_resigned",
                data = new
                {
                    leaverId = leaverInfo.Color.ToString().ToLower(),
                    leaverName = leaverName,
                    winnerName = winnerName,
                    reason = isIntentional ? "resign" : "disconnect"
                }
            };

            string json = JsonSerializer.Serialize(payload);
            await BroadcastMessage(json);
        }

        private static async Task BroadcastTurnNotification()
        {
            foreach (var pair in _players)
            {
                // Не надсилаємо "Твій хід" глядачам
                if (!pair.Value.IsSpectator && pair.Value.Color == gameBoard.CurrentTurn && pair.Key.State == WebSocketState.Open)
                {
                    await SendToClient(pair.Key, "{\"type\":\"your_turn\", \"data\":{}}");
                }
            }
        }

        private static async Task BroadcastMessage(string message)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            foreach (var client in _players.Keys)
            {
                if (client.State == WebSocketState.Open)
                {
                    try { await client.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None); }
                    catch { }
                }
            }
        }

        private static async Task SendToClient(WebSocket ws, string message)
        {
            if (ws == null || ws.State != WebSocketState.Open) return;
            try
            {
                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch { }
        }

        private static (int row, int col) ParseNotation(string pos) => (8 - (pos[1] - '0'), pos[0] - 'a');
        private static string ConvertToNotation(int row, int col) => $"{(char)('a' + col)}{8 - row}";
    }
}