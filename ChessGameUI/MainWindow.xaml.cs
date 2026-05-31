using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ChessGameUI
{
    public partial class MainWindow : Window
    {
        // ЗМІННІ
        private ClientWebSocket? ws;
        private string? myPlayerId;
        private string? myPlayerName;
        private string? myColor;
        private string currentCheckColor = "none";
        private Button[,] squares = new Button[8, 8];
        private string? selectedSquare = null;

        // КОНСТРУКТОР
        public MainWindow()
        {
            InitializeComponent();
            Console.OutputEncoding = Encoding.UTF8;
            CreateChessBoard();
        }

        // СТВОРЕННЯ ДОШКИ
        private void CreateChessBoard()
        {
            for (int i = 0; i < 8; i++)
            {
                ChessBoard.RowDefinitions.Add(new RowDefinition());
                ChessBoard.ColumnDefinitions.Add(new ColumnDefinition());
            }

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    Button square = new Button();
                    square.FontSize = 48;
                    square.Margin = new Thickness(2);

                    // М'яко скидаємо межі, щоб кнопка слухалася кольору фону
                    square.BorderThickness = new Thickness(0);
                    
                    // Якщо увімкнено "ClickMode.Release", кнопка менше смикає дефолтні стилі
                    square.ClickMode = ClickMode.Release; 

                    if ((row + col) % 2 == 0)
                        square.Background = new SolidColorBrush(Color.FromRgb(240, 217, 181));
                    else
                        square.Background = new SolidColorBrush(Color.FromRgb(181, 136, 99));

                    string position = GetPosition(row, col);
                    square.Tag = position;
                    square.Click += Square_Click;

                    Grid.SetRow(square, row);
                    Grid.SetColumn(square, col);
                    ChessBoard.Children.Add(square);
                    squares[row, col] = square;
                }
            }

            SetupInitialPosition();
        }

        // Перетворення row,col → "e2"
        private string GetPosition(int row, int col)
        {
            char file = (char)('a' + col);
            int rank = 8 - row;
            return $"{file}{rank}";
        }

        // Початкова розстановка
        private void SetupInitialPosition()
        {
            // Білі фігури
            for (int col = 0; col < 8; col++) SetPiece(6, col, "Images/wp.png");
            SetPiece(7, 0, "Images/wr.png");
            SetPiece(7, 1, "Images/wn.png");
            SetPiece(7, 2, "Images/wb.png");
            SetPiece(7, 3, "Images/wq.png");
            SetPiece(7, 4, "Images/wk.png");
            SetPiece(7, 5, "Images/wb.png");
            SetPiece(7, 6, "Images/wn.png");
            SetPiece(7, 7, "Images/wr.png");

            // Чорні фігури
            for (int col = 0; col < 8; col++) SetPiece(1, col, "Images/bp.png");
            SetPiece(0, 0, "Images/br.png");
            SetPiece(0, 1, "Images/bn.png");
            SetPiece(0, 2, "Images/bb.png");
            SetPiece(0, 3, "Images/bq.png");
            SetPiece(0, 4, "Images/bk.png");
            SetPiece(0, 5, "Images/bb.png");
            SetPiece(0, 6, "Images/bn.png");
            SetPiece(0, 7, "Images/br.png");
        }

        private void SetPiece(int row, int col, string imagePath)
        {
            Image pieceImage = new Image();
            pieceImage.Source = new BitmapImage(new Uri($"pack://application:,,,/{imagePath}"));
            pieceImage.Tag = imagePath; 
            pieceImage.Margin = new Thickness(5); 

            squares[row, col].Content = pieceImage;
        }

        // ОБРОБКА КЛІКІВ
        private void Square_Click(object sender, RoutedEventArgs e)
        {
            Button? clickedSquare = sender as Button;
            if (clickedSquare == null) return;

            string? position = clickedSquare.Tag as string;
            if (position == null) return;

            if (selectedSquare == null)
            {
                // Перший клік — вибір фігури
                if (clickedSquare.Content is Image)
                {
                    selectedSquare = position;
                    
                    // Підсвічуємо саму вибрану фігуру жовтою рамкою
                    clickedSquare.BorderBrush = Brushes.Yellow;
                    clickedSquare.BorderThickness = new Thickness(4);
                    StatusText.Text = $"Вибрано: {position}. Запитуємо доступні ходи...";

                    // Запитуємо у сервера, куди ця фігура може ходити
                    SendGetValidMovesRequest(position);
                }
            }
            else
            {
                // Якщо гравець клікнув на ту саму клітинку — знімаємо виділення
                if (selectedSquare == position)
                {
                    ResetHighlight();
                    selectedSquare = null;
                    StatusText.Text = "Виділення скасовано.";
                    return;
                }

                // Другий клік — намагаємося походити
                string from = selectedSquare;
                string to = position;

                // Скидаємо підсвічування ВСІХ клітинок (і рамки, і кольорові ходи)
                ResetHighlight();

                SendMove(from, to);
                StatusText.Text = $"Відправлено запит: {from} → {to} ... чекаємо сервер";

                selectedSquare = null;
            }
        }

        private void ResetHighlight()
        {
            foreach (var square in squares)
            {
                if (square != null)
                {
                    square.BorderThickness = new Thickness(0);
                }
            }
            // Повертаємо клітинкам шахівниці їхні оригінальні кольори
            ResetSquareBackgrounds(); 
        }

        private void ResetSquareBackgrounds()
        {
            // 1. Повертаємо всім клітинкам шахівниці їхні оригінальні кольори
            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (squares[row, col] != null)
                    {
                        if ((row + col) % 2 == 0)
                            squares[row, col].Background = new SolidColorBrush(Color.FromRgb(240, 217, 181)); 
                        else
                            squares[row, col].Background = new SolidColorBrush(Color.FromRgb(181, 136, 99));    
                    }
                }
            }

            // 2. Якщо зараз зафіксовано шах — накладаємо червоне підсвічування поверх базового кольору
            if (!string.IsNullOrEmpty(currentCheckColor) && currentCheckColor != "none")
            {
                HighlightKingCheck(currentCheckColor);
            }
        }

        private void HighlightKingCheck(string color)
        {
            if (string.IsNullOrEmpty(color) || color.ToLower() == "none") return;

            string colorLower = color.ToLower();
            
            // Шукаємо ключове ім'я файлу короля: "wk" (white king) або "bk" (black king)
            string kingKey = colorLower == "white" ? "wk" : "bk";

            for (int row = 0; row < 8; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (squares[row, col]?.Content is Image img)
                    {
                        string imgTag = img.Tag?.ToString()?.ToLower() ?? "";
                        
                        // Перевіряємо, чи є в назві картинки згадка про потрібного короля
                        if (imgTag.Contains(kingKey))
                        {
                            // Насичений яскраво-червоний колір
                            squares[row, col].Background = new SolidColorBrush(Color.FromRgb(234, 55, 55));
                            return; // Короля знайдено і підсвічено, виходимо
                        }
                    }
                }
            }
        }

        // WEBSOCKET
        private async void SendGetValidMovesRequest(string position)
        {
            var message = new
            {
                type = "get_valid_moves",
                data = new
                {
                    playerId = myPlayerId,
                    position = position
                }
            };
            await SendJson(message);
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ws = new ClientWebSocket();
                string serverAddress = ServerAddressBox.Text;

                StatusText.Text = "Підключення...";

                await ws.ConnectAsync(new Uri(serverAddress), CancellationToken.None);

                StatusText.Text = "✅ Connected!";
                ConnectButton.IsEnabled = false;
                ResignButton.IsEnabled = true;

                string playerName = PlayerNameBox.Text;
                myPlayerName = playerName; // Зберігаємо введений нікнейм у глобальну змінну

                await SendPlayerConnect(playerName);

                _ = Task.Run(() => ListenToServer());
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Помилка: {ex.Message}";
                MessageBox.Show($"Помилка підключення: {ex.Message}", "Error");
            }
        }

        private async Task SendPlayerConnect(string name)
        {
            var message = new
            {
                type = "player_connect",
                data = new { playerName = name }
            };
            await SendJson(message);
        }

        private async void SendMove(string from, string to)
        {
            var message = new
            {
                type = "chess_move",
                data = new
                {
                    playerId = myPlayerId,
                    from = from,
                    to = to
                }
            };
            await SendJson(message);
        }

        private async Task SendJson(object message)
        {
            if (ws == null || ws.State != WebSocketState.Open)
                return;

            string json = JsonSerializer.Serialize(message);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }

        private async Task ListenToServer()
        {
            byte[] buffer = new byte[1024 * 4];

            try
            {
                while (ws != null && ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer),
                        CancellationToken.None
                    );

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        HandleServerMessage(json);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            StatusText.Text = "Сервер закрив з'єднання";
                        });
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Помилка: {ex.Message}";
                });
            }
        }

        private void HandleServerMessage(string json)
        {
            try
            {
                // Виводимо сирий JSON в Output Window для відладки взаємодії
                System.Diagnostics.Debug.WriteLine($"[SERVER JSON]: {json}");

                if (string.IsNullOrWhiteSpace(json) || !json.Trim().StartsWith("{"))
                {
                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = $"Отримано не-JSON текст: {json}";
                    });
                    return;
                }

                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                if (!root.TryGetProperty("type", out var typeProp) || typeProp.GetString() == null) 
                    return;

                string type = typeProp.GetString()!;

                // Перевіряємо наявність властивості "data"
                if (!root.TryGetProperty("data", out var data)) 
                    return;

                Dispatcher.Invoke(() =>
                {
                    switch (type)
                    {
                        case "connect_success":
                            if (data.ValueKind == JsonValueKind.Object)
                            {
                                myPlayerId = data.TryGetProperty("playerId", out var idProp) ? idProp.GetString() : null;
                                
                                if (data.TryGetProperty("color", out var colorPropConn))
                                {
                                    string rawColor = colorPropConn.GetString()?.ToLower() ?? "white";
                                    myColor = rawColor;

                                    if (rawColor == "white")
                                        StatusText.Text = "Ви граєте за БІЛІ ⚪";
                                    else if (rawColor == "black")
                                        StatusText.Text = "Ви граєте за ЧОРНІ ⚫";
                                    else
                                        StatusText.Text = $"Ви граєте за {rawColor}";
                                }
                                else
                                {
                                    StatusText.Text = "Успішно підключено! Очікування старту гри...";
                                }
                            }
                            break;

                        // ДОДАЄМО ОБРОБКУ ПОМИЛКИ НІКНЕЙМУ
                        case "connect_error":
                            if (data.ValueKind == JsonValueKind.Object)
                            {
                                string errorType = data.TryGetProperty("error", out var errProp) ? errProp.GetString() ?? "" : "";
                                string message = data.TryGetProperty("message", out var errorMsgProp) ? errorMsgProp.GetString() ?? "" : "";

                                if (errorType == "NameOccupied" || message.Contains("зайнятий") || message.Contains("Occupied"))
                                {
                                    StatusText.Text = "❌ Помилка: Нікнейм зайнятий";
                                    
                                    MessageBox.Show(
                                        "Цей нікнейм уже зайнятий іншим гравцем! Будь ласка, введіть інше ім'я.", 
                                        "Помилка підключення", 
                                        MessageBoxButton.OK, 
                                        MessageBoxImage.Warning
                                    );

                                    // Повертаємо кнопки в активний стан, щоб гравець міг спробувати ще раз
                                    ConnectButton.IsEnabled = true;
                                    ResignButton.IsEnabled = false;
                                }
                                else
                                {
                                    // На випадок іншої помилки підключення від сервера
                                    MessageBox.Show($"Помилка підключення: {message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    ConnectButton.IsEnabled = true;
                                }
                            }
                            break;

                        case "player_resigned":
                            if (data.ValueKind == JsonValueKind.Object)
                            {
                                string leaverId = data.TryGetProperty("leaverId", out var lId) ? lId.GetString() ?? "" : "";
                                string leaverName = data.TryGetProperty("leaverName", out var lName) ? lName.GetString() ?? "Гравець" : "Гравець";
                                string winnerName = data.TryGetProperty("winnerName", out var wName) ? wName.GetString() ?? "Суперник" : "Суперник";

                                // Порівнюємо по імені поточного клієнта
                                bool iAmTheLeaver = !string.IsNullOrEmpty(myPlayerName) && myPlayerName.Equals(leaverName, StringComparison.OrdinalIgnoreCase);

                                if (iAmTheLeaver)
                                {
                                    // Вікно для того, хто здався/вийшов
                                    MessageBox.Show(
                                        $"Ви здалися. Перемога за '{winnerName}'!", 
                                        "Кінець партії", 
                                        MessageBoxButton.OK, 
                                        MessageBoxImage.Information
                                    );
                                    
                                    Application.Current.Shutdown();
                                }
                                else
                                {
                                    // Вікно для того, хто переміг (залишився в грі)
                                    MessageBox.Show(
                                        "Ви перемогли! Гарна гра;)", 
                                        "Партію завершено", 
                                        MessageBoxButton.OK, 
                                        MessageBoxImage.Asterisk
                                    );
                                    
                                    Application.Current.Shutdown();
                                }
                            }
                            break;

                        case "assign_color":
                            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("color", out var colorPropAssign))
                            {
                                string rawColor = colorPropAssign.GetString()?.ToLower() ?? "white";
                                myColor = rawColor; 

                                if (rawColor == "white")
                                    StatusText.Text = "Ви БІЛІ ⚪";
                                else if (rawColor == "black")
                                    StatusText.Text = "Ви ЧОРНІ ⚫";
                            }
                            break;
                            
                        case "move_made":
                            try
                            {
                                if (data.ValueKind == JsonValueKind.Object)
                                {
                                    string fromPosition = data.TryGetProperty("from", out var fProp) ? fProp.GetString() ?? "" : "";
                                    string toPosition = data.TryGetProperty("to", out var tProp) ? tProp.GetString() ?? "" : "";
                                    
                                    currentCheckColor = "none";
                                    if (data.TryGetProperty("check", out var checkProp))
                                    {
                                        currentCheckColor = checkProp.GetString() ?? "none";
                                    }

                                    MovePieceOnBoard(fromPosition, toPosition);
                                    StatusText.Text = $"Хід: {fromPosition} → {toPosition}";

                                    if (data.TryGetProperty("status", out var statusProp))
                                    {
                                        string gameStatus = statusProp.GetString() ?? "active";

                                        if (gameStatus != "active" && gameStatus != "none")
                                        {
                                            if (gameStatus == "white_win")
                                            {
                                                MessageBox.Show("МАТ! Перемога за БІЛИМИ!", "Кінець партії", MessageBoxButton.OK, MessageBoxImage.Information);
                                                Application.Current.Shutdown();
                                                return; 
                                            }
                                            else if (gameStatus == "black_win")
                                            {
                                                MessageBox.Show("МАТ! Беззаперечна перемога ЧОРНИХ!", "Кінець партії", MessageBoxButton.OK, MessageBoxImage.Information);
                                                Application.Current.Shutdown();
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                StatusText.Text = $"Помилка обробки ходу: {ex.Message}";
                            }
                            break;

                        case "valid_moves":
                            ResetSquareBackgrounds(); 

                            // Якщо сервер прислав рядок з помилкою замість об'єкта/масиву
                            if (data.ValueKind == JsonValueKind.String)
                            {
                                StatusText.Text = $"Інфо від сервера: {data.GetString()}";
                                break;
                            }

                            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("moves", out var movesProp))
                            {
                                foreach (var moveElement in movesProp.EnumerateArray())
                                {
                                    if (moveElement.ValueKind == JsonValueKind.Object)
                                    {
                                        string targetPos = moveElement.TryGetProperty("to", out var toP) ? toP.GetString() ?? "" : "";
                                        bool isCapture = false;
                                        if (moveElement.TryGetProperty("isCapture", out var capProp))
                                            isCapture = capProp.GetBoolean();

                                        if (!string.IsNullOrEmpty(targetPos))
                                            HighlightSquareAsValid(targetPos, isCapture);
                                    }
                                    else if (moveElement.ValueKind == JsonValueKind.String)
                                    {
                                        string targetPos = moveElement.GetString() ?? "";
                                        if (!string.IsNullOrEmpty(targetPos))
                                            HighlightSquareAsValid(targetPos, false);
                                    }
                                }
                            }
                            else if (data.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var moveElement in data.EnumerateArray())
                                {
                                    string targetPos = moveElement.GetString() ?? "";
                                    if (!string.IsNullOrEmpty(targetPos))
                                        HighlightSquareAsValid(targetPos, false);
                                }
                            }
                            break;

                        case "your_turn":
                            StatusText.Text = "✅ Ваш хід!";
                            break;

                        case "invalid_move":
                            StatusText.Text = "❌ Нелегальний хід!";
                            break;

                        case "game_over":
                            string winner = "Unknown";
                            if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("winner", out var winProp))
                            {
                                winner = winProp.GetString() ?? "Unknown";
                            }
                            StatusText.Text = $"Гра закінчена! Переможець: {winner}";
                            MessageBox.Show($"Гра закінчена!\nПереможець: {winner}", "Game Over");
                            break;

                        default:
                            StatusText.Text = $"Подія від сервера: {type}";
                            break;
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Помилка парсингу: {ex.Message}";
                });
            }
        }

        private void HighlightSquareAsValid(string pos, bool isCapture)
        {
            var (row, col) = ParsePosition(pos);
            if (row >= 0 && row < 8 && col >= 0 && col < 8)
            {
                if (squares[row, col] != null)
                {
                    if (isCapture)
                    {
                        squares[row, col].Background = new SolidColorBrush(Color.FromRgb(245, 140, 140));
                    }
                    else
                    {
                        squares[row, col].Background = new SolidColorBrush(Color.FromRgb(150, 200, 240));
                    }
                }
            }
        }
        
        private void MovePieceOnBoard(string from, string to)
        {
            var (fromRow, fromCol) = ParsePosition(from);
            var (toRow, toCol) = ParsePosition(to);

            if (fromRow < 0 || fromRow >= 8 || fromCol < 0 || fromCol >= 8 ||
                toRow < 0 || toRow >= 8 || toCol < 0 || toCol >= 8) return;

            Button fromButton = squares[fromRow, fromCol];
            Button toButton = squares[toRow, toCol];

            if (fromButton?.Content is Image pieceImage)
            {
                string? imagePath = pieceImage.Tag?.ToString(); 
                if (!string.IsNullOrEmpty(imagePath))
                {
                    AnimateAndMovePiece(fromButton, toButton, imagePath);
                }
            }
        }

        private void AnimateAndMovePiece(Button fromButton, Button toButton, string imagePath)
        {
            if (fromButton == null || toButton == null) return;

            Image animatedPiece = new Image();
            animatedPiece.Source = new BitmapImage(new Uri($"pack://application:,,,/{imagePath}"));
            animatedPiece.Width = Math.Max(fromButton.ActualWidth - 10, 10);
            animatedPiece.Height = Math.Max(fromButton.ActualHeight - 10, 10);

            try
            {
                Point startPoint = fromButton.TransformToVisual(AnimationLayer).Transform(new Point(0, 0));
                Point endPoint = toButton.TransformToVisual(AnimationLayer).Transform(new Point(0, 0));

                Canvas.SetLeft(animatedPiece, startPoint.X);
                Canvas.SetTop(animatedPiece, startPoint.Y);

                AnimationLayer.Children.Add(animatedPiece);
                fromButton.Content = null; 

                TimeSpan duration = TimeSpan.FromSeconds(0.25);
                DoubleAnimation animX = new DoubleAnimation(startPoint.X, endPoint.X, duration);
                DoubleAnimation animY = new DoubleAnimation(startPoint.Y, endPoint.Y, duration);

                animX.Completed += (s, e) =>
                {
                    AnimationLayer.Children.Remove(animatedPiece); 

                    Image finalImage = new Image();
                    finalImage.Source = new BitmapImage(new Uri($"pack://application:,,,/{imagePath}"));
                    finalImage.Tag = imagePath;
                    finalImage.Margin = new Thickness(5);

                    toButton.Content = finalImage;
                    ResetSquareBackgrounds();
                };

                animatedPiece.BeginAnimation(Canvas.LeftProperty, animX);
                animatedPiece.BeginAnimation(Canvas.TopProperty, animY);
            }
            catch
            {
                // Резервний варіант без анімації
                fromButton.Content = null;
                Image finalImage = new Image();
                finalImage.Source = new BitmapImage(new Uri($"pack://application:,,,/{imagePath}"));
                finalImage.Tag = imagePath;
                finalImage.Margin = new Thickness(5);
                toButton.Content = finalImage;
                ResetSquareBackgrounds();
            }
        }

        private (int row, int col) ParsePosition(string pos)
        {
            if (string.IsNullOrEmpty(pos) || pos.Length < 2) return (-1, -1);
            int col = pos[0] - 'a';
            int row = 8 - (pos[1] - '0');
            return (row, col);
        }

        private async void ResignButton_Click(object sender, RoutedEventArgs e)
        {
            var message = new
            {
                type = "resign",
                data = new { playerId = myPlayerId }
            };
            await SendJson(message);
            StatusText.Text = "Кінець гри!";
        }
    }
}