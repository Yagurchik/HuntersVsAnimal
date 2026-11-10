using System.Collections.Generic;
using UnityEngine;

// A salt puddle belongs to the volley in which its bearer died.
public sealed class BrineHazard : MonoBehaviour
{
    private readonly HashSet<Animal> touched = new HashSet<Animal>();
    private readonly Collider2D[] contacts = new Collider2D[24];
    private int volleyId;

    public static void Spawn(LevelGenerator room, Vector3 position)
    {
        if (room == null || Player.CurrentVolleyId <= 0)
        {
            return;
        }

        GameObject puddle = new GameObject("Salt Puddle", typeof(BrineHazard));
        puddle.transform.SetParent(room.transform, true);
        puddle.transform.position = position;
        BrineHazard hazard = puddle.GetComponent<BrineHazard>();
        hazard.volleyId = Player.CurrentVolleyId;
        hazard.CreateVisual();
    }

    private void FixedUpdate()
    {
        if (Player.CurrentVolleyId != volleyId)
        {
            Destroy(gameObject);
            return;
        }
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, 0.4f, contacts);
        for (int i = 0; i < count; i++)
        {
            Animal projectile = contacts[i] != null
                ? contacts[i].GetComponentInParent<Animal>() : null;
            if (projectile == null || !touched.Add(projectile))
            {
                continue;
            }

            if (projectile.TakeEnemyDamage(1f))
            {
                Destroy(projectile.gameObject);
            }
        }
    }

    private void CreateVisual()
    {
        LineRenderer outline = gameObject.AddComponent<LineRenderer>();
        outline.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        outline.useWorldSpace = false;
        outline.loop = true;
        outline.positionCount = 24;
        outline.startWidth = 0.07f;
        outline.endWidth = 0.07f;
        outline.startColor = new Color(0.54f, 0.91f, 0.84f, 0.7f);
        outline.endColor = outline.startColor;
        outline.sortingOrder = 21;
        for (int i = 0; i < 24; i++)
        {
            float angle = Mathf.PI * 2f * i / 24f;
            outline.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.4f,
                Mathf.Sin(angle) * 0.23f, 0f));
        }
    }

    private void OnDestroy()
    {
        LineRenderer outline = GetComponent<LineRenderer>();
        if (outline != null && outline.sharedMaterial != null)
        {
            Destroy(outline.sharedMaterial);
        }
    }
}
