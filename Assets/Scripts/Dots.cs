using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class Dots : MonoBehaviour
{
    public Sprite dot;
    [Range(0.01f, 1f)] public float size;
    [Range(0.1f, 1f)] public float delta;
    [Range(0, 1f)] public float alpha;

    public static Dots instance;

    List<Vector2> positions = new List<Vector2>();
    List<GameObject> dots = new List<GameObject>();
    public void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
    }
    private void FixedUpdate()
    {
        if (positions.Count > 0)
        {
            DestroyAllDots();
            positions.Clear();
        }
    }
    private void DestroyAllDots()
    {
        foreach (var dot in dots) 
        {
            Destroy(dot);
        }
        dots.Clear();
    }
    GameObject GetOneDot()
    {
        var gameObject = new GameObject();
        gameObject.transform.localScale = Vector3.one * size;
        gameObject.transform.parent = transform;

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.color = new Color(1,1,1,alpha);
        sr.sprite = dot;

        return gameObject;
    }
    public void DrawDottedLine (Vector2 start, Vector2 end)
    {
        DestroyAllDots ();
        Vector2 point = start;
        Vector2 direction = (end-start).normalized;
        while((end - start).magnitude > (point-start).magnitude)
            { 
            positions.Add(point);
            point += (direction * delta);
            }
        Render();
    }
    private void Render()
    {
        foreach(var position in positions)
        {
            var g = GetOneDot();
            g.transform.position = position;
            dots.Add(g);
        }
    }
}
