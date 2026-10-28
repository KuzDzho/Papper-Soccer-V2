#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PaperSoccer.Native.Editor
{
    public static class PaperSoccerSmokeTests
    {
        [MenuItem("Paper Soccer/Run Rules Smoke Tests")]
        public static void Run()
        {
            try
            {
                var first = new PaperSoccerEngine(11, 15, 3);
                Check(first.LegalTargets().Count == 8, "The centre ball should have eight opening moves.");

                first.DoMove(new PitchPoint(6, 7));
                first.DoMove(new PitchPoint(6, 8));
                var third = first.DoMove(new PitchPoint(5, 8));
                Check(third.Kind == PitchEventKind.Pass, "The third ordinary line should pass the turn.");
                Check(first.Current == 1 && first.TurnMoves == 0, "The next player should receive a fresh turn after line three.");

                var wall = new PaperSoccerEngine(11, 15, 3);
                wall.DoMove(new PitchPoint(4, 7));
                wall.DoMove(new PitchPoint(3, 7));
                wall.DoMove(new PitchPoint(2, 7));
                wall.DoMove(new PitchPoint(1, 7));
                wall.DoMove(new PitchPoint(0, 7));
                Check(wall.BouncePending, "Landing on a side wall must require a bounce.");
                Check(!wall.LegalTargets().Contains(new PitchPoint(0, 6)), "Moving along the side boundary must be forbidden.");
                wall.DoMove(new PitchPoint(1, 6));
                Check(!wall.BouncePending, "A clean landing after a wall bounce must finish the forced continuation.");

                var goal = new PaperSoccerEngine(11, 15, 3);
                for (int y = 6; y >= 0; y--)
                {
                    var ev = goal.DoMove(new PitchPoint(5, y));
                    if (y == 0) Check(ev.Kind == PitchEventKind.Goal && goal.Scores[0] == 1, "The upper goal should award a point to the attacking side.");
                }
                goal.ResetAfterGoal();
                Check(goal.Edges.Count == 0 && goal.Ball == new PitchPoint(5, 7) && goal.Current == 1, "After a goal the field should clear and the conceding player should kick off.");

                Debug.Log("PAPER SOCCER SMOKE TESTS: PASS (opening moves, three-line turn, wall bounce, no boundary travel, goal and restart).");
                EditorUtility.DisplayDialog("Проверка правил пройдена", "Все встроенные проверки игрового движка пройдены.", "ОК");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Проверка правил не пройдена", ex.Message, "ОК");
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
