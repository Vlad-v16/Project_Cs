using System;
using ChessServer.Models;

namespace ChessServer.Pieces
{
    public class Knight : ChessPiece
    {
        public Knight(PieceColor color, int row, int col) : base(color, row ,col) {}

        public override bool CanMove(int targetRow, int targetCol, ChessPiece?[,] board)
        {
            int dRow = Math.Abs(targetRow - Row);
            int dCol = Math.Abs(targetCol - Column);

            // Перевірка ходу літерою «Г»
            if (!((dRow == 2 && dCol == 1) || (dRow == 1 && dCol == 2))) return false;

            // Безпечно перевіряємо цільову клітинку
            var targetPiece = board[targetRow, targetCol];
            return targetPiece == null || targetPiece.Color != this.Color;
        }
    }
}