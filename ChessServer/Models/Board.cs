using System;
using System.Collections.Generic;
using ChessServer.Pieces;

namespace ChessServer.Models
{
    public class Board
    {
        public ChessPiece[,] Pieces = new ChessPiece[8, 8];

        public PieceColor CurrentTurn { get; private set; } = PieceColor.White;

        public void SwitchTurn()
        {
            CurrentTurn = (CurrentTurn == PieceColor.White) ? PieceColor.Black : PieceColor.White;
        }

        public Board()
        {
            SetupBoard();
        }

        private void SetupBoard()
        {
            // Чорні фігури
            for (int i = 0; i < 8; i++)
            {
                Pieces[1, i] = new Pawn(PieceColor.Black, 1, i);
                Pieces[6, i] = new Pawn(PieceColor.White, 6, i);
            }
            Pieces[0, 0] = new Rook(PieceColor.Black, 0, 0);
            Pieces[0, 7] = new Rook(PieceColor.Black, 0, 7);
            Pieces[0, 1] = new Knight(PieceColor.Black, 0, 1);
            Pieces[0, 6] = new Knight(PieceColor.Black, 0, 6);
            Pieces[0, 2] = new Bishop(PieceColor.Black, 0, 2);
            Pieces[0, 5] = new Bishop(PieceColor.Black, 0, 5);
            Pieces[0, 3] = new Queen(PieceColor.Black, 0, 3);
            Pieces[0, 4] = new King(PieceColor.Black, 0, 4);

            // Білі фігури
            Pieces[7, 0] = new Rook(PieceColor.White, 7, 0);
            Pieces[7, 7] = new Rook(PieceColor.White, 7, 7);
            Pieces[7, 1] = new Knight(PieceColor.White, 7, 1);
            Pieces[7, 6] = new Knight(PieceColor.White, 7, 6);
            Pieces[7, 2] = new Bishop(PieceColor.White, 7, 2);
            Pieces[7, 5] = new Bishop(PieceColor.White, 7, 5);
            Pieces[7, 3] = new Queen(PieceColor.White, 7, 3);
            Pieces[7, 4] = new King(PieceColor.White, 7, 4);
        }

        public struct MoveResult 
        {
            public bool Success;
            public bool IsCastling;
            public int RookFromCol;
            public int RookToCol;
        }

        public MoveResult TryMove(int fromRow, int fromCol, int toRow, int toCol)
        {
            var piece = Pieces[fromRow, fromCol];
            var result = new MoveResult { Success = false, IsCastling = false };

            if (piece == null || piece.Color != CurrentTurn) return result;

            if (piece.CanMove(toRow, toCol, Pieces))
            {
                // Заборона рокіровки під час шаху
                if (piece is King && Math.Abs(toCol - fromCol) == 2 && IsInCheck(piece.Color))
                {
                    return result; 
                }

                // Захист від шаху власному королю
                if (WouldMoveResultInCheck(fromRow, fromCol, toRow, toCol, piece.Color))
                {
                    return result; 
                }

                // Рокіровка
                if (piece is King && Math.Abs(toCol - fromCol) == 2)
                {
                    result.IsCastling = true;
                    result.RookFromCol = (toCol > fromCol) ? 7 : 0;
                    result.RookToCol = (toCol > fromCol) ? 5 : 3;

                    var rook = Pieces[fromRow, result.RookFromCol];
                    if (rook != null)
                    {
                        Pieces[fromRow, result.RookToCol] = rook;
                        Pieces[fromRow, result.RookFromCol] = null!;
                        rook.Column = result.RookToCol;
                        rook.HasMoved = true;
                    }
                }

                // Основний хід фігури
                Pieces[toRow, toCol] = piece;
                Pieces[fromRow, fromCol] = null!;
                piece.Row = toRow;
                piece.Column = toCol;
                piece.HasMoved = true;

                SwitchTurn();
                result.Success = true;
            }
            return result;
        }

        public bool IsInCheck(PieceColor color)
        {
            int kingRow = -1;
            int kingCol = -1;

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    if (Pieces[r, c] is King && Pieces[r, c].Color == color)
                    {
                        kingRow = r;
                        kingCol = c;
                        break;
                    }
                }
                if (kingRow != -1) break;
            }

            if (kingRow == -1) return false;

            for (int r = 0; r < 8; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    var piece = Pieces[r, c];
                    if (piece != null && piece.Color != color)
                    {
                        if (piece.CanMove(kingRow, kingCol, Pieces))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public bool IsCheckmate(PieceColor color)
        {
            if (!IsInCheck(color)) return false;

            for (int fromRow = 0; fromRow < 8; fromRow++)
            {
                for (int fromCol = 0; fromCol < 8; fromCol++)
                {
                    var piece = Pieces[fromRow, fromCol];
                    if (piece != null && piece.Color == color)
                    {
                        for (int toRow = 0; toRow < 8; toRow++)
                        {
                            for (int toCol = 0; toCol < 8; toCol++)
                            {
                                if (piece.CanMove(toRow, toCol, Pieces))
                                {
                                    if (!WouldMoveResultInCheck(fromRow, fromCol, toRow, toCol, color))
                                    {
                                        return false; 
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return true;
        }  

        private bool WouldMoveResultInCheck(int fromRow, int fromCol, int toRow, int toCol, PieceColor color)
        {
            var movingPiece = Pieces[fromRow, fromCol];
            var targetPiece = Pieces[toRow, toCol];

            if (movingPiece == null) return false;

            int origRow = movingPiece.Row;
            int origCol = movingPiece.Column;

            Pieces[toRow, toCol] = movingPiece;
            Pieces[fromRow, fromCol] = null!;
            movingPiece.Row = toRow;
            movingPiece.Column = toCol;

            bool inCheckAfterMove = IsInCheck(color);

            movingPiece.Row = origRow;
            movingPiece.Column = origCol;
            Pieces[fromRow, fromCol] = movingPiece;
            Pieces[toRow, toCol] = targetPiece;

            return inCheckAfterMove;
        }

        public List<(int row, int col)> GetValidMovesForPiece(int row, int col)
        {
            var validMoves = new List<(int, int)>();
            var piece = Pieces[row, col];

            if (piece == null || piece.Color != CurrentTurn) 
            {
                return validMoves;
            }

            for (int targetRow = 0; targetRow < 8; targetRow++)
            {
                for (int targetCol = 0; targetCol < 8; targetCol++)
                {
                    if (piece.CanMove(targetRow, targetCol, Pieces))
                    {
                        if (piece is King && Math.Abs(targetCol - col) == 2 && IsInCheck(piece.Color))
                        {
                            continue; 
                        }

                        if (!WouldMoveResultInCheck(row, col, targetRow, targetCol, piece.Color))
                        {
                            validMoves.Add((targetRow, targetCol));
                        }
                    }
                }
            }

            return validMoves;
        }
    }
}