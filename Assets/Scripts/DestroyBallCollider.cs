using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class DestroyBallCollider : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        CompleteProjectile(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        CompleteProjectile(other);
    }

    private void CompleteProjectile(Collider2D other)
    {
        Animal animal = other.GetComponentInParent<Animal>();
        if (animal == null || animal.IsReturning) return;
        BoarProjectile boar = animal.GetComponent<BoarProjectile>();
        if (boar != null)
        {
            boar.BounceFromBottomZone(GetComponent<Collider2D>(), other);
            return;
        }
        animal.CompleteAtBottom();
    }
}
