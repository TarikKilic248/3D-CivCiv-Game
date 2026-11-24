using System.Collections;
using UnityEngine;

public class CoinCollect : MonoBehaviour, ICollectable
{
    [SerializeField] private int value = 1;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobAmplitude = 0.15f;
    [SerializeField] private float _bobFrequency = 2f;
    [SerializeField] private AudioClip _collectSfx;

    private Vector3 _startPosition;
    private Vector3 _initialScale;
    private bool _isCollected;

    private void Awake()
    {
        _startPosition = transform.position;
        _initialScale = transform.localScale;
    }

    private void Update()
    {
        if (!_isCollected)
        {
            AnimateCoin();
        }
    }

    private void AnimateCoin()
    {
        transform.Rotate(0f, _rotationSpeed * Time.deltaTime, 0f, Space.World);
        float yOffset = Mathf.Sin(Time.time * _bobFrequency) * _bobAmplitude;
        transform.position = _startPosition + new Vector3(0f, yOffset, 0f);
    }

    public void Collect()
    {
        if (_isCollected)
            return;

        if (CoinManager.Instance == null)
        {
            Debug.LogError("CoinManager is missing from the scene.", this);
            return;
        }

        if (CoinManager.Instance.AddCoins(value))
        {
            _isCollected = true;
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
            SettingsUI.PlaySFX(_collectSfx);
            StartCoroutine(CollectAnimationRoutine());
        }
    }

    private IEnumerator CollectAnimationRoutine()
    {
        const float duration = 0.15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.Lerp(_initialScale, Vector3.zero, t);
            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected)
            return;

        if (other.GetComponentInParent<PlayerController>() == null)
            return;

        Collect();
    }
}
