using System;
using ChessServer.Models;

namespace ChessServer.Pieces
{
    public class Rook : ChessPiece
    {
        public Rook(PieceColor color, int row, int col) : base(color, row, col) { }

        public override bool CanMove(int targetRow, int targetCol, ChessPiece?[,] board)
        {
            // Тура ходить тільки по горизонталі або вертикалі
            if (Row != targetRow && Column != targetCol) return false;

            int rowStep = Math.Sign(targetRow - Row);
            int colStep = Math.Sign(targetCol - Column);

            int currentRow = Row + rowStep;
            int currentCol = Column + colStep;

            // Йдемо по лінії, поки не досягнемо цільової клітинки
            while (currentRow != targetRow || currentCol != targetCol)
            {
                // Якщо на шляху є фігура — рух заблоковано
                if (board[currentRow, currentCol] != null) return false;

                // Захист від виходу за межі
                if (currentRow < 0 || currentRow > 7 || currentCol < 0 || currentCol > 7) return false;

                currentRow += rowStep;
                currentCol += colStep;
            }

            // Перевіряємо фігуру на кінцевій клітинці
            var targetPiece = board[targetRow, targetCol];
            return targetPiece == null || targetPiece.Color != this.Color;
        }
    }
}