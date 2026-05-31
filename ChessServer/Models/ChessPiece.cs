namespace ChessServer.Models
{
    public enum PieceColor { White, Black }

    public abstract class ChessPiece
    {
        public PieceColor Color { get; }
        public int Row { get; set; }
        public int Column { get; set; }

        protected ChessPiece(PieceColor color, int row, int col)
        {
            Color = color;
            Row = row;
            Column = col;
        }
        
        public bool HasMoved { get; set; } = false;

        public abstract bool CanMove(int targetRow, int targetCol, ChessPiece[,] board);
    }
}