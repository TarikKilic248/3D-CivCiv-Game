using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(PlayerController))]
public class ChickPeckAttack : MonoBehaviour
{
    [SerializeField] private float _peckRange = 1.8f;
    [SerializeField] private float _peckRadius = 0.8f;
    [SerializeField] private int _peckDamage = 1;
    [SerializeField] private float _groundPoundForce = 25f;

    public event Action OnPeckPerformed;
    public event Action<Vector3> OnGroundPoundImpact;

    public bool IsGroundPounding { get; private set; }

    private PlayerController _playerController;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
    }

    private void Update()
    {
        if (SettingsUI.IsOpen)
            return;

        if (_playerController != null && IsGroundPounding && _playerController.IsGrounded())
        {
            CompleteGroundPoundImpact();
        }

        HandleAttackInputs();
    }

    private void HandleAttackInputs()
    {
        bool peckPressed = false;
        bool poundPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.fKey.wasPressedThisFrame) peckPressed = true;
            if (kb.leftCtrlKey.wasPressedThisFrame) poundPressed = true;
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            peckPressed = true;
        }
#endif

        try
        {
            if (!peckPressed && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.F)))
                peckPressed = true;
            if (!poundPressed && Input.GetKeyDown(KeyCode.LeftControl))
                poundPressed = true;
        }
        catch
        {
            // Input System paketi tek basina aktifse eski API sessizce atlanir
        }

        if (peckPressed)
        {
            PerformPeck();
        }

        if (poundPressed && _playerController != null && !_playerController.IsGrounded() && !IsGroundPounding)
        {
            StartGroundPound();
        }
    }

    public void PerformPeck()
    {
        Transform orient = _playerController != null ? _playerController.GetOrientation() : transform;
        Vector3 forward = orient != null ? orient.forward : transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.6f + forward * (_peckRange * 0.5f);

        Collider[] hits = Physics.OverlapSphere(origin, _peckRadius, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform) || hit.transform == transform)
                continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                Vector3 hitPoint = hit.ClosestPoint(origin);
                Vector3 hitNormal = (transform.position - hit.transform.position).normalized;
                if (hitNormal.sqrMagnitude < 0.001f) hitNormal = -forward;
                damageable.TakeDamage(_peckDamage, hitPoint, hitNormal);
            }
        }

        OnPeckPerformed?.Invoke();
    }

    public void StartGroundPound()
    {
        if (_playerController == null || _playerController.IsGrounded())
            return;

        IsGroundPounding = true;

        ChickGlideController glide = GetComponent<ChickGlideController>();
        if (glide != null && glide.IsGliding)
        {
            glide.StopGlide();
        }

        Rigidbody rb = _playerController.GetRigidbody();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.AddForce(Vector3.down * _groundPoundForce, ForceMode.VelocityChange);
        }
    }

    private void CompleteGroundPoundImpact()
    {
        IsGroundPounding = false;
        Vector3 impactPos = transform.position;

        Collider[] hits = Physics.OverlapSphere(impactPos, _peckRadius * 1.75f, ~0, QueryTriggerInteraction.Collide);
        foreach (var hit in hits)
        {
            if (hit == null || hit.transform.IsChildOf(transform) || hit.transform == transform)
                continue;

            IDamageable damageable = hit.GetComponentInParent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                Vector3 hitPoint = hit.ClosestPoint(impactPos);
                damageable.TakeDamage(_peckDamage * 2, hitPoint, Vector3.up);
            }
        }

        OnGroundPoundImpact?.Invoke(impactPos);
    }
}
