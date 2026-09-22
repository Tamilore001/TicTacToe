using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TicTacToe.Pages;

public class IndexModel : PageModel
{
    private const string BoardSessionKey = "TicTacToe_Board";
    private const string DifficultySessionKey = "TicTacToe_Difficulty";
    private const string EmptyBoard = "---------"; // '-' = empty cell
    private const string DefaultDifficulty = "Hard";

    public string[] Board { get; set; } = new string[9];
    public string Message { get; set; } = "";
    public bool GameOver { get; set; }
    public string Difficulty { get; set; } = DefaultDifficulty;

    private static readonly int[][] WinningLines =
    {
        new[] { 0, 1, 2 }, new[] { 3, 4, 5 }, new[] { 6, 7, 8 }, // rows
        new[] { 0, 3, 6 }, new[] { 1, 4, 7 }, new[] { 2, 5, 8 }, // columns
        new[] { 0, 4, 8 }, new[] { 2, 4, 6 }                    // diagonals
    };

    public void OnGet()
    {
        string boardStr = HttpContext.Session.GetString(BoardSessionKey) ?? EmptyBoard;
        ApplyState(boardStr);
    }

    public IActionResult OnPost(int position)
    {
        string boardStr = HttpContext.Session.GetString(BoardSessionKey) ?? EmptyBoard;

        // Ignore the click if the game already ended or the cell is taken
        if (!IsGameOver(boardStr) && position >= 0 && position < 9 && boardStr[position] == '-')
        {
            // Human always plays X
            var chars = boardStr.ToCharArray();
            chars[position] = 'X';
            boardStr = new string(chars);

            // If the human didn't just win or fill the board, let the AI respond as O
            if (GetWinner(boardStr) == null && !IsBoardFull(boardStr))
            {
                int aiMove = GetAiMove(boardStr);
                if (aiMove != -1)
                {
                    chars = boardStr.ToCharArray();
                    chars[aiMove] = 'O';
                    boardStr = new string(chars);
                }
            }

            HttpContext.Session.SetString(BoardSessionKey, boardStr);
        }

        // Redirect back to a clean "/" so no stale ?handler=... query string
        // can hijack the next form submission.
        return RedirectToPage();
    }

    public IActionResult OnPostReset()
    {
        HttpContext.Session.SetString(BoardSessionKey, EmptyBoard);
        return RedirectToPage();
    }

    public IActionResult OnPostSetDifficulty(string difficulty)
    {
        if (difficulty is "Easy" or "Medium" or "Hard")
        {
            HttpContext.Session.SetString(DifficultySessionKey, difficulty);
        }

        // Changing difficulty mid-game would mix strategies, so start fresh
        HttpContext.Session.SetString(BoardSessionKey, EmptyBoard);
        return RedirectToPage();
    }

    // Picks the AI's move based on the current difficulty level
    private int GetAiMove(string boardStr)
    {
        string difficulty = GetDifficulty();

        return difficulty switch
        {
            // Always random - easy to beat
            "Easy" => GetRandomMove(boardStr),
            // Coin flip between random and optimal each move
            "Medium" => Random.Shared.Next(2) == 0 ? GetRandomMove(boardStr) : GetBestMove(boardStr),
            // Always optimal - unbeatable
            _ => GetBestMove(boardStr),
        };
    }

    private string GetDifficulty()
    {
        return HttpContext.Session.GetString(DifficultySessionKey) ?? DefaultDifficulty;
    }

    private static int GetRandomMove(string boardStr)
    {
        var emptyCells = new List<int>();
        for (int i = 0; i < 9; i++)
        {
            if (boardStr[i] == '-')
            {
                emptyCells.Add(i);
            }
        }

        return emptyCells.Count == 0 ? -1 : emptyCells[Random.Shared.Next(emptyCells.Count)];
    }

    private void ApplyState(string boardStr)
    {
        Difficulty = GetDifficulty();

        for (int i = 0; i < 9; i++)
        {
            Board[i] = boardStr[i] == '-' ? string.Empty : boardStr[i].ToString();
        }

        string? winner = GetWinner(boardStr);
        if (winner == "X")
        {
            Message = "You win!";
            GameOver = true;
        }
        else if (winner == "O")
        {
            Message = "The AI wins!";
            GameOver = true;
        }
        else if (IsBoardFull(boardStr))
        {
            Message = "It's a draw!";
            GameOver = true;
        }
        else
        {
            Message = "Your turn!";
            GameOver = false;
        }
    }

    private bool IsGameOver(string boardStr) => GetWinner(boardStr) != null || IsBoardFull(boardStr);

    private bool IsBoardFull(string boardStr) => !boardStr.Contains('-');

    private string? GetWinner(string boardStr)
    {
        foreach (var line in WinningLines)
        {
            char a = boardStr[line[0]];
            char b = boardStr[line[1]];
            char c = boardStr[line[2]];

            if (a != '-' && a == b && b == c)
            {
                return a.ToString();
            }
        }

        return null;
    }

    // Picks the optimal move for O using minimax
    private int GetBestMove(string boardStr)
    {
        char[] board = boardStr.ToCharArray();
        int bestScore = int.MinValue;
        int bestMove = -1;

        for (int i = 0; i < 9; i++)
        {
            if (board[i] == '-')
            {
                board[i] = 'O';
                int score = Minimax(board, 0, false);
                board[i] = '-';

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = i;
                }
            }
        }

        return bestMove;
    }

    // isMaximizing = true means it's O's (AI's) turn to move in this branch
    private int Minimax(char[] board, int depth, bool isMaximizing)
    {
        string boardStr = new string(board);
        string? winner = GetWinner(boardStr);

        if (winner == "O") return 10 - depth;  // AI win: prefer faster wins
        if (winner == "X") return depth - 10;  // Human win: prefer slower losses
        if (IsBoardFull(boardStr)) return 0;

        if (isMaximizing)
        {
            int best = int.MinValue;
            for (int i = 0; i < 9; i++)
            {
                if (board[i] == '-')
                {
                    board[i] = 'O';
                    best = Math.Max(best, Minimax(board, depth + 1, false));
                    board[i] = '-';
                }
            }
            return best;
        }
        else
        {
            int best = int.MaxValue;
            for (int i = 0; i < 9; i++)
            {
                if (board[i] == '-')
                {
                    board[i] = 'X';
                    best = Math.Min(best, Minimax(board, depth + 1, true));
                    board[i] = '-';
                }
            }
            return best;
        }
    }
}