using System;
using System.Collections.Generic;
using UnityEngine;

namespace PaperSoccer.Native
{
    public sealed class PaperSoccerGame : MonoBehaviour
    {
        private enum ScreenMode { Menu, Match }
        private enum PlayMode { Bot, Local }

        private sealed class ArenaStyle
        {
            public string Id, Name;
            public Color Background, Pitch, Grid, Node, Accent;
            public ArenaStyle(string id, string name, string bg, string pitch, string grid, string node, string accent)
            {
                Id = id; Name = name; Background = Hex(bg); Pitch = Hex(pitch); Grid = Hex(grid); Node = Hex(node); Accent = Hex(accent);
            }
        }

        private static PaperSoccerGame instance;
        private readonly ArenaStyle[] arenas = {
            new ArenaStyle("stadium", "СТАДИОН", "#050805", "#0B1A10", "#22402C", "#789B76", "#9FE870"),
            new ArenaStyle("night", "НОЧЬ", "#050711", "#0B1022", "#1D3157", "#7586C9", "#64DDFB"),
            new ArenaStyle("winter", "ЗИМА", "#061018", "#102432", "#2B4A5B", "#B5D5E2", "#B8EEFF"),
            new ArenaStyle("beach", "ПЛЯЖ", "#160F09", "#302316", "#604A2B", "#D5B77A", "#FFD27A"),
            new ArenaStyle("paper", "ТЕТРАДЬ", "#111014", "#E7E0CE", "#C8C1B2", "#6C6A64", "#4C78C8")
        };
        private readonly string[] swatches = { "#00D4FF", "#FF3366", "#9FE870", "#FFB020", "#FF6A2A", "#2FD6B0", "#FF7AD9", "#F0EAD6" };

        private ScreenMode screen = ScreenMode.Menu;
        private PlayMode playMode = PlayMode.Bot;
        private int difficulty = 1;
        private int pitchPreset = 1;
        private int targetScore = 3;
        private int arenaIndex;
        private int[] colorIndices = { 0, 1 };
        private string[] teamNames = { "Ты", "ИИ" };
        private PaperSoccerEngine game;
        private PaperSoccerEngine turnSnapshot;
        private bool rulesOpen;
        private bool matchOver;
        private bool resultSaved;
        private bool soundOn = true;
        private float coinTossUntil;
        private float goalUntil;
        private float nextAiMoveAt;
        private string banner = "Выбери режим и начинай матч";
        private string lastGoalText = "";
        private Vector2 pitchOrigin;
        private float pitchCell;
        private float virtualWidth;
        private float virtualHeight;
        private GUIStyle invisibleButton;
        private readonly Dictionary<string, GUIStyle> textStyles = new Dictionary<string, GUIStyle>();
        private Texture2D pixel;
        private Texture2D transparent;
        private Texture2D inputTexture;
        private Texture2D circle;
        private Texture2D softCircle;
        private AudioSource audioSource;
        private AudioClip clickClip;
        private AudioClip goalClip;

        private static readonly Color Ink = Hex("#F0EAD6");
        private static readonly Color Muted = Hex("#8A9388");
        private static readonly Color Panel = Hex("#111713");
        private static readonly Color PanelAlt = Hex("#19221B");
        private static readonly Color AccentGreen = Hex("#9FE870");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Launch()
        {
            if (instance != null) return;
            var root = new GameObject("Paper Soccer - Game");
            root.AddComponent<PaperSoccerGame>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Application.targetFrameRate = 60;
            LoadPreferences();
            BuildTextures();
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            clickClip = CreateTone("UI click", 740f, 0.045f, 0.13f);
            goalClip = CreateTone("Goal", 520f, 0.23f, 0.22f);
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
            if (transparent != null) Destroy(transparent);
            if (inputTexture != null) Destroy(inputTexture);
            if (circle != null) Destroy(circle);
            if (softCircle != null) Destroy(softCircle);
            if (clickClip != null) Destroy(clickClip);
            if (goalClip != null) Destroy(goalClip);
        }

        private void LoadPreferences()
        {
            difficulty = Mathf.Clamp(PlayerPrefs.GetInt("psn.difficulty", 1), 0, 2);
            pitchPreset = Mathf.Clamp(PlayerPrefs.GetInt("psn.pitch", 1), 0, 2);
            targetScore = PlayerPrefs.GetInt("psn.score", 3);
            if (targetScore != 1 && targetScore != 3 && targetScore != 5) targetScore = 3;
            arenaIndex = Mathf.Clamp(PlayerPrefs.GetInt("psn.arena", 0), 0, arenas.Length - 1);
            colorIndices[0] = Mathf.Clamp(PlayerPrefs.GetInt("psn.color0", 0), 0, swatches.Length - 1);
            colorIndices[1] = Mathf.Clamp(PlayerPrefs.GetInt("psn.color1", 1), 0, swatches.Length - 1);
            if (colorIndices[0] == colorIndices[1]) colorIndices[1] = (colorIndices[0] + 1) % swatches.Length;
            teamNames[0] = PlayerPrefs.GetString("psn.name0", "Ты");
            teamNames[1] = PlayerPrefs.GetString("psn.name1", "ИИ");
            soundOn = PlayerPrefs.GetInt("psn.sound", 1) == 1;
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetInt("psn.difficulty", difficulty);
            PlayerPrefs.SetInt("psn.pitch", pitchPreset);
            PlayerPrefs.SetInt("psn.score", targetScore);
            PlayerPrefs.SetInt("psn.arena", arenaIndex);
            PlayerPrefs.SetInt("psn.color0", colorIndices[0]);
            PlayerPrefs.SetInt("psn.color1", colorIndices[1]);
            PlayerPrefs.SetString("psn.name0", teamNames[0] ?? "Игрок 1");
            PlayerPrefs.SetString("psn.name1", teamNames[1] ?? "Игрок 2");
            PlayerPrefs.SetInt("psn.sound", soundOn ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void BuildTextures()
        {
            pixel = MakeSolidTexture(Color.white);
            transparent = MakeSolidTexture(new Color(1, 1, 1, 0));
            inputTexture = MakeSolidTexture(Panel);
            circle = MakeCircle(64, false);
            softCircle = MakeCircle(96, true);
            invisibleButton = new GUIStyle()
            {
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0),
                border = new RectOffset(0, 0, 0, 0), fontSize = 1
            };
            invisibleButton.normal.background = transparent;
            invisibleButton.hover.background = transparent;
            invisibleButton.active.background = transparent;
            invisibleButton.focused.background = transparent;
            invisibleButton.normal.textColor = new Color(1, 1, 1, 0);
            invisibleButton.hover.textColor = new Color(1, 1, 1, 0);
            invisibleButton.active.textColor = new Color(1, 1, 1, 0);
        }

        private Texture2D MakeSolidTexture(Color color)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, color); tex.Apply();
            return tex;
        }

        private Texture2D MakeCircle(int size, bool soft)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - size / 2f) / (size / 2f);
                float dy = (y + 0.5f - size / 2f) / (size / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = soft ? Mathf.Clamp01((1f - d) * 1.8f) * Mathf.Clamp01(1f - d * 0.72f) : Mathf.Clamp01((1f - d) * size * 0.25f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            return tex;
        }

        private AudioClip CreateTone(string clipName, float frequency, float seconds, float volume)
        {
            const int rate = 22050;
            int samples = Mathf.Max(256, Mathf.RoundToInt(rate * seconds));
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Pow(1f - i / (float)samples, 2f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * volume;
            }
            var clip = AudioClip.Create(clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void PlayUiSound(bool goal = false)
        {
            if (soundOn && audioSource != null) audioSource.PlayOneShot(goal ? goalClip : clickClip);
        }

        private void Update()
        {
            if (screen != ScreenMode.Match || game == null) return;
            if (game.Status == PitchStatus.Goal && goalUntil > 0 && Time.realtimeSinceStartup >= goalUntil && !matchOver)
            {
                game.ResetAfterGoal();
                goalUntil = 0;
                turnSnapshot = null;
                banner = $"Розыгрыш начинает {TeamName(game.Current)}";
                nextAiMoveAt = Time.realtimeSinceStartup + 0.65f;
            }
            if (game.Status == PitchStatus.Finished && !matchOver)
            {
                matchOver = true;
                goalUntil = 0;
                SaveMatchResult();
            }
            if (!matchOver && Time.realtimeSinceStartup >= coinTossUntil && game.Status == PitchStatus.Playing && playMode == PlayMode.Bot && game.Current == 1 && Time.realtimeSinceStartup >= nextAiMoveAt)
                RunAiTurnStep();
        }

        private void SaveMatchResult()
        {
            if (resultSaved) return;
            resultSaved = true;
            if (playMode == PlayMode.Bot)
            {
                string key = "psn." + difficulty.ToString() + ".";
                string result = game.Winner == 0 ? "wins" : "losses";
                PlayerPrefs.SetInt(key + result, PlayerPrefs.GetInt(key + result, 0) + 1);
                PlayerPrefs.Save();
            }
            PlayUiSound(true);
            banner = game.Winner >= 0 ? $"Побеждает {TeamName(game.Winner)}" : "Матч завершен";
        }

        private void RunAiTurnStep()
        {
            if (game == null || game.Status != PitchStatus.Playing || game.Current != 1) return;
            List<PitchPoint> legal = game.LegalTargets();
            if (legal.Count == 0)
            {
                if (game.CanEndTurn) ApplyMoveEvent(game.EndTurn());
                else banner = "Нет доступных ходов";
                nextAiMoveAt = Time.realtimeSinceStartup + 0.55f;
                return;
            }
            PitchPoint chosen = ChooseAiMove();
            if (game.TurnMoves == 0 && !game.BouncePending) turnSnapshot = game.Copy();
            try { ApplyMoveEvent(game.DoMove(chosen)); }
            catch (Exception e) { Debug.LogWarning("Paper Soccer AI skipped a move: " + e.Message); }
            nextAiMoveAt = Time.realtimeSinceStartup + (game.BouncePending ? 0.34f : 0.48f);
        }

        private PitchPoint ChooseAiMove()
        {
            List<PitchPoint> moves = game.LegalTargets();
            int side = game.Current;
            if (difficulty == 0)
            {
                var safe = new List<PitchPoint>();
                foreach (var p in moves) if (game.GoalOwner(p) != 1 - side) safe.Add(p);
                if (safe.Count == 0) safe = moves;
                var goals = new List<PitchPoint>();
                foreach (var p in safe) if (game.GoalOwner(p) == side) goals.Add(p);
                if (goals.Count > 0) return goals[UnityEngine.Random.Range(0, goals.Count)];
                return safe[UnityEngine.Random.Range(0, safe.Count)];
            }

            float best = float.NegativeInfinity;
            var bestMoves = new List<PitchPoint>();
            foreach (var p in moves)
            {
                PaperSoccerEngine candidate = game.Copy();
                PitchMoveEvent ev = candidate.DoMove(p);
                float score = candidate.EvaluateFor(side);
                if (ev.Bounce) score += 2.5f;
                if (candidate.Status == PitchStatus.Playing)
                {
                    int direction = side == 0 ? -1 : 1;
                    score += (p.Y - game.Ball.Y) * direction * 0.7f;
                    if (difficulty >= 2 && candidate.Current != side && candidate.Status == PitchStatus.Playing)
                    {
                        List<PitchPoint> replies = candidate.LegalTargets();
                        float worstReply = float.PositiveInfinity;
                        foreach (var reply in replies)
                        {
                            var response = candidate.Copy();
                            response.DoMove(reply);
                            float responseScore = response.EvaluateFor(side);
                            if (responseScore < worstReply) worstReply = responseScore;
                            if (worstReply < -13000) break;
                        }
                        if (!float.IsPositiveInfinity(worstReply)) score = worstReply + 0.25f * score;
                    }
                }
                score += UnityEngine.Random.Range(-0.35f, 0.35f);
                if (score > best + 0.01f) { best = score; bestMoves.Clear(); bestMoves.Add(p); }
                else if (Mathf.Abs(score - best) <= 0.01f) bestMoves.Add(p);
            }
            return bestMoves[UnityEngine.Random.Range(0, bestMoves.Count)];
        }

        private void StartMatch()
        {
            SavePreferences();
            int[] rowOptions = { 15, 19, 25 };
            game = new PaperSoccerEngine(11, rowOptions[pitchPreset], targetScore);
            game.Current = UnityEngine.Random.Range(0, 2);
            screen = ScreenMode.Match;
            rulesOpen = false;
            matchOver = false;
            resultSaved = false;
            turnSnapshot = null;
            coinTossUntil = Time.realtimeSinceStartup + 1.45f;
            goalUntil = 0;
            nextAiMoveAt = coinTossUntil + 0.35f;
            banner = $"Монетка: первым ходит {TeamName(game.Current)}";
            lastGoalText = "";
            PlayUiSound();
        }

        private void ReturnToMenu()
        {
            SavePreferences();
            screen = ScreenMode.Menu;
            rulesOpen = false;
            game = null;
            turnSnapshot = null;
            matchOver = false;
            PlayUiSound();
        }

        private void RestartMatch()
        {
            StartMatch();
            PlayUiSound();
        }

        private void DoPlayerMove(PitchPoint target)
        {
            if (!CanHumanAct() || game == null) return;
            if (game.TurnMoves == 0 && !game.BouncePending) turnSnapshot = game.Copy();
            try { ApplyMoveEvent(game.DoMove(target)); }
            catch (Exception e) { banner = e.Message; PlayUiSound(); }
        }

        private void ApplyMoveEvent(PitchMoveEvent ev)
        {
            if (ev.Kind == PitchEventKind.Goal || ev.Kind == PitchEventKind.Finished)
            {
                lastGoalText = ev.Scorer >= 0 ? $"ОЧКО: {TeamName(ev.Scorer)}" : "ГОЛ!";
                banner = ev.Kind == PitchEventKind.Finished ? $"Побеждает {TeamName(ev.Winner)}" : $"Очко получает {TeamName(ev.Scorer)}";
                PlayUiSound(true);
                if (ev.Kind == PitchEventKind.Goal) goalUntil = Time.realtimeSinceStartup + 1.75f;
                else { matchOver = true; SaveMatchResult(); }
                turnSnapshot = null;
            }
            else if (ev.Kind == PitchEventKind.Pass)
            {
                banner = $"Ход: {TeamName(game.Current)}";
                turnSnapshot = null;
                PlayUiSound();
            }
            else
            {
                banner = ev.Bounce ? "ОТСКОК — продолжай ход" : $"Линия {game.TurnMoves} из 3";
                PlayUiSound();
            }
            if (game.Status == PitchStatus.Playing && playMode == PlayMode.Bot && game.Current == 1)
                nextAiMoveAt = Time.realtimeSinceStartup + (ev.Bounce ? 0.34f : 0.5f);
        }

        private bool CanHumanAct()
        {
            if (screen != ScreenMode.Match || game == null || game.Status != PitchStatus.Playing || matchOver) return false;
            if (Time.realtimeSinceStartup < coinTossUntil) return false;
            return playMode == PlayMode.Local || game.Current == 0;
        }

        private void UndoTurn()
        {
            if (!CanHumanAct() || turnSnapshot == null || game.TurnMoves == 0) return;
            game = turnSnapshot.Copy();
            turnSnapshot = null;
            banner = "Ход отменен";
            PlayUiSound();
        }

        private string TeamName(int side)
        {
            string name = side >= 0 && side < teamNames.Length ? teamNames[side] : "Игрок";
            return string.IsNullOrWhiteSpace(name) ? (side == 0 ? "Игрок 1" : "Игрок 2") : name.Trim();
        }

        private Color TeamColor(int side) => Hex(swatches[colorIndices[Mathf.Clamp(side, 0, 1)]]);
        private ArenaStyle CurrentArena => arenas[Mathf.Clamp(arenaIndex, 0, arenas.Length - 1)];

        private void OnGUI()
        {
            if (pixel == null) BuildTextures();
            float scale = Mathf.Min(Screen.width / 390f, Screen.height / 760f);
            scale = Mathf.Max(0.45f, scale);
            virtualWidth = Screen.width / scale;
            virtualHeight = Screen.height / scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUI.depth = 0;
            DrawRect(new Rect(0, 0, virtualWidth, virtualHeight), CurrentArena.Background);
            if (screen == ScreenMode.Menu) DrawMenu();
            else DrawMatch();
            if (rulesOpen) DrawRulesOverlay();
            GUI.matrix = Matrix4x4.identity;
            GUI.color = Color.white;
        }

        private void DrawMenu()
        {
            float w = virtualWidth, h = virtualHeight;
            DrawRect(new Rect(20, h * 0.048f, 4, 58), CurrentArena.Accent);
            DrawText(new Rect(34, h * 0.042f, w - 58, 43), "БУМАЖНЫЙ ФУТБОЛ", 28, Ink, TextAnchor.MiddleLeft, true);
            DrawText(new Rect(35, h * 0.102f, w - 60, 22), "ТАКТИКА  /  ОТСКОК  /  ГОЛ", 10, Muted, TextAnchor.MiddleLeft, true);

            float left = 20, gap = 9, contentW = w - left * 2;
            float modeY = h * 0.158f, modeH = 54, modeW = (contentW - gap) / 2;
            DrawOption(new Rect(left, modeY, modeW, modeH), "ПРОТИВ ИИ", "Уровни сложности", playMode == PlayMode.Bot, () => { playMode = PlayMode.Bot; teamNames[1] = "ИИ"; PlayUiSound(); });
            DrawOption(new Rect(left + modeW + gap, modeY, modeW, modeH), "ВДВОЕМ", "Один телефон", playMode == PlayMode.Local, () => { playMode = PlayMode.Local; if (teamNames[1] == "ИИ") teamNames[1] = "Игрок 2"; PlayUiSound(); });

            if (playMode == PlayMode.Bot)
            {
                DrawText(new Rect(left, h * 0.235f, 150, 18), "СЛОЖНОСТЬ", 10, Muted, TextAnchor.MiddleLeft, true);
                string[] levels = { "НОВИЧОК", "ЛЮБИТЕЛЬ", "ПРОФИ" };
                for (int i = 0; i < 3; i++)
                {
                    int captured = i;
                    DrawSmallOption(new Rect(left + i * (contentW / 3f), h * 0.258f, contentW / 3f - 6, 38), levels[i], difficulty == i, () => { difficulty = captured; SavePreferences(); PlayUiSound(); });
                }
            }
            else
            {
                DrawText(new Rect(left, h * 0.244f, contentW, 36), "Играйте по очереди на одном экране. После каждого хода передайте телефон сопернику.", 12, Muted, TextAnchor.MiddleLeft, false);
            }

            DrawText(new Rect(left, h * 0.317f, 120, 17), "РАЗМЕР ПОЛЯ", 10, Muted, TextAnchor.MiddleLeft, true);
            string[] sizes = { "СПРИНТ · 15", "КЛАССИКА · 19", "МАРАФОН · 25" };
            for (int i = 0; i < 3; i++)
            {
                int captured = i;
                DrawSmallOption(new Rect(left + i * (contentW / 3f), h * 0.34f, contentW / 3f - 6, 38), sizes[i], pitchPreset == i, () => { pitchPreset = captured; SavePreferences(); PlayUiSound(); });
            }

            DrawText(new Rect(left, h * 0.397f, 170, 17), "ПОБЕДА ДО", 10, Muted, TextAnchor.MiddleLeft, true);
            int[] scores = { 1, 3, 5 };
            for (int i = 0; i < scores.Length; i++)
            {
                int captured = scores[i];
                DrawSmallOption(new Rect(left + i * 61, h * 0.419f, 54, 36), captured.ToString(), targetScore == captured, () => { targetScore = captured; SavePreferences(); PlayUiSound(); });
            }
            DrawText(new Rect(left + 202, h * 0.398f, contentW - 202, 18), "АРЕНА", 10, Muted, TextAnchor.MiddleLeft, true);
            float arenaGap = 4, arenaW = (contentW - 202 - arenaGap * 4) / 5f;
            for (int i = 0; i < arenas.Length; i++)
            {
                int captured = i;
                DrawSmallOption(new Rect(left + 202 + i * (arenaW + arenaGap), h * 0.419f, arenaW, 36), arenas[i].Name, arenaIndex == i, () => { arenaIndex = captured; SavePreferences(); PlayUiSound(); }, 9);
            }

            DrawText(new Rect(left, h * 0.477f, contentW, 18), "ЦВЕТА КОМАНД", 10, Muted, TextAnchor.MiddleLeft, true);
            for (int side = 0; side < 2; side++)
            {
                DrawText(new Rect(left, h * (side == 0 ? 0.501f : 0.544f), 20, 26), side == 0 ? "1" : "2", 10, side == 0 ? TeamColor(0) : TeamColor(1), TextAnchor.MiddleCenter, true);
                for (int i = 0; i < swatches.Length; i++)
                {
                    int capturedSide = side, capturedColor = i;
                    Rect r = new Rect(left + 34 + i * ((contentW - 34) / 8f), h * (side == 0 ? 0.501f : 0.544f), (contentW - 34) / 8f - 3, 26);
                    Color c = Hex(swatches[i]);
                    DrawRect(r, c);
                    if (colorIndices[side] == i) DrawBorder(r, Color.white, 2);
                    if (GUI.Button(r, GUIContent.none, invisibleButton))
                    {
                        if (colorIndices[1 - capturedSide] == capturedColor) colorIndices[1 - capturedSide] = colorIndices[capturedSide];
                        colorIndices[capturedSide] = capturedColor;
                        SavePreferences(); PlayUiSound();
                    }
                }
            }
            DrawText(new Rect(left, h * 0.594f, contentW, 17), "НАЗВАНИЯ КОМАНД", 10, Muted, TextAnchor.MiddleLeft, true);
            float nameGap = 8, nameW = (contentW - nameGap) / 2;
            DrawTextField(new Rect(left, h * 0.617f, nameW, 32), ref teamNames[0]);
            DrawTextField(new Rect(left + nameW + nameGap, h * 0.617f, nameW, 32), ref teamNames[1]);

            DrawButton(new Rect(left, h * 0.686f, contentW, 56), "НАЧАТЬ МАТЧ", AccentGreen, Hex("#10180F"), true, StartMatch);
            DrawButton(new Rect(left, h * 0.762f, (contentW - gap) / 2, 43), "ПРАВИЛА", PanelAlt, Ink, true, () => { rulesOpen = true; PlayUiSound(); });
            DrawButton(new Rect(left + (contentW + gap) / 2, h * 0.762f, (contentW - gap) / 2, 43), soundOn ? "ЗВУК: ВКЛ" : "ЗВУК: ВЫКЛ", PanelAlt, Ink, true, () => { soundOn = !soundOn; SavePreferences(); PlayUiSound(); });

            int wins = PlayerPrefs.GetInt("psn." + difficulty + ".wins", 0);
            int losses = PlayerPrefs.GetInt("psn." + difficulty + ".losses", 0);
            DrawText(new Rect(left, h * 0.835f, contentW, 24), playMode == PlayMode.Bot ? $"СТАТИСТИКА ПРОТИВ ИИ     {wins} ПОБЕД  /  {losses} ПОРАЖЕНИЙ" : "ЛОКАЛЬНАЯ ИГРА  ·  БЕЗ ИНТЕРНЕТА", 9, Muted, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(left, h * 0.894f, contentW, 20), "PAPER SOCCER  /  NATIVE EDITION", 8, new Color(Muted.r, Muted.g, Muted.b, 0.65f), TextAnchor.MiddleCenter, true);
        }

        private void DrawMatch()
        {
            float w = virtualWidth, h = virtualHeight;
            ArenaStyle style = CurrentArena;
            DrawButton(new Rect(16, 15, 76, 34), "‹ МЕНЮ", PanelAlt, Ink, true, ReturnToMenu, 10);
            DrawText(new Rect(94, 15, w - 188, 34), "БУМАЖНЫЙ ФУТБОЛ", 14, Ink, TextAnchor.MiddleCenter, true);
            DrawButton(new Rect(w - 94, 15, 78, 34), soundOn ? "ЗВУК ●" : "ЗВУК ○", PanelAlt, Ink, true, () => { soundOn = !soundOn; SavePreferences(); PlayUiSound(); }, 9);

            float cardY = 58, cardH = 54, cardGap = 8, cardW = (w - 32 - cardGap) / 2;
            DrawRect(new Rect(16, cardY, cardW, cardH), Panel);
            DrawRect(new Rect(16, cardY, 3, cardH), TeamColor(0));
            DrawText(new Rect(28, cardY + 5, cardW - 40, 17), TeamName(0).ToUpperInvariant(), 10, TeamColor(0), TextAnchor.MiddleLeft, true);
            DrawText(new Rect(28, cardY + 20, cardW - 40, 29), game.Scores[0].ToString(), 26, Ink, TextAnchor.MiddleLeft, true);
            float rightX = 16 + cardW + cardGap;
            DrawRect(new Rect(rightX, cardY, cardW, cardH), Panel);
            DrawRect(new Rect(rightX + cardW - 3, cardY, 3, cardH), TeamColor(1));
            DrawText(new Rect(rightX + 12, cardY + 5, cardW - 40, 17), TeamName(1).ToUpperInvariant(), 10, TeamColor(1), TextAnchor.MiddleRight, true);
            DrawText(new Rect(rightX + 12, cardY + 20, cardW - 40, 29), game.Scores[1].ToString(), 26, Ink, TextAnchor.MiddleRight, true);

            float boardTop = 125, boardBottom = h - 151;
            float maxW = w - 40, maxH = boardBottom - boardTop;
            pitchCell = Mathf.Min(maxW / (game.Cols - 1), maxH / (game.Rows - 1));
            pitchCell = Mathf.Max(10f, pitchCell);
            float actualW = (game.Cols - 1) * pitchCell, actualH = (game.Rows - 1) * pitchCell;
            pitchOrigin = new Vector2((w - actualW) / 2f, boardTop + (maxH - actualH) / 2f);
            Rect outer = new Rect(pitchOrigin.x - pitchCell * 0.58f, pitchOrigin.y - pitchCell * 0.58f, actualW + pitchCell * 1.16f, actualH + pitchCell * 1.16f);
            DrawRect(outer, style.Pitch);
            DrawBorder(outer, new Color(style.Grid.r, style.Grid.g, style.Grid.b, 0.72f), 1.2f);

            int gh = game.GoalHalf, mid = (game.Cols - 1) / 2;
            float goalX = pitchOrigin.x + (mid - gh - 0.48f) * pitchCell;
            float goalW = (gh * 2 + 1) * pitchCell + 0.96f * pitchCell;
            DrawRect(new Rect(goalX, pitchOrigin.y - pitchCell * 0.4f, goalW, pitchCell * 0.8f), new Color(TeamColor(0).r, TeamColor(0).g, TeamColor(0).b, 0.14f));
            float bottomY = pitchOrigin.y + (game.Rows - 1) * pitchCell;
            DrawRect(new Rect(goalX, bottomY - pitchCell * 0.4f, goalW, pitchCell * 0.8f), new Color(TeamColor(1).r, TeamColor(1).g, TeamColor(1).b, 0.14f));
            DrawBorder(new Rect(goalX, pitchOrigin.y - pitchCell * 0.4f, goalW, pitchCell * 0.8f), new Color(TeamColor(0).r, TeamColor(0).g, TeamColor(0).b, 0.8f), 1.3f);
            DrawBorder(new Rect(goalX, bottomY - pitchCell * 0.4f, goalW, pitchCell * 0.8f), new Color(TeamColor(1).r, TeamColor(1).g, TeamColor(1).b, 0.8f), 1.3f);

            for (int x = 0; x < game.Cols; x++)
                DrawLine(NodeScreen(new PitchPoint(x, 0)), NodeScreen(new PitchPoint(x, game.Rows - 1)), new Color(style.Grid.r, style.Grid.g, style.Grid.b, 0.22f), 0.55f);
            for (int y = 0; y < game.Rows; y++)
                DrawLine(NodeScreen(new PitchPoint(0, y)), NodeScreen(new PitchPoint(game.Cols - 1, y)), new Color(style.Grid.r, style.Grid.g, style.Grid.b, 0.22f), 0.55f);

            foreach (PitchEdge edge in game.Edges)
            {
                Vector2 a = NodeScreen(edge.A), b = NodeScreen(edge.B);
                Color c = TeamColor(edge.By);
                DrawLine(a, b, new Color(c.r, c.g, c.b, 0.17f), Mathf.Max(5.2f, pitchCell * 0.22f));
                DrawLine(a, b, c, Mathf.Max(2.1f, pitchCell * 0.095f));
                DrawLine(a, b, Color.Lerp(c, Color.white, 0.36f), Mathf.Max(0.8f, pitchCell * 0.035f));
            }

            float nodeSize = Mathf.Clamp(pitchCell * 0.13f, 2.0f, 4.4f);
            for (int y = 0; y < game.Rows; y++)
            for (int x = 0; x < game.Cols; x++)
            {
                var p = new PitchPoint(x, y);
                Vector2 s = NodeScreen(p);
                GUI.color = new Color(style.Node.r, style.Node.g, style.Node.b, 0.78f);
                GUI.DrawTexture(new Rect(s.x - nodeSize / 2, s.y - nodeSize / 2, nodeSize, nodeSize), circle);
                GUI.color = Color.white;
            }

            bool humanCanMove = CanHumanAct();
            List<PitchPoint> legal = humanCanMove ? game.LegalTargets() : new List<PitchPoint>();
            foreach (PitchPoint p in legal)
            {
                Vector2 s = NodeScreen(p);
                float size = Mathf.Clamp(pitchCell * 0.6f, 12f, 27f);
                Color c = TeamColor(game.Current);
                GUI.color = new Color(c.r, c.g, c.b, 0.22f);
                GUI.DrawTexture(new Rect(s.x - size * 0.65f, s.y - size * 0.65f, size * 1.3f, size * 1.3f), softCircle);
                GUI.color = new Color(c.r, c.g, c.b, 0.85f);
                GUI.DrawTexture(new Rect(s.x - size * 0.32f, s.y - size * 0.32f, size * 0.64f, size * 0.64f), circle);
                GUI.color = Color.white;
                float hit = Mathf.Clamp(pitchCell * 0.88f, 12f, 34f);
                if (GUI.Button(new Rect(s.x - hit / 2f, s.y - hit / 2f, hit, hit), GUIContent.none, invisibleButton))
                {
                    DoPlayerMove(p);
                    return;
                }
            }

            Vector2 ball = NodeScreen(game.Ball);
            float ballSize = Mathf.Clamp(pitchCell * 0.58f, 10f, 23f);
            Color ballTeam = TeamColor(game.Current);
            GUI.color = new Color(ballTeam.r, ballTeam.g, ballTeam.b, 0.25f);
            GUI.DrawTexture(new Rect(ball.x - ballSize * 1.15f, ball.y - ballSize * 1.15f, ballSize * 2.3f, ballSize * 2.3f), softCircle);
            GUI.color = Hex("#F7F3E8");
            GUI.DrawTexture(new Rect(ball.x - ballSize / 2, ball.y - ballSize / 2, ballSize, ballSize), circle);
            GUI.color = ballTeam;
            GUI.DrawTexture(new Rect(ball.x - ballSize * 0.19f, ball.y - ballSize * 0.19f, ballSize * 0.38f, ballSize * 0.38f), circle);
            GUI.color = Color.white;

            DrawRect(new Rect(16, h - 136, w - 32, 28), new Color(Panel.r, Panel.g, Panel.b, 0.94f));
            string hint = Time.realtimeSinceStartup < coinTossUntil ? "ПОДБРАСЫВАЕМ МОНЕТКУ…" : matchOver ? "МАТЧ ЗАВЕРШЕН" : game.Status == PitchStatus.Goal ? lastGoalText : game.BouncePending ? "ОТСКОК: ОБЯЗАТЕЛЬНО ПРОДОЛЖИ ХОД" : CanHumanAct() ? "НАЖМИ НА СОСЕДНЮЮ ТОЧКУ, ЧТОБЫ ПРОВЕСТИ ЛИНИЮ" : $"ХОДИТ {TeamName(game.Current).ToUpperInvariant()}";
            DrawText(new Rect(22, h - 135, w - 44, 26), hint, 9, game.BouncePending ? AccentGreen : Muted, TextAnchor.MiddleCenter, true);
            float buttonY = h - 96, buttonH = 48;
            float buttonGap = 8, halfW = (w - 32 - buttonGap) / 2f;
            bool canEnd = CanHumanAct() && game.CanEndTurn;
            DrawButton(new Rect(16, buttonY, halfW, buttonH), "КОНЕЦ ХОДА", canEnd ? AccentGreen : PanelAlt, canEnd ? Hex("#10180F") : Muted, canEnd, () => { if (CanHumanAct()) { ApplyMoveEvent(game.EndTurn()); } }, 11);
            bool canUndo = CanHumanAct() && turnSnapshot != null && game.TurnMoves > 0 && !game.BouncePending;
            DrawButton(new Rect(16 + halfW + buttonGap, buttonY, halfW, buttonH), "ОТМЕНИТЬ ХОД", PanelAlt, canUndo ? Ink : Muted, canUndo, UndoTurn, 11);

            if (!matchOver && game.Status == PitchStatus.Playing && Time.realtimeSinceStartup < coinTossUntil)
            {
                DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.48f));
                DrawRect(new Rect(28, h * 0.41f, w - 56, 110), Panel);
                DrawBorder(new Rect(28, h * 0.41f, w - 56, 110), new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.55f), 1.2f);
                DrawText(new Rect(40, h * 0.425f, w - 80, 35), "ПЕРЕД МАТЧЕМ", 11, Muted, TextAnchor.MiddleCenter, true);
                DrawText(new Rect(40, h * 0.46f, w - 80, 40), "ПЕРВЫМ ХОДИТ", 21, Ink, TextAnchor.MiddleCenter, true);
                DrawText(new Rect(40, h * 0.505f, w - 80, 30), TeamName(game.Current).ToUpperInvariant(), 16, TeamColor(game.Current), TextAnchor.MiddleCenter, true);
            }

            if (game.Status == PitchStatus.Goal && !matchOver)
            {
                DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.55f));
                Rect panel = new Rect(28, h * 0.39f, w - 56, 142);
                DrawRect(panel, Panel);
                DrawBorder(panel, TeamColor(game.LastScorer), 2f);
                DrawText(new Rect(panel.x + 10, panel.y + 15, panel.width - 20, 42), "ОЧКО!", 34, TeamColor(game.LastScorer), TextAnchor.MiddleCenter, true);
                DrawText(new Rect(panel.x + 10, panel.y + 60, panel.width - 20, 28), TeamName(game.LastScorer) + " забирает розыгрыш", 13, Ink, TextAnchor.MiddleCenter, true);
                DrawText(new Rect(panel.x + 10, panel.y + 98, panel.width - 20, 24), $"{game.Scores[0]}  :  {game.Scores[1]}", 17, Muted, TextAnchor.MiddleCenter, true);
                if (GUI.Button(new Rect(panel.x + panel.width - 105, panel.y + panel.height - 31, 92, 24), "ДАЛЬШЕ", invisibleButton)) { game.ResetAfterGoal(); goalUntil = 0; turnSnapshot = null; PlayUiSound(); }
            }

            if (matchOver) DrawMatchOver();
        }

        private Vector2 NodeScreen(PitchPoint p) => new Vector2(pitchOrigin.x + p.X * pitchCell, pitchOrigin.y + p.Y * pitchCell);

        private void DrawMatchOver()
        {
            float w = virtualWidth, h = virtualHeight;
            DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.72f));
            Rect panel = new Rect(24, h * 0.34f, w - 48, 250);
            DrawRect(panel, Panel);
            DrawBorder(panel, game.Winner >= 0 ? TeamColor(game.Winner) : AccentGreen, 2f);
            DrawText(new Rect(panel.x + 12, panel.y + 20, panel.width - 24, 25), "ФИНАЛЬНЫЙ СВИСТОК", 11, Muted, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(panel.x + 12, panel.y + 54, panel.width - 24, 53), "ПОБЕДА", 38, game.Winner >= 0 ? TeamColor(game.Winner) : Ink, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(panel.x + 12, panel.y + 107, panel.width - 24, 31), game.Winner >= 0 ? TeamName(game.Winner) : "Ничья", 16, Ink, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(panel.x + 12, panel.y + 143, panel.width - 24, 28), $"{game.Scores[0]}  :  {game.Scores[1]}", 22, Muted, TextAnchor.MiddleCenter, true);
            DrawButton(new Rect(panel.x + 18, panel.y + 184, panel.width - 36, 42), "РЕВАНШ", AccentGreen, Hex("#10180F"), true, RestartMatch, 12);
            DrawText(new Rect(panel.x + 10, panel.y + panel.height - 25, panel.width - 20, 20), "ИЛИ НАЖМИ «МЕНЮ» ВВЕРХУ", 8, Muted, TextAnchor.MiddleCenter, true);
        }

        private void DrawRulesOverlay()
        {
            float w = virtualWidth, h = virtualHeight;
            DrawRect(new Rect(0, 0, w, h), new Color(0, 0, 0, 0.84f));
            Rect panel = new Rect(20, 36, w - 40, h - 72);
            DrawRect(panel, Panel);
            DrawBorder(panel, new Color(AccentGreen.r, AccentGreen.g, AccentGreen.b, 0.65f), 1.2f);
            DrawText(new Rect(panel.x + 18, panel.y + 15, panel.width - 80, 42), "ПРАВИЛА ИГРЫ", 22, Ink, TextAnchor.MiddleLeft, true);
            DrawButton(new Rect(panel.x + panel.width - 63, panel.y + 18, 45, 34), "ЗАКРЫТЬ", PanelAlt, Ink, true, () => { rulesOpen = false; PlayUiSound(); }, 8);
            float y = panel.y + 74, textW = panel.width - 36;
            DrawText(new Rect(panel.x + 18, y, textW, 48), "ЦЕЛЬ\nЗабей мяч в ворота соперника и первым набери установленное количество очков. Цвет ворот соответствует команде, которая получает очко при попадании.", 12, Ink, TextAnchor.UpperLeft, false); y += 78;
            DrawText(new Rect(panel.x + 18, y, textW, 77), "ХОД\nЗа ход можно провести до трех линий. Мяч перемещается в соседнюю точку по горизонтали, вертикали или диагонали. После третьей линии ход переходит сопернику. После первой или второй линии можно нажать «Конец хода».", 12, Ink, TextAnchor.UpperLeft, false); y += 108;
            DrawText(new Rect(panel.x + 18, y, textW, 102), "ОТСКОК\nПри попадании на границу поля или в точку, где уже есть линии, мяч отскакивает. Следующий ход обязателен, даже если лимит в три линии уже исчерпан. Отскок не сбрасывает счетчик линий. Двигаться вдоль границы и повторять линии нельзя. Диагонали могут пересекаться.", 12, Ink, TextAnchor.UpperLeft, false); y += 132;
            DrawText(new Rect(panel.x + 18, y, textW, 95), "ГОЛ И ТУПИК\nЗа попадание в ворота соперника или в свои ворота очко получает владелец ворот. Если после хода больше нет допустимых линий, очко получает соперник игрока, оказавшегося в тупике. После розыгрыша поле очищается, а начинает игрок, пропустивший очко.", 12, Ink, TextAnchor.UpperLeft, false); y += 117;
            DrawText(new Rect(panel.x + 18, Mathf.Min(y, panel.y + panel.height - 52), textW, 30), "В игре против ИИ сложность влияет на выбор ходов. Все матчи сохраняют счет только на текущем устройстве.", 10, Muted, TextAnchor.UpperLeft, false);
        }

        private void DrawOption(Rect rect, string title, string subtitle, bool active, Action action)
        {
            DrawRect(rect, active ? PanelAlt : Panel);
            DrawBorder(rect, active ? AccentGreen : new Color(1, 1, 1, 0.08f), active ? 1.5f : 1f);
            DrawText(new Rect(rect.x + 8, rect.y + 5, rect.width - 16, 24), title, 13, active ? AccentGreen : Ink, TextAnchor.MiddleCenter, true);
            DrawText(new Rect(rect.x + 6, rect.y + 29, rect.width - 12, 16), subtitle, 9, Muted, TextAnchor.MiddleCenter, false);
            if (GUI.Button(rect, GUIContent.none, invisibleButton)) action();
        }

        private void DrawSmallOption(Rect rect, string label, bool active, Action action, int font = 10)
        {
            DrawRect(rect, active ? PanelAlt : Panel);
            if (active) DrawBorder(rect, AccentGreen, 1.2f);
            else DrawBorder(rect, new Color(1, 1, 1, 0.09f), 1f);
            DrawText(new Rect(rect.x + 2, rect.y + 1, rect.width - 4, rect.height - 2), label, font, active ? AccentGreen : Muted, TextAnchor.MiddleCenter, true);
            if (GUI.Button(rect, GUIContent.none, invisibleButton)) action();
        }

        private void DrawButton(Rect rect, string label, Color fill, Color text, bool enabled, Action action, int font = 12)
        {
            DrawRect(rect, fill);
            DrawBorder(rect, enabled ? new Color(1, 1, 1, 0.14f) : new Color(1, 1, 1, 0.04f), 1f);
            DrawText(new Rect(rect.x + 5, rect.y + 2, rect.width - 10, rect.height - 4), label, font, text, TextAnchor.MiddleCenter, true);
            GUI.enabled = enabled;
            if (GUI.Button(rect, GUIContent.none, invisibleButton)) action();
            GUI.enabled = true;
        }

        private void DrawTextField(Rect rect, ref string value)
        {
            DrawRect(rect, Panel);
            DrawBorder(rect, new Color(1, 1, 1, 0.12f), 1f);
            var style = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 6, 3, 3),
                normal = { textColor = Ink, background = inputTexture }, focused = { textColor = Color.white, background = inputTexture },
                hover = { textColor = Ink, background = inputTexture }, active = { textColor = Ink, background = inputTexture }
            };
            value = GUI.TextField(new Rect(rect.x + 2, rect.y + 1, rect.width - 4, rect.height - 2), value ?? "", 14, style);
            if (value.Length > 14) value = value.Substring(0, 14);
        }

        private void DrawText(Rect rect, string text, int size, Color color, TextAnchor anchor, bool bold)
        {
            string key = size + "|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + (int)anchor + "|" + bold;
            if (!textStyles.TryGetValue(key, out GUIStyle style))
            {
                style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size, alignment = anchor, wordWrap = true,
                    fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                    padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0)
                };
                style.normal.textColor = color;
                style.hover.textColor = color;
                textStyles[key] = style;
            }
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
        }

        private void DrawRect(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = old;
        }

        private void DrawBorder(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
        }

        private void DrawLine(Vector2 a, Vector2 b, Color color, float thickness)
        {
            float dx = b.x - a.x, dy = b.y - a.y;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            if (length < 0.1f) return;
            Vector2 center = (a + b) * 0.5f;
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            Matrix4x4 old = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, center);
            Color oldColor = GUI.color; GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - length * 0.5f, center.y - thickness * 0.5f, length, thickness), pixel);
            GUI.color = oldColor;
            GUI.matrix = old;
        }

        private static Color Hex(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out Color c)) return c;
            return Color.white;
        }
    }
}
