using System;
using ChessServer.Models;

namespace ChessServer.Pieces
{
    public class King : ChessPiece
    {
        public King(PieceColor color, int row, int col) : base(color, row, col) { }

        public override bool CanMove(int targetRow, int targetCol, ChessPiece?[,] board)
        {
            // 1. Перевірка на рокіровку
            if (!HasMoved && Math.Abs(targetCol - Column) == 2 && targetRow == Row)
            {
                int rookCol = (targetCol > Column) ? 7 : 0; // 7 — королівська (праворуч), 0 — ферзева (ліворуч)
                
                // Безпечно перевіряємо, чи є об'єкт у клітинці турою
                if (board[Row, rookCol] is Rook rook)
                {
                    // Перевіряємо, що тура НАШОГО кольору і вона не ходила
                    if (rook.Color == this.Color && !rook.HasMoved)
                    {
                        // Перевіряємо, чи вільна дорога між королем і турою
                        int step = (targetCol > Column) ? 1 : -1;
                        for (int c = Column + step; c != rookCol; c += step)
                        {
                            if (board[Row, c] != null) return false; // Шлях перекритий фігурою
                        }
                        
                        return true; 
                    }
                }
            }

            // 2. БАЗОВИЙ ХІД КОРОЛЯ (на 1 клітинку в будь-який бік)
            int dRow = Math.Abs(Row - targetRow);
            int dCol = Math.Abs(Column - targetCol);

            // Якщо хід більше ніж на 1 клітинку або на місці — це нелегальний хід
            if (dRow > 1 || dCol > 1 || (dRow == 0 && dCol == 0)) return false;

            // Не можна ставати на фігуру власного кольору
            var targetPiece = board[targetRow, targetCol];
            return targetPiece == null || targetPiece.Color != this.Color;
        }
    }
}