using System.Collections;
using UnityEngine;

public class CoinMagnet : MonoBehaviour
{
    [SerializeField] private float _defaultMagnetRadius = 3f;
    [SerializeField] private float _pullSpeed = 14f;

    private bool _isSuperMagnetActive;
    private float _superMagnetRadius;
    private Coroutine _superMagnetRoutine;

    public float CurrentRadius => _isSuperMagnetActive ? _superMagnetRadius : _defaultMagnetRadius;

    private void Update()
    {
        PullNearbyCoins();
    }

    public void ActivateSuperMagnet(float radius, float duration)
    {
        if (radius <= 0f || duration <= 0f)
            return;

        if (_superMagnetRoutine != null)
        {
            StopCoroutine(_superMagnetRoutine);
        }

        _superMagnetRoutine = StartCoroutine(SuperMagnetRoutine(radius, duration));
    }

    private IEnumerator SuperMagnetRoutine(float radius, float duration)
    {
        _isSuperMagnetActive = true;
        _superMagnetRadius = Mathf.Max(_defaultMagnetRadius, radius);

        yield return new WaitForSeconds(duration);

        _isSuperMagnetActive = false;
        _superMagnetRoutine = null;
    }

    private void PullNearbyCoins()
    {
        float currentRadius = CurrentRadius;
        if (currentRadius <= 0f)
            return;

        Vector3 targetPos = transform.position + Vector3.up * 0.5f;
        Collider[] hits = Physics.OverlapSphere(targetPos, currentRadius, ~0, QueryTriggerInteraction.Collide);

        foreach (var hit in hits)
        {
            if (hit == null)
                continue;

            CoinCollect coin = hit.GetComponentInParent<CoinCollect>();
            if (coin == null)
                continue;

            coin.transform.position = Vector3.MoveTowards(
                coin.transform.position,
                targetPos,
                _pullSpeed * Time.deltaTime);
        }
    }
}
