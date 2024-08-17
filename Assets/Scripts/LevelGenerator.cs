using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    public Vector2Int size;
    public GameObject brickPrefab;
    [SerializeField] private Settings m_Settings;
    [SerializeField] public List<GameObject> EnemyCollection = new List<GameObject>();
    [SerializeField] private List<Brick> BrickCollection = new List<Brick>();
    private List<Vector3> transformsSize = new List<Vector3>();
    private List<int> _posTransform = new List<int>();
    public float losePos;
    public bool lose;

    void Awake()
    {
        for (int i = 0; i < 11; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                transformsSize.Add(new Vector3(i, -j, 0));
            }
        }
        CreateCell();
    }
    private void Update()
    {
        if (GameManager.Instance.stepGame == true)
        {
            GameManager.Instance.stepGame = false;
            Debug.Log("Ход врага");
            StepEnemy();
            if (m_Settings._enemyAddWaves > 0)
            {
                m_Settings._enemyAddWaves--;
                CreateCell();
            }
        }
    }
    private void CreateCell()
    {
        _posTransform.Clear();
        for (int y = 0; y < transformsSize.Count; y++)
        {
            _posTransform.Add(y);
            Debug.Log(y);
        }
        Vector3 _pos = transform.position;
        float enemySet = m_Settings._enemySet * EnemyHard();
        for (int i = 0; i < enemySet; i++)
        {
            if (_posTransform.Count > 0)
            {
                int x = GetRandomElement(_posTransform);
                Debug.Log(x);
                Vector3 newPosition = _pos + transformsSize[x];
                _posTransform.Remove(x);

                bool positionOccupied = false;
                foreach (GameObject enemy in EnemyCollection)
                {
                    if (enemy.transform.position == newPosition)
                    {
                        positionOccupied = true;
                        break;
                    }
                }

                if (!positionOccupied)
                {
                    GameObject newBrick = Instantiate(brickPrefab, transform);
                    Brick brick = newBrick.GetComponent<Brick>();
                    brick.levelGenerator = this;
                    newBrick.transform.position = newPosition;
                    EnemyCollection.Add(newBrick);
                }
                else
                {
                    i--;
                }
            }
            else
            {
                return;
            }
        }
    }
    public void StepEnemy()
    {
        if (EnemyCollection.Count > 0)
        {
            BrickCollection.Clear();
            foreach (GameObject enemy in EnemyCollection)
            {
                Brick brick = enemy.GetComponent<Brick>();
                if (brick != null)
                {
                    BrickCollection.Add(brick);
                }
            }
            foreach (Brick brick in BrickCollection)
            {
                brick.Step();
            }
            if (GameManager.Instance.stepPlayer == false)
            {
                GameManager.Instance.stepPlayer = true;
            }
            else
            {
                Debug.Log("YOU LOSE");
            }
        }
        else
        {
            Debug.Log("Win");
            GameManager.Instance.WinGame();
        }
    }
    public void DestroyEnemy(GameObject enemy)
    {
        if (EnemyCollection.Contains(enemy))
        {
            EnemyCollection.Remove(enemy);
        }
    }
    public float EnemyHard()
    {
        float ratio = (float)EnemyCollection.Count / 55;
        if (ratio == 0)
        {
            return 1;
        }
        if (ratio < 0.3)
        {
            return 0.7f;
        }
        if (ratio < 0.6)
        {
            return 0.4f;
        }
        if (ratio < 0.9)
        {
            return 0.1f;
        }
        return 0;
    }
    int GetRandomElement(List<int> _posTransform)
    {
        int randomIndex = Random.Range(0, _posTransform.Count);
        return _posTransform[randomIndex];
    }
}
