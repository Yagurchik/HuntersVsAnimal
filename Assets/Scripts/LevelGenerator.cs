using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    [SerializeField] private Vector2Int size;
    public GameObject brickPrefab;
    [SerializeField] private Settings m_Settings;
    [SerializeField] public List<GameObject> EnemyCollection = new List<GameObject>();
    [SerializeField] private List<Brick> BrickCollection = new List<Brick>();
    private List<Vector3> transformsSize = new List<Vector3>();
    private List<int> _posTransform = new List<int>();

    private void Start()
    {
        if (GameManager.Instance.stage == 1)
        {
            GameManager.Instance.rooms.Add(this);
            for (int i = 0; i < 11; i++)
            {
                for (int j = 0; j < size.y; j++)
                {
                    transformsSize.Add(new Vector3(i, -j, 0));
                }
            }
            CreateCell();
        }
    }
    void OnEnable()
    {
        if (GameManager.Instance.stage != 1)
        {
            GameManager.Instance.rooms.Add(this);
            for (int i = 0; i < 11; i++)
            {
                for (int j = 0; j < size.y; j++)
                {
                    transformsSize.Add(new Vector3(i, -j, 0));
                }
            }
            CreateCell();
        }
    }
    private void Update()
    {
        if (GameManager.Instance.stepGame == true)
        {
            List<LevelGenerator> roomsCopy = new List<LevelGenerator>(GameManager.Instance.rooms);
            foreach (LevelGenerator item in roomsCopy)
            {
                Debug.Log(this);
                item.StepEnemy();
                    if (item.m_Settings._enemyAddWaves > 0)
                    {
                        item.m_Settings._enemyAddWaves--;
                        item.CreateCell();
                    }
            }
            GameManager.Instance.stepGame = false;
        }
    }
    private void CreateCell()
    {
        _posTransform.Clear();
        for (int y = 0; y < transformsSize.Count; y++)
        {
            _posTransform.Add(y);
        }
        Vector3 _pos = transform.position;
        float enemySet = m_Settings._enemySet * EnemyHard();
        for (int i = 0; i < enemySet; i++)
        {
            if (_posTransform.Count > 0)
            {
                int x = GetRandomElement(_posTransform);
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
        }
        else if(m_Settings._enemyAddWaves !=0)
        {
            if (GameManager.Instance.stepPlayer == false)
            {
                GameManager.Instance.stepPlayer = true;
                return;
            }
        }
        else
        {
            if (GameManager.Instance.stage < GameManager.Instance.maxStage)
            {
                GameManager.Instance.NextStage();
            }
            else if(GameManager.Instance.rooms.Count == 1)
            {
                GameManager.Instance.WinGame();
            }
            Destroy(this);

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
    private void OnDestroy()
    {
        GameManager.Instance.rooms.Remove(this);
    }
}
