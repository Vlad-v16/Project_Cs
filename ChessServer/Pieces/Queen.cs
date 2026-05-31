using System;
using ChessServer.Models;

namespace ChessServer.Pieces
{
    public class Queen : ChessPiece
    {
        public Queen(PieceColor color, int row, int col) : base(color, row, col) { }

        public override bool CanMove(int targetRow, int targetCol, ChessPiece?[,] board)
        {
            bool IsRookMove = (Row == targetRow || Column == targetCol);
            bool IsBishopMove = Math.Abs(Row - targetRow) == Math.Abs(Column - targetCol);

            // Якщо хід не по прямій і не по діагоналі — це нелегальний хід
            if (!IsRookMove && !IsBishopMove) return false;

            int rowStep = Math.Sign(targetRow - Row);
            int colStep = Math.Sign(targetCol - Column);

            int currentRow = Row + rowStep;
            int currentCol = Column + colStep;

            // Йдемо доки не впремося в цільову клітинку хоча б однією координатою
            // (оскільки хід валідний, вони прийдуть туди одночасно або одна з них занулиться, якщо хід прямий)
            while (currentRow != targetRow || currentCol != targetCol)
            {
                // Якщо на шляху зустріли фігуру — шлях перекритий
                if (board[currentRow, currentCol] != null) return false;

                // Якщо раптом вийшли за межі дошки
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