using System;
using ChessServer.Models;

namespace ChessServer.Pieces
{
    public class Bishop : ChessPiece
    {
        public Bishop(PieceColor color, int row, int col) : base(color, row, col) { }

        public override bool CanMove(int targetRow, int targetCol, ChessPiece?[,] board)
        {
            // Якщо хід не по діагоналі — відразу false
            if (Math.Abs(Row - targetRow) != Math.Abs(Column - targetCol)) return false;

            int rowStep = Math.Sign(targetRow - Row);
            int colStep = Math.Sign(targetCol - Column);

            int currentRow = Row + rowStep;
            int currentCol = Column + colStep;

            // Перевіряємо, чи вільна траєкторія (не враховуючи кінцеву клітинку)
            while (currentRow != targetRow && currentCol != targetCol)
            {
                if (board[currentRow, currentCol] != null) return false;
                currentRow += rowStep;
                currentCol += colStep;
            }

            // Перевіряємо фігуру на кінцевій клітинці
            var targetPiece = board[targetRow, targetCol];
            
            // Безпечна перевірка: якщо клітинка порожня АБО там ворог
            return targetPiece == null || targetPiece.Color != this.Color;
        }
    }
}