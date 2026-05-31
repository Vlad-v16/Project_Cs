using System;
using ChessServer.Models;

namespace ChessServer.Pieces
{ 
    public class Pawn : ChessPiece
    {
        public Pawn(PieceColor color, int row, int col) : base(color, row, col) { }

        public override bool CanMove(int targetRow, int targetCol, ChessPiece?[,] board)
        {
            int direction = (Color == PieceColor.White) ? -1 : 1;
            int startRow = (Color == PieceColor.White) ? 6 : 1;

            int dRow = targetRow - Row;
            int dCol = Math.Abs(targetCol - Column);

            // 1. Звичайний хід вперед на 1 клітинку (тільки на порожнє місце)
            if (dCol == 0 && dRow == direction)
            {
                return board[targetRow, targetCol] == null;
            }

            // 2. Перший подвійний хід вперед (обидві клітинки мають бути порожніми)
            if (dCol == 0 && Row == startRow && dRow == 2 * direction)
            {
                return board[targetRow, targetCol] == null && board[Row + direction, Column] == null;
            }

            // 3. Взяття ворожої фігури по діагоналі
            if (dCol == 1 && dRow == direction)
            {
                var targetPiece = board[targetRow, targetCol];
                // Фігура обов'язково має там бути і вона повинна бути ворожою
                return targetPiece != null && targetPiece.Color != this.Color;
            }

            return false;
        }
    }
}