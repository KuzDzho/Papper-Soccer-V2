using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperSoccer.Native
{
    public struct PitchPoint : IEquatable<PitchPoint>
    {
        public int X;
        public int Y;
        public PitchPoint(int x, int y) { X = x; Y = y; }
        public bool Equals(PitchPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is PitchPoint other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Y;
        public static bool operator ==(PitchPoint a, PitchPoint b) => a.Equals(b);
        public static bool operator !=(PitchPoint a, PitchPoint b) => !a.Equals(b);
    }

    public struct PitchEdge
    {
        public PitchPoint A;
        public PitchPoint B;
        public int By;
        public PitchEdge(PitchPoint a, PitchPoint b, int by) { A = a; B = b; By = by; }
    }

    public enum PitchStatus { Playing, Goal, Finished }
    public enum PitchEventKind { Move, Pass, Goal, Finished }

    public struct PitchMoveEvent
    {
        public PitchEventKind Kind;
        public bool Bounce;
        public int Scorer;
        public int Winner;
        public PitchPoint From;
        public PitchPoint To;
    }

    /// <summary>Standalone rule engine ported from the web game's Match engine.</summary>
    public sealed class PaperSoccerEngine
    {
        public readonly int Cols;
        public readonly int Rows;
        public readonly int WinScore;
        public readonly List<PitchEdge> Edges = new List<PitchEdge>();
        private readonly HashSet<long> used = new HashSet<long>();
        private int[] degree;

        public PitchPoint Ball;
        public int Current;
        public int[] Scores = { 0, 0 };
        public PitchStatus Status = PitchStatus.Playing;
        public int Winner = -1;
        public int LastScorer = -1;
        public int TurnMoves;
        public bool BouncePending;
        public int MoveCount;

        public PaperSoccerEngine(int cols, int rows, int winScore)
        {
            Cols = Mathf.Max(5, cols | 1);
            Rows = Mathf.Max(7, rows | 1);
            WinScore = Mathf.Max(1, winScore);
            degree = new int[Cols * Rows];
            Ball = new PitchPoint((Cols - 1) / 2, (Rows - 1) / 2);
        }

        private PaperSoccerEngine(PaperSoccerEngine source)
        {
            Cols = source.Cols; Rows = source.Rows; WinScore = source.WinScore;
            Edges.AddRange(source.Edges);
            foreach (long e in source.used) used.Add(e);
            degree = (int[])source.degree.Clone();
            Ball = source.Ball; Current = source.Current;
            Scores = (int[])source.Scores.Clone(); Status = source.Status;
            Winner = source.Winner; LastScorer = source.LastScorer;
            TurnMoves = source.TurnMoves; BouncePending = source.BouncePending;
            MoveCount = source.MoveCount;
        }

        public PaperSoccerEngine Copy() => new PaperSoccerEngine(this);
        private int Index(PitchPoint p) => p.Y * Cols + p.X;
        private long EdgeKey(PitchPoint a, PitchPoint b)
        {
            long aa = Index(a), bb = Index(b);
            if (aa > bb) { long t = aa; aa = bb; bb = t; }
            return (aa << 32) | (bb & 0xffffffffL);
        }
        public int GoalHalf => Mathf.Max(1, Mathf.RoundToInt(((Cols - 1) / 2f) * 0.22f));
        public int GoalOwner(PitchPoint p)
        {
            int mid = (Cols - 1) / 2;
            if (Mathf.Abs(p.X - mid) > GoalHalf) return -1;
            if (p.Y == 0) return 0;
            if (p.Y == Rows - 1) return 1;
            return -1;
        }
        public bool IsInside(PitchPoint p) => p.X >= 0 && p.Y >= 0 && p.X < Cols && p.Y < Rows;
        public bool IsWall(PitchPoint p)
        {
            bool edge = p.X == 0 || p.Y == 0 || p.X == Cols - 1 || p.Y == Rows - 1;
            return edge && GoalOwner(p) == -1;
        }
        private bool WallTravel(PitchPoint a, PitchPoint b)
        {
            int lastX = Cols - 1, lastY = Rows - 1;
            if (a.X == b.X && (a.X == 0 || a.X == lastX) && a.Y != b.Y) return true;
            if (a.Y == b.Y && (a.Y == 0 || a.Y == lastY) && a.X != b.X) return true;
            return false;
        }
        private bool Adjacent(PitchPoint a, PitchPoint b)
        {
            int dx = Mathf.Abs(a.X - b.X), dy = Mathf.Abs(a.Y - b.Y);
            return dx <= 1 && dy <= 1 && dx + dy > 0;
        }

        public List<PitchPoint> LegalTargets() => LegalTargets(Ball, true);
        public List<PitchPoint> LegalTargets(PitchPoint from, bool obeyStatus = false)
        {
            var result = new List<PitchPoint>(8);
            if (obeyStatus && Status != PitchStatus.Playing) return result;
            if (GoalOwner(from) != -1) return result;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                var to = new PitchPoint(from.X + dx, from.Y + dy);
                if (!IsInside(to) || WallTravel(from, to) || used.Contains(EdgeKey(from, to))) continue;
                result.Add(to);
            }
            return result;
        }
        public bool CanEndTurn => Status == PitchStatus.Playing && !BouncePending && TurnMoves >= 1 && TurnMoves < 3;
        private bool BounceAt(PitchPoint p) => IsWall(p) || degree[Index(p)] >= 1;

        public PitchMoveEvent DoMove(PitchPoint to)
        {
            if (Status != PitchStatus.Playing) throw new InvalidOperationException("Матч уже завершен.");
            if (TurnMoves >= 3 && !BouncePending) throw new InvalidOperationException("За ход можно провести не более трех линий.");
            PitchPoint from = Ball;
            if (!Adjacent(from, to) || !IsInside(to) || WallTravel(from, to) || used.Contains(EdgeKey(from, to)))
                throw new InvalidOperationException("Этот ход недоступен.");

            int mover = Current;
            bool bounce = BounceAt(to);
            Edges.Add(new PitchEdge(from, to, mover));
            used.Add(EdgeKey(from, to));
            degree[Index(from)]++; degree[Index(to)]++;
            Ball = to; MoveCount++; TurnMoves++;

            int mouth = GoalOwner(to);
            if (mouth != -1)
            {
                Scores[mouth]++;
                LastScorer = mouth;
                if (Scores[mouth] >= WinScore)
                {
                    Status = PitchStatus.Finished; Winner = mouth;
                    return Event(PitchEventKind.Finished, false, mouth, mouth, from, to);
                }
                Status = PitchStatus.Goal;
                return Event(PitchEventKind.Goal, false, mouth, -1, from, to);
            }

            bool hasFollowUp = LegalTargets().Count > 0;
            BouncePending = bounce && hasFollowUp;
            if (!BouncePending && (!hasFollowUp || TurnMoves >= 3))
                return PassTurn(from, to, bounce);
            return Event(PitchEventKind.Move, bounce, -1, -1, from, to);
        }

        public PitchMoveEvent EndTurn()
        {
            if (!CanEndTurn) throw new InvalidOperationException("Сейчас нельзя завершить ход.");
            return PassTurn(Ball, Ball, false);
        }

        private PitchMoveEvent PassTurn(PitchPoint from, PitchPoint to, bool bounce)
        {
            Current = 1 - Current; TurnMoves = 0; BouncePending = false;
            if (LegalTargets().Count == 0)
            {
                int scorer = Current;
                Scores[scorer]++; LastScorer = scorer;
                if (Scores[scorer] >= WinScore)
                {
                    Status = PitchStatus.Finished; Winner = scorer;
                    return Event(PitchEventKind.Finished, bounce, scorer, scorer, from, to);
                }
                Status = PitchStatus.Goal;
                return Event(PitchEventKind.Goal, bounce, scorer, -1, from, to);
            }
            return Event(PitchEventKind.Pass, bounce, -1, -1, from, to);
        }

        private PitchMoveEvent Event(PitchEventKind kind, bool bounce, int scorer, int winner, PitchPoint from, PitchPoint to)
        {
            return new PitchMoveEvent { Kind = kind, Bounce = bounce, Scorer = scorer, Winner = winner, From = from, To = to };
        }

        public void ResetAfterGoal()
        {
            Edges.Clear(); used.Clear(); degree = new int[Cols * Rows];
            Ball = new PitchPoint((Cols - 1) / 2, (Rows - 1) / 2);
            Current = LastScorer == 0 ? 1 : 0;
            TurnMoves = 0; BouncePending = false; Status = PitchStatus.Playing;
        }

        public int FreedomAt(PitchPoint point) => LegalTargets(point).Count;

        public float EvaluateFor(int side)
        {
            if (Status == PitchStatus.Finished) return Winner == side ? 100000 : -100000;
            if (Status == PitchStatus.Goal) return LastScorer == side ? 15000 : -15000;
            int myDistance = DistanceToGoal(Ball, side);
            int opponentDistance = DistanceToGoal(Ball, 1 - side);
            int mobility = LegalTargets().Count;
            float score = (Scores[side] - Scores[1 - side]) * 600f + (opponentDistance - myDistance) * 3.3f;
            score += (Current == side ? mobility : -mobility) * 1.4f;
            if (BouncePending && Current == side) score += 3;
            return score;
        }

        private int DistanceToGoal(PitchPoint p, int side)
        {
            int targetY = side == 0 ? 0 : Rows - 1;
            int best = int.MaxValue;
            int mid = (Cols - 1) / 2;
            for (int x = mid - GoalHalf; x <= mid + GoalHalf; x++)
                best = Mathf.Min(best, Mathf.Abs(p.X - x) + Mathf.Abs(p.Y - targetY));
            return best;
        }

        public int DegreeAt(PitchPoint p) => degree[Index(p)];
    }
}
