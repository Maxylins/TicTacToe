using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.EntityFrameworkCore;
using SkiaSharp;

namespace Tic_Tac_Toe;

// 1. МОДЕЛЬ ДАННЫХ И КОНТЕКСТ БАЗЫ ДАННЫХ
public class User
{
    [Key] public int Id { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public int Wins { get; set; } = 0;
}

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; } = null!;
    protected override void OnConfiguring(DbContextOptionsBuilder options) => options.UseSqlite("Data Source=game.db");
}

// 2. ЛОГИКА ОКНА И ИГРЫ
public partial class MainWindow : Window
{
    private User? _currentUser;
    private string[] _board = Enumerable.Repeat("", 9).ToArray();
    private bool _gameActive = false;

    public MainWindow()
    {
        InitializeComponent();

        // Автоматически создаем базу данных при старте
        using var db = new AppDbContext();
        db.Database.EnsureCreated();
    }

    // ЛОГИКА АВТОРИЗАЦИИ И РЕГИСТРАЦИИ
    private void OnAuthClick(object? sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        string mode = button?.CommandParameter?.ToString() ?? "";
        string username = txtUsername.Text ?? "";
        string password = txtPassword.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            lblStatus.Text = "Заполните все поля!";
            return;
        }

        using var db = new AppDbContext();

        if (mode == "reg")
        {
            if (db.Users.Any(u => u.Username == username))
            {
                lblStatus.Text = "Этот логин уже занят!";
                return;
            }
            db.Users.Add(new User { Username = username, Password = password });
            db.SaveChanges();
            lblStatus.Text = "Регистрация успешна! Теперь войдите.";
        }
        else if (mode == "login")
        {
            _currentUser = db.Users.FirstOrDefault(u => u.Username == username && u.Password == password);
            if (_currentUser != null)
            {
                // Переключаем экраны
                authPanel.IsVisible = false;
                gamePanel.IsVisible = true;

                UpdateProfileGrid();
                StartNewGame();
            }
            else
            {
                lblStatus.Text = "Неверный логин или пароль!";
            }
        }
    }

    // ЛОГИКА ИГРЫ
    private string _difficulty = "medium";
    private readonly Random _random = new();

    // Событие переключения радио-кнопок
    private void OnDifficultyChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.IsChecked == true)
        {
            _difficulty = rb.Name switch
            {
                "rbEasy" => "easy",
                "rbMedium" => "medium",
                "rbHard" => "hard",
                _ => "medium"
            };
        }
    }

    private void OnMoveClick(object? sender, RoutedEventArgs e)
    {
        if (!_gameActive || sender is not Button button || button.CommandParameter is not string indexStr) return;

        int idx = int.Parse(indexStr);
        if (_board[idx] != "") return;

        // --- 1. ХОД ИГРОКА (X) ---
        _board[idx] = "X";
        button.Content = "X";

        if (CheckWin("X")) { EndGame(true); return; }
        if (_board.All(cell => cell != "")) { EndGame(false, true); return; }

        // --- 2. ПОШАГОВЫЙ АЛГОРИТМ БОТА (O) С УЧЕТОМ СЛОЖНОСТИ ---
        _gameActive = false;

        int botChoice = FindBestMove();

        if (botChoice != -1)
        {
            _board[botChoice] = "O";
            var btn = gridBoard.Children.OfType<Button>().FirstOrDefault(b => b.CommandParameter?.ToString() == botChoice.ToString());
            if (btn != null) btn.Content = "O";

            if (CheckWin("O")) { EndGame(false); return; }
        }

        if (_board.All(cell => cell != "")) { EndGame(false, true); return; }
        _gameActive = true;
    }

    private int FindBestMove()
    {
        // Собираем все свободные клетки на случай рандомного хода
        var emptyCells = _board.Select((val, i) => val == "" ? i : -1).Where(i => i != -1).ToList();
        if (!emptyCells.Any()) return -1;

        // ЛЕГКАЯ: Полный рандом
        if (_difficulty == "easy")
        {
            return emptyCells[_random.Next(emptyCells.Count)];
        }

        // СРЕДНЯЯ: 50% шанс рандома, 50% умного алгоритма
        if (_difficulty == "medium")
        {
            if (_random.Next(0, 100) < 50)
            {
                return emptyCells[_random.Next(emptyCells.Count)];
            }
        }

        // СЛОЖНАЯ (или если на средней выпали «умные» 50%): Логический пошаговый алгоритм
        int[][] winLines = [[0, 1, 2], [3, 4, 5], [6, 7, 8], [0, 3, 6], [1, 4, 7], [2, 5, 8], [0, 4, 8], [2, 4, 6]];

        // 1. Пытаемся выиграть
        foreach (var line in winLines)
        {
            if (_board[line[0]] == "O" && _board[line[1]] == "O" && _board[line[2]] == "") return line[2];
            if (_board[line[0]] == "O" && _board[line[2]] == "O" && _board[line[1]] == "") return line[1];
            if (_board[line[1]] == "O" && _board[line[2]] == "O" && _board[line[0]] == "") return line[0];
        }

        // 2. Защищаемся
        foreach (var line in winLines)
        {
            if (_board[line[0]] == "X" && _board[line[1]] == "X" && _board[line[2]] == "") return line[2];
            if (_board[line[0]] == "X" && _board[line[2]] == "X" && _board[line[1]] == "") return line[1];
            if (_board[line[1]] == "X" && _board[line[2]] == "X" && _board[line[0]] == "") return line[0];
        }

        // 3. Центр
        if (_board[4] == "") return 4;

        // 4. Углы
        int[] corners = [0, 2, 6, 8];
        foreach (int corner in corners)
        {
            if (_board[corner] == "") return corner;
        }

        // 5. Любой остаток (если алгоритм дошел сюда)
        return emptyCells[_random.Next(emptyCells.Count)];
    }

    private bool CheckWin(string symbol)
    {
        int[][] winLines = [[0, 1, 2], [3, 4, 5], [6, 7, 8], [0, 3, 6], [1, 4, 7], [2, 5, 8], [0, 4, 8], [2, 4, 6]];
        return winLines.Any(line => _board[line[0]] == symbol && _board[line[1]] == symbol && _board[line[2]] == symbol);
    }


    private void EndGame(bool isPlayerWin, bool isDraw = false)
    {
        _gameActive = false;

        if (isDraw)
        {
            lblStatus.Text = "Ничья!";
        }
        else if (isPlayerWin)
        {
            lblStatus.Text = "Вы победили!";
            if (_currentUser != null)
            {
                using var db = new AppDbContext();
                var user = db.Users.Find(_currentUser.Id);
                if (user != null)
                {
                    user.Wins++;
                    db.SaveChanges();
                    _currentUser.Wins = user.Wins;
                    UpdateProfileGrid();
                }
            }
        }
        else
        {
            lblStatus.Text = "Победил Компьютер (O)!";
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e) => StartNewGame();

    private void StartNewGame()
    {
        _board = Enumerable.Repeat("", 9).ToArray();
        foreach (var child in gridBoard.Children)
            if (child is Button btn) btn.Content = "";

        lblStatus.Text = "Ваш ход (X)";
        _gameActive = true;
    }

    private void UpdateProfileGrid()
    {
        if (_currentUser != null)
        {
            lblUserInfo.Text = $"Игрок: {_currentUser.Username}  |  Побед: {_currentUser.Wins}";
        }
    }
}