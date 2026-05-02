using UnityEngine;
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instance { get; private set; }

    int _playerUnitIndex;
    int _enemyUnitIndex;
    bool _enemyTurnRunning;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public Unit GetActivePlayerUnit()
    {
        return GetActiveUnit(GameManager.Instance?.playerUnits, ref _playerUnitIndex);
    }

    public Unit GetActiveEnemyUnit()
    {
        return GetActiveUnit(GameManager.Instance?.enemyUnits, ref _enemyUnitIndex);
    }

    public Unit GetActiveUnitForCurrentTurn()
    {
        if (GameManager.Instance == null)
        {
            return null;
        }

        return GameManager.Instance.IsEnemyTurn ? GetActiveEnemyUnit() : GetActivePlayerUnit();
    }

    public void AdvancePlayerUnit()
    {
        AdvanceIndex(GameManager.Instance?.playerUnits, ref _playerUnitIndex);
    }

    public void AdvanceEnemyUnit()
    {
        AdvanceIndex(GameManager.Instance?.enemyUnits, ref _enemyUnitIndex);
    }

    public void RunEnemyTurn()
    {
        if (_enemyTurnRunning || GameManager.Instance == null || GameManager.Instance.CurrentMatchMode == GameManager.MatchMode.Hotseat)
        {
            return;
        }

        var activeEnemy = GetActiveEnemyUnit();
        if (activeEnemy == null)
        {
            GameManager.Instance?.EndEnemyTurn();
            return;
        }

        var ai = activeEnemy.GetComponent<EnemyAI>();
        if (ai == null)
        {
            GameManager.Instance?.EndEnemyTurn();
            return;
        }

        _enemyTurnRunning = true;
        ai.TakeTurn(OnEnemyTurnComplete);
    }

    void OnEnemyTurnComplete()
    {
        _enemyTurnRunning = false;
        GameManager.Instance?.EndEnemyTurn();
    }

    static Unit GetActiveUnit(System.Collections.Generic.List<Unit> units, ref int index)
    {
        if (units == null || units.Count == 0)
        {
            index = 0;
            return null;
        }

        for (int i = units.Count - 1; i >= 0; i--)
        {
            if (units[i] == null || units[i].IsDead)
            {
                units.RemoveAt(i);
            }
        }

        if (units.Count == 0)
        {
            index = 0;
            return null;
        }

        index = ((index % units.Count) + units.Count) % units.Count;
        return units[index];
    }

    static void AdvanceIndex(System.Collections.Generic.List<Unit> units, ref int index)
    {
        if (units == null || units.Count == 0)
        {
            index = 0;
            return;
        }

        index = (index + 1) % units.Count;
    }
}

