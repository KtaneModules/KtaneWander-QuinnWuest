using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class WanderScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable[] ArrowSels;
    public KMSelectable MiddleSel;
    public GameObject[] WallObjs;
    public GameObject[] VertexObjs;
    public Material[] WallMats;
    public GameObject[] StarObjs;
    public GameObject MazeParent;
    public TextMesh AliveCountText;
    public TextMesh GoalText;
    public AudioSource ActionAudio;

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private bool[][] _originalWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
    private bool[][] _visitedCells = new bool[4][] { new bool[4], new bool[4], new bool[4], new bool[4] };
    private bool[][] _transformedWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
    private int _wallColor;
    private int[] _currentPositions;
    private bool[] _deadPositions;
    private List<int> _actionHistory = new List<int>();
    private bool _isAnimating;
    private int _aliveCount;
    private int _goal;
    private bool _canMove;
    private bool _firstTimeGettingOne;
    private static readonly string[] _colorNames = new string[] { "BLACK", "BLUE", "GREEN", "CYAN", "RED", "MAGENTA", "YELLOW", "WHITE" };

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int btn = 0; btn < ArrowSels.Length; btn++)
            ArrowSels[btn].OnInteract += ArrowPress(btn);
        MiddleSel.OnInteract += MiddlePress;
        // Debug.LogFormat("[Wander #{0}] 404 Logging not found!", _moduleId);
        // Above was for manual challenge.
        Setup();
    }

    private bool MiddlePress()
    {
        if (_moduleSolved || _isAnimating)
            return false;
        if (!_canMove)
        {
            Audio.PlaySoundAtTransform("MiddlePress", transform);
            _aliveCount = 16;
            AliveCountText.text = _aliveCount.ToString();
            MazeParent.SetActive(false);
            AliveCountText.gameObject.SetActive(true);
            StartCoroutine(PulseObject(AliveCountText.gameObject, new Vector3(0.002f, 0.002f, 0.002f)));
            StartCoroutine(PulseObject(GoalText.gameObject, new Vector3(0.001f, 0.001f, 0.001f)));
            _canMove = true;
            return false;
        }
        _isAnimating = true;
        StartCoroutine(ShowActionHistory());
        return false;
    }

    private KMSelectable.OnInteractHandler ArrowPress(int btn)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, transform);
            ArrowSels[btn].AddInteractionPunch(0.5f);
            if (_moduleSolved || _isAnimating || !_canMove)
                return false;
            _actionHistory.Add(btn);
            _aliveCount = 0;
            StartCoroutine(PulseObject(AliveCountText.gameObject, new Vector3(0.002f, 0.002f, 0.002f)));
            StartCoroutine(PulseObject(GoalText.gameObject, new Vector3(0.001f, 0.001f, 0.001f)));
            for (int i = 0; i < _currentPositions.Length; i++)
            {
                if (!_deadPositions[i] && CheckValidMove(_currentPositions[i], btn))
                {
                    _aliveCount++;
                    _currentPositions[i] = btn == 0 ? (_currentPositions[i] - 4) : btn == 1 ? (_currentPositions[i] + 1) : btn == 2 ? (_currentPositions[i] + 4) : (_currentPositions[i] - 1);
                }
                else
                    _deadPositions[i] = true;
            }
            AliveCountText.text = _aliveCount.ToString();
            var dir = new string[] { "UP", "RIGHT", "DOWN", "LEFT" };
            if (_aliveCount == 1)
            {
                if (_firstTimeGettingOne)
                {
                    _firstTimeGettingOne = false;
                    Debug.LogFormat("[Wander #{0}] Moved {1}. There is now {2} alive star remaining.", _moduleId, dir[btn], _aliveCount);
                    Debug.LogFormat("[Wander #{0}] Revealing goal coordinate: {1}", _moduleId, GetCoord(_goal));
                }
                GoalText.gameObject.SetActive(true);
            }
            else if (_aliveCount == 0)
            {
                _isAnimating = true;
                Debug.LogFormat("[Maze Manual Challenge #{0}] All stars have died. Strike.", _moduleId);
                StartCoroutine(ShowActionHistory());
            }
            else
                Debug.LogFormat("[Wander #{0}] Moved {1}. There are now {2} alive stars remaining.", _moduleId, dir[btn], _aliveCount);
            return false;
        };
    }

    private void Setup()
    {
        _firstTimeGettingOne = true;
        AliveCountText.gameObject.SetActive(false);
        GoalText.gameObject.SetActive(false);
        _goal = Rnd.Range(0, 16);
        GoalText.text = GetCoord(_goal);
        for (int i = 0; i < _originalWalls.Length; i++)
            for (int j = 0; j < _originalWalls[i].Length; j++)
                _originalWalls[i][j] = true;
        for (int i = 0; i < StarObjs.Length; i++)
            StarObjs[i].SetActive(false);
        _deadPositions = new bool[16];
        _currentPositions = Enumerable.Range(0, 16).ToArray();
        var x = Rnd.Range(0, 4);
        var y = Rnd.Range(0, 4);
        GenerateMaze(x, y);
        _wallColor = Rnd.Range(0, 8);
        DoMazeTransformations();
    }

    private void GenerateMaze(int x, int y)
    {
        _visitedCells[x][y] = true;
        var arr = Enumerable.Range(0, 4).ToArray().Shuffle();
        for (int i = 0; i < 4; i++)
        {
            switch (arr[i])
            {
                case 0:
                    if (y != 0 && !_visitedCells[x][y - 1])
                    {
                        _originalWalls[y * 2][x] = false;
                        GenerateMaze(x, y - 1);
                    }
                    break;
                case 1:
                    if (x != 3 && !_visitedCells[x + 1][y])
                    {
                        _originalWalls[y * 2 + 1][x + 1] = false;
                        GenerateMaze(x + 1, y);
                    }
                    break;
                case 2:
                    if (y != 3 && !_visitedCells[x][y + 1])
                    {
                        _originalWalls[y * 2 + 2][x] = false;
                        GenerateMaze(x, y + 1);
                    }
                    break;
                default:
                    if (x != 0 && !_visitedCells[x - 1][y])
                    {
                        _originalWalls[y * 2 + 1][x] = false;
                        GenerateMaze(x - 1, y);
                    }
                    break;
            }
        }
    }

    private void DoMazeTransformations()
    {
        LogMaze(_originalWalls, false);
        _transformedWalls = SetTempWalls(_originalWalls);
        var tempWalls = SetTempWalls(_originalWalls);
        Debug.LogFormat("[Wander #{0}] The color of the walls is {1}.", _moduleId, _colorNames[_wallColor]);
        if ((_wallColor & 4) == 4)
        {
            Debug.LogFormat("[Wander #{0}] Applying RED transformation, horizontal flip.", _moduleId);
            for (int i = 0; i < _transformedWalls.Length; i++)
                for (int j = 0; j < _transformedWalls[i].Length; j++)
                    _transformedWalls[i][j] = tempWalls[i][(_transformedWalls[i].Length - 1) - j];
            tempWalls = SetTempWalls(_transformedWalls);
        }
        if ((_wallColor & 2) == 2)
        {
            Debug.LogFormat("[Wander #{0}] Applying GREEN transformation, vertical flip.", _moduleId);
            for (int i = 0; i < _transformedWalls.Length; i++)
                for (int j = 0; j < _transformedWalls[i].Length; j++)
                    _transformedWalls[i][j] = tempWalls[(_transformedWalls.Length - 1) - i][j];
        }
        if ((_wallColor & 1) == 1)
        {
            Debug.LogFormat("[Wander #{0}] Applying BLUE transformation, row and column swap.", _moduleId);
            var str = "";
            str += "#" + (_transformedWalls[0][0] ? "#" : "-") + "#" + (_transformedWalls[0][1] ? "#" : "-") + "#" + (_transformedWalls[0][2] ? "#" : "-") + "#" + (_transformedWalls[0][3] ? "#" : "-") + "#";
            str += (_transformedWalls[1][0] ? "#" : "-") + "-" + (_transformedWalls[1][1] ? "#" : "-") + "-" + (_transformedWalls[1][2] ? "#" : "-") + "-" + (_transformedWalls[1][3] ? "#" : "-") + "-" + (_transformedWalls[1][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[2][0] ? "#" : "-") + "#" + (_transformedWalls[2][1] ? "#" : "-") + "#" + (_transformedWalls[2][2] ? "#" : "-") + "#" + (_transformedWalls[2][3] ? "#" : "-") + "#";
            str += (_transformedWalls[3][0] ? "#" : "-") + "-" + (_transformedWalls[3][1] ? "#" : "-") + "-" + (_transformedWalls[3][2] ? "#" : "-") + "-" + (_transformedWalls[3][3] ? "#" : "-") + "-" + (_transformedWalls[3][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[4][0] ? "#" : "-") + "#" + (_transformedWalls[4][1] ? "#" : "-") + "#" + (_transformedWalls[4][2] ? "#" : "-") + "#" + (_transformedWalls[4][3] ? "#" : "-") + "#";
            str += (_transformedWalls[5][0] ? "#" : "-") + "-" + (_transformedWalls[5][1] ? "#" : "-") + "-" + (_transformedWalls[5][2] ? "#" : "-") + "-" + (_transformedWalls[5][3] ? "#" : "-") + "-" + (_transformedWalls[5][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[6][0] ? "#" : "-") + "#" + (_transformedWalls[6][1] ? "#" : "-") + "#" + (_transformedWalls[6][2] ? "#" : "-") + "#" + (_transformedWalls[6][3] ? "#" : "-") + "#";
            str += (_transformedWalls[7][0] ? "#" : "-") + "-" + (_transformedWalls[7][1] ? "#" : "-") + "-" + (_transformedWalls[7][2] ? "#" : "-") + "-" + (_transformedWalls[7][3] ? "#" : "-") + "-" + (_transformedWalls[7][4] ? "#" : "-") + "";
            str += "#" + (_transformedWalls[8][0] ? "#" : "-") + "#" + (_transformedWalls[8][1] ? "#" : "-") + "#" + (_transformedWalls[8][2] ? "#" : "-") + "#" + (_transformedWalls[8][3] ? "#" : "-") + "#";
            _transformedWalls = SetTempWalls(GetSwappedWalls(str));
        }
        ShowMaze();
    }

    private bool[][] SetTempWalls(bool[][] walls)
    {
        var tempWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        for (int i = 0; i < walls.Length; i++)
            for (int j = 0; j < walls[i].Length; j++)
                tempWalls[i][j] = walls[i][j];
        return tempWalls;
    }

    private void ShowMaze()
    {
        var str = "";
        LogMaze(_transformedWalls, true);
        for (int i = 0; i < _transformedWalls.Length; i++)
            for (int j = 0; j < _transformedWalls[i].Length; j++)
                str += _transformedWalls[i][j] ? "#" : ".";
        for (int i = 0; i < str.Length; i++)
        {
            WallObjs[i].SetActive(str[i] == '#');
            WallObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
        }
        for (int i = 0; i < VertexObjs.Length; i++)
            VertexObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
    }

    private void LogMaze(bool[][] walls, bool transformed)
    {
        Debug.LogFormat("[Wander #{0}] Maze walls, {1}:", _moduleId, transformed ? "after transformation" : "before transformation");
        var arr = GetMazeString(walls, true);
        for (int i = 0; i < arr.Length; i++)
            Debug.LogFormat("[Wander #{0}] {1}", _moduleId, arr[i]);
    }

    private string[] GetMazeString(bool[][] walls, bool newLines)
    {
        var str = "";
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[0][0] ? "#" : "-", walls[0][1] ? "#" : "-", walls[0][2] ? "#" : "-", walls[0][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[1][0] ? "#" : "-", walls[1][1] ? "#" : "-", walls[1][2] ? "#" : "-", walls[1][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[2][0] ? "#" : "-", walls[2][1] ? "#" : "-", walls[2][2] ? "#" : "-", walls[2][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[3][0] ? "#" : "-", walls[3][1] ? "#" : "-", walls[3][2] ? "#" : "-", walls[3][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[4][0] ? "#" : "-", walls[4][1] ? "#" : "-", walls[4][2] ? "#" : "-", walls[4][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[5][0] ? "#" : "-", walls[5][1] ? "#" : "-", walls[5][2] ? "#" : "-", walls[5][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#{4}", walls[6][0] ? "#" : "-", walls[6][1] ? "#" : "-", walls[6][2] ? "#" : "-", walls[6][3] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("{0}-{1}-{2}-{3}-{4}{5}", walls[7][0] ? "#" : "-", walls[7][1] ? "#" : "-", walls[7][2] ? "#" : "-", walls[7][3] ? "#" : "-", walls[1][4] ? "#" : "-", newLines ? "\n" : "");
        str += string.Format("#{0}#{1}#{2}#{3}#", walls[8][0] ? "#" : "-", walls[8][1] ? "#" : "-", walls[8][2] ? "#" : "-", walls[8][3] ? "#" : "-");
        return str.Split('\n');
    }

    private bool[][] GetSwappedWalls(string str)
    {
        var walls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        for (int i = 0; i < walls.Length; i++)
        {
            for (int j = 0; j < walls[i].Length; j++)
            {
                if (i % 2 == 0)
                    walls[i][j] = str[(j * 18 + 9) + i] == '#';
                else
                    walls[i][j] = str[(j * 18) + i] == '#';
            }
        }
        return walls;
    }

    private void Reset()
    {
        MazeParent.SetActive(true);
        _canMove = false;
        _originalWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        _visitedCells = new bool[4][] { new bool[4], new bool[4], new bool[4], new bool[4] };
        _transformedWalls = new bool[9][] { new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4], new bool[5], new bool[4] };
        _actionHistory = new List<int>();
        _isAnimating = false;
        Setup();
    }

    private bool CheckValidMove(int num, int dir)
    {
        var pos = num / 4 * 16 + 8 + (num / 4 * 2) + num % 4 * 2 + 2;
        var walls = GetMazeString(_originalWalls, false).Join("");
        if (dir == 0)
            return walls[pos - 9] == '-';
        if (dir == 1)
            return walls[pos + 1] == '-';
        if (dir == 2)
            return walls[pos + 9] == '-';
        else
            return walls[pos - 1] == '-';
    }

    private IEnumerator ShowActionHistory()
    {
        ActionAudio.Play();
        GoalText.gameObject.SetActive(false);
        AliveCountText.gameObject.SetActive(false);
        var current = Enumerable.Range(0, 16).ToArray();
        var dead = new bool[16];
        for (int i = 0; i < StarObjs.Length; i++)
            StarObjs[i].SetActive(true);
        yield return new WaitForSeconds(0.964f);
        if (_actionHistory.Count == 0)
        {
            for (int i = 0; i < 16; i++)
            {
                StarObjs[i].SetActive(true);
                StartCoroutine(PulseObject(StarObjs[i], new Vector3(0.15f, 0.15f, 0.15f)));
            }
            yield return new WaitForSeconds(0.964f);
        }
        else
        {
            for (int a = 0; a < _actionHistory.Count; a++)
            {
                var alive = new List<int>();
                for (int i = 0; i < current.Length; i++)
                {
                    if (!dead[i] && CheckValidMove(current[i], _actionHistory[a]))
                    {
                        current[i] = _actionHistory[a] == 0 ? (current[i] - 4) : _actionHistory[a] == 1 ? (current[i] + 1) : _actionHistory[a] == 2 ? (current[i] + 4) : (current[i] - 1);
                        alive.Add(current[i]);
                    }
                    else
                        dead[i] = true;
                }
                for (int i = 0; i < 16; i++)
                {
                    StarObjs[i].SetActive(alive.Contains(i));
                    StartCoroutine(PulseObject(StarObjs[i], new Vector3(0.15f, 0.15f, 0.15f)));
                }
                yield return new WaitForSeconds(0.964f);
            }
        }
        ActionAudio.Stop();
        if (_aliveCount == 0)
        {
            Module.HandleStrike();
            Reset();
            yield break;
        }
        if (_aliveCount == 1)
        {
            int curPos = -1;
            for (int i = 0; i < _currentPositions.Length; i++)
                if (!_deadPositions[i])
                    curPos = _currentPositions[i];
            if (curPos == _goal)
            {
                _moduleSolved = true;
                Module.HandlePass();
                Audio.PlaySoundAtTransform("Solve", transform);
                MazeParent.SetActive(true);
                var str = "";
                for (int i = 0; i < _originalWalls.Length; i++)
                    for (int j = 0; j < _originalWalls[i].Length; j++)
                        str += _originalWalls[i][j] ? "#" : ".";
                for (int i = 0; i < str.Length; i++)
                {
                    WallObjs[i].SetActive(str[i] == '#');
                    WallObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
                }
                for (int i = 0; i < VertexObjs.Length; i++)
                    VertexObjs[i].GetComponent<MeshRenderer>().material = WallMats[_wallColor];
                Debug.LogFormat("[Maze Manual Challenge #{0}] Successfully submitted at position {1}. Module solved.", _moduleId, GetCoord(curPos));
                for (int i = 0; i < StarObjs.Length; i++)
                    StarObjs[i].GetComponent<MeshRenderer>().material = WallMats[2];
                yield break;
            }
            else
            {
                Debug.LogFormat("[Maze Manual Challenge #{0}] Incorrectly submitted at position {1}. Strike.", _moduleId, GetCoord(curPos));
                Module.HandleStrike();
                Reset();
            }
        }
        else
        {
            Debug.LogFormat("[Maze Manual Challenge #{0}] Attempted to submit when there were multiple live positions. Strike.", _moduleId);
            Module.HandleStrike();
            Reset();
        }
    }

    private IEnumerator PulseObject(GameObject obj, Vector3 scale)
    {
        var duration = 0.2f;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            obj.transform.localScale = new Vector3(Easing.InOutQuad(elapsed, scale.x * 1.2f, scale.x, duration), Easing.InOutQuad(elapsed, scale.y * 1.2f, scale.y, duration), Easing.InOutQuad(elapsed, scale.z * 1.2f, scale.z, duration));
            yield return null;
            elapsed += Time.deltaTime;
        }
        obj.transform.localScale = new Vector3(scale.x, scale.y, scale.z);
    }

    private string GetCoord(int num)
    {
        return "ABCD"[num % 4].ToString() + "1234"[num / 4].ToString();
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press urdl [Press the up, right, down, left buttons.] | !{0} press submit [Press the submit button.] | 'press' is optional.";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        yield return null;
        if (_isAnimating)
        {
            yield return "sendtochaterror You cannot interact with the module during its animation! Command ignored.";
            yield break;
        }
        var m = Regex.Match(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            yield return "solve";
            yield return "strike";
            MiddleSel.OnInteract();
            yield break;
        }
        if (!_canMove)
        {
            yield return "sendtochaterror You cannot press the arrow buttons during this phase! Command ignored.";
            yield break;
        }
        command = (command.ToUpperInvariant().StartsWith("PRESS ") ? command.Substring(6) : command).ToUpperInvariant();
        var list = new List<int>();
        for (int i = 0; i < command.Length; i++)
        {
            var ix = "URDL ".IndexOf(command[i]);
            if (ix == 4)
                continue;
            if (ix == -1)
            {
                yield return "sendtochaterror " + command[i] + "is not a valid movement! Command ignored.";
                yield break;
            }
            list.Add(ix);
        }
        yield return null;
        yield return "solve";
        yield return "strike";
        for (int i = 0; i < list.Count; i++)
        {
            ArrowSels[list[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    struct QueueItem
    {
        public int Cell;
        public int Parent;
        public int Direction;
        public QueueItem(int cell, int parent, int dir)
        {
            Cell = cell;
            Parent = parent;
            Direction = dir;
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        // Go to movement phase.
        if (!_canMove)
        {
            MiddleSel.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }

        // If there is only one star left, don't attempt to kill off stars.
        if (_aliveCount == 1)
            goto oneLeft;

        // Move from the current position to the furthest corner, in an attempt to kill off all but one star.
        var visited1 = new Dictionary<int, QueueItem>();
        var q1 = new Queue<QueueItem>();
        int cur1 = -1;
        for (int i = 0; i < _currentPositions.Length; i++)
            if (!_deadPositions[i])
                cur1 = _currentPositions[i];
        int sol1 =
            cur1 % 4 < 2 && cur1 / 4 < 2 ? 15 :
            cur1 % 4 < 2 && cur1 / 4 > 1 ? 3 :
            cur1 % 4 > 1 && cur1 / 4 < 2 ? 0 :
            12;
        // More specifically, identify the quadrant of the current position, and travel to the corner in the opposite quadrant.
        q1.Enqueue(new QueueItem(cur1, -1, 0));
        while (q1.Count > 0)
        {
            var qi = q1.Dequeue();
            if (visited1.ContainsKey(qi.Cell))
                continue;
            visited1[qi.Cell] = qi;
            if (qi.Cell == sol1)
                break;
            if (CheckValidMove(qi.Cell, 0))
                q1.Enqueue(new QueueItem(qi.Cell - 4, qi.Cell, 0));
            if (CheckValidMove(qi.Cell, 1))
                q1.Enqueue(new QueueItem(qi.Cell + 1, qi.Cell, 1));
            if (CheckValidMove(qi.Cell, 2))
                q1.Enqueue(new QueueItem(qi.Cell + 4, qi.Cell, 2));
            if (CheckValidMove(qi.Cell, 3))
                q1.Enqueue(new QueueItem(qi.Cell - 1, qi.Cell, 3));
        }
        var r1 = sol1;
        var path1 = new List<int>();
        while (true)
        {
            var nr = visited1[r1];
            if (nr.Parent == -1)
                break;
            path1.Add(nr.Direction);
            r1 = nr.Parent;
        }
        for (int i = 0; i < path1.Count - 1; i++)
        {
            var d = path1[(path1.Count - 1) - i];
            ArrowSels[d].OnInteract();
            yield return new WaitForSeconds(0.1f);
            if (_aliveCount == 1) // Stop the movement path prematurely if there's only one star left.
                goto oneLeft;
        }
        
        // Now that one star is left, travel to the goal.
        oneLeft:;
        var visited2 = new Dictionary<int, QueueItem>();
        var q2 = new Queue<QueueItem>();
        int cur2 = -1;
        for (int i = 0; i < _currentPositions.Length; i++)
            if (!_deadPositions[i])
                cur2 = _currentPositions[i];
        q2.Enqueue(new QueueItem(cur2, -1, 0));
        if (cur2 == _goal)
            goto atGoal;
        while (q2.Count > 0)
        {
            var qi = q2.Dequeue();
            if (visited2.ContainsKey(qi.Cell))
                continue;
            visited2[qi.Cell] = qi;
            if (qi.Cell == _goal)
                break;
            if (CheckValidMove(qi.Cell, 0))
                q2.Enqueue(new QueueItem(qi.Cell - 4, qi.Cell, 0));
            if (CheckValidMove(qi.Cell, 1))
                q2.Enqueue(new QueueItem(qi.Cell + 1, qi.Cell, 1));
            if (CheckValidMove(qi.Cell, 2))
                q2.Enqueue(new QueueItem(qi.Cell + 4, qi.Cell, 2));
            if (CheckValidMove(qi.Cell, 3))
                q2.Enqueue(new QueueItem(qi.Cell - 1, qi.Cell, 3));
        }
        var r2 = _goal;
        var path2 = new List<int>();
        while (true)
        {
            var nr = visited2[r2];
            if (nr.Parent == -1)
                break;
            path2.Add(nr.Direction);
            r2 = nr.Parent;
        }
        for (int i = path2.Count - 1; i >= 0; i--)
        {
            ArrowSels[path2[i]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        atGoal:
        MiddleSel.OnInteract();
        while (!_moduleSolved)
            yield return true;
    }
}
