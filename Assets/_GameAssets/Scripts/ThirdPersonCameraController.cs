using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unity.Cinemachine;

public enum CameraPerspective
{
    ThirdPerson,
    FirstPerson
}

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("--- References ---")]
    [Tooltip("Ana Player objesi (Karakteriniz)")]
    [SerializeField] private Transform _playerTransform;
    [Tooltip("Karakterin 3D modeli (Mesh / Visual nesnesi)")]
    [SerializeField] private Transform _playerVisualTransform;
    [Tooltip("Orientation nesnesi (İsteğe bağlıdır. Boş bırakırsanız karakter doğrudan farenin yönüne döner!)")]
    [SerializeField] private Transform _orientationTransform;

    [Header("--- Cinemachine Cameras ---")]
    [Tooltip("3. Şahıs (TPS) Cinemachine Kamerası")]
    [SerializeField] private CinemachineCamera _tpsCamera;
    [Tooltip("1. Şahıs (FPS) Cinemachine Kamerası")]
    [SerializeField] private CinemachineCamera _fpsCamera;

    [Header("--- Mouse Rotation (Fare ile Dönüş) ---")]
    [Tooltip("Karakter fare / kamera yönüne doğru dönsün mü? (Orientation gerektirmez)")]
    [SerializeField] private bool _rotateWithMouse = true;
    [Tooltip("TPS modunda karakterin fare/kamera yönüne dönme hızı")]
    [SerializeField] private float _tpsRotationSpeed = 15f;
    [Tooltip("FPS modunda anlık dönüş (FPS oyunlarındaki gibi gecikmesiz)")]
    [SerializeField] private bool _instantTurnInFPS = true;
    [Tooltip("Doğrudan fare (Mouse X) hassasiyeti ile dönüş ekle")]
    [SerializeField] private bool _useDirectMouseX = false;
    [SerializeField] private float _mouseSensitivity = 3f;

    [Header("--- Perspective Settings ---")]
    [Tooltip("Başlangıçtaki kamera modu")]
    [SerializeField] private CameraPerspective _currentPerspective = CameraPerspective.ThirdPerson;
    [Tooltip("Kamera açısını değiştirme tuşu (Varsayılan: V)")]
    [SerializeField] private KeyCode _switchKey = KeyCode.V;
    [Tooltip("FPS modunda karakter modelini gizle (kafanın ekrana girmesini engeller)")]
    [SerializeField] private bool _hideVisualInFPS = true;

    [Header("--- Cursor Settings ---")]
    [Tooltip("Oyun başladığında fare imlecini kilitlesin ve gizlesin mi?")]
    [SerializeField] private bool _lockCursor = true;

    // Aktif kamera öncelikleri (Cinemachine Priority)
    private const int HIGH_PRIORITY = 20;
    private const int LOW_PRIORITY = 10;

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;

        // Eksik referansları otomatik toparla
        AutoFindReferences();
        EnsureCamerasRuntime();
    }

    private void Start()
    {
        if (_lockCursor)
        {
            SetCursorLock(true);
        }

        ApplyPerspective(_currentPerspective);
    }

    private void Update()
    {
        HandleInputs();
        RotateCharacterWithMouse();
    }

    private void LateUpdate()
    {
        // LateUpdate'te de kamera güncellemelerine karşı hizalama tazelenir
        if (_rotateWithMouse && _currentPerspective == CameraPerspective.FirstPerson)
        {
            AlignPlayerWithCameraForward(true);
        }
    }

    /// <summary>
    /// Karakterin farenin baktığı yatay yöne dönmesini sağlar (Orientation transformuna ihtiyaç kalmaz!)
    /// </summary>
    private void RotateCharacterWithMouse()
    {
        if (_playerTransform == null) return;

        // 1. Doğrudan fare X hareketiyle çevirme seçeneği
        if (_useDirectMouseX)
        {
            float mouseX = GetMouseXInput();
            if (Mathf.Abs(mouseX) > 0.001f)
            {
                _playerTransform.Rotate(Vector3.up, mouseX * _mouseSensitivity);
            }
        }

        // 2. Kameranın baktığı yatay açıya göre karakteri döndürme
        if (_rotateWithMouse)
        {
            bool instant = (_currentPerspective == CameraPerspective.FirstPerson && _instantTurnInFPS);
            AlignPlayerWithCameraForward(instant);
        }

        // 3. Karakter modelini gövdeyle senkronize tut
        if (_playerVisualTransform != null)
        {
            _playerVisualTransform.localRotation = Quaternion.identity;
        }

        // 4. Eğer eski Orientation transformu hala sahnede bağlıysa onu da senkronize et
        if (_orientationTransform != null)
        {
            _orientationTransform.rotation = _playerTransform.rotation;
        }
    }

    private void AlignPlayerWithCameraForward(bool instant)
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) return;
        }

        Vector3 camForward = _mainCamera.transform.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude < 0.001f) return;
        camForward.Normalize();

        Quaternion targetRot = Quaternion.LookRotation(camForward);

        if (instant)
        {
            _playerTransform.rotation = targetRot;
        }
        else
        {
            _playerTransform.rotation = Quaternion.Slerp(_playerTransform.rotation, targetRot, Time.deltaTime * _tpsRotationSpeed);
        }
    }

    private float GetMouseXInput()
    {
        float mx = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            mx = Mouse.current.delta.x.ReadValue() * 0.1f;
        }
#endif
        try
        {
            if (Mathf.Approximately(mx, 0f))
            {
                mx = Input.GetAxis("Mouse X");
            }
        }
        catch { }

        return mx;
    }

    [ContextMenu("Referansları Otomatik Bağla (Auto Find)")]
    public void AutoFindReferences()
    {
        // 1. Player Transform
        if (_playerTransform == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) _playerTransform = pc.transform;
            else _playerTransform = transform;
        }

        // 2. Player Visual (Mesh / 3D Model)
        if (_playerVisualTransform == null && _playerTransform != null)
        {
            foreach (Transform child in _playerTransform)
            {
                string n = child.name.ToLower();
                if (n.Contains("visual") || n.Contains("model") || n.Contains("chr") || n.Contains("street") || n.Contains("mesh"))
                {
                    _playerVisualTransform = child;
                    break;
                }
            }
            if (_playerVisualTransform == null)
            {
                var smr = _playerTransform.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) _playerVisualTransform = smr.transform;
                else
                {
                    var mr = _playerTransform.GetComponentInChildren<MeshRenderer>();
                    if (mr != null) _playerVisualTransform = mr.transform;
                }
            }
        }

        // 3. Orientation Transform (İsteğe bağlı, artık zorunlu değil)
        if (_orientationTransform == null && _playerTransform != null)
        {
            var orient = _playerTransform.Find("Orientation");
            if (orient != null) _orientationTransform = orient;
        }

        // 4. Cinemachine Kameraları Sahneden Otomatik Bul
        if (_tpsCamera == null || _fpsCamera == null)
        {
            var allCams = FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include);
            foreach (var cam in allCams)
            {
                string n = cam.name.ToLower();
                if (_tpsCamera == null && (n.Contains("tps") || n.Contains("third")))
                {
                    _tpsCamera = cam;
                }
                else if (_fpsCamera == null && (n.Contains("fps") || n.Contains("first")))
                {
                    _fpsCamera = cam;
                }
            }

            if (_tpsCamera == null && allCams.Length > 0) _tpsCamera = allCams[0];
            if (_fpsCamera == null && allCams.Length > 1 && allCams[1] != _tpsCamera) _fpsCamera = allCams[1];
        }
    }

    private void EnsureCamerasRuntime()
    {
        if (_mainCamera != null && _mainCamera.GetComponent<CinemachineBrain>() == null)
        {
            _mainCamera.gameObject.AddComponent<CinemachineBrain>();
        }

        if (_tpsCamera == null && _playerTransform != null)
        {
            GameObject tpsObj = new GameObject("CM_TPS_Camera");
            _tpsCamera = tpsObj.AddComponent<CinemachineCamera>();
            _tpsCamera.Target.TrackingTarget = _playerTransform;
            _tpsCamera.Priority = HIGH_PRIORITY;
        }

        if (_fpsCamera == null && _playerTransform != null)
        {
            Transform fpsPos = _playerTransform.Find("CameraPos_FPS");
            if (fpsPos == null)
            {
                GameObject fPos = new GameObject("CameraPos_FPS");
                fPos.transform.SetParent(_playerTransform);
                fPos.transform.localPosition = new Vector3(0f, 1.6f, 0.15f);
                fpsPos = fPos.transform;
            }

            GameObject fpsObj = new GameObject("CM_FPS_Camera");
            _fpsCamera = fpsObj.AddComponent<CinemachineCamera>();
            _fpsCamera.Target.TrackingTarget = fpsPos;
            _fpsCamera.Priority = LOW_PRIORITY;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoFindReferences();
    }

    private void Reset()
    {
        AutoFindReferences();
    }
#endif

    private void HandleInputs()
    {
        bool switchPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.vKey.wasPressedThisFrame) switchPressed = true;
            if (kb.escapeKey.wasPressedThisFrame) ToggleCursorLock();
        }
#endif

        try
        {
            if (!switchPressed && Input.GetKeyDown(_switchKey)) switchPressed = true;
            if (Input.GetKeyDown(KeyCode.Escape)) ToggleCursorLock();
        }
        catch { }

        if (switchPressed)
        {
            TogglePerspective();
        }
    }

    public void TogglePerspective()
    {
        CameraPerspective newPerspective = (_currentPerspective == CameraPerspective.ThirdPerson)
            ? CameraPerspective.FirstPerson
            : CameraPerspective.ThirdPerson;

        ApplyPerspective(newPerspective);
    }

    public void ApplyPerspective(CameraPerspective perspective)
    {
        _currentPerspective = perspective;
        bool isFPS = (_currentPerspective == CameraPerspective.FirstPerson);

        if (_tpsCamera != null)
        {
            _tpsCamera.Priority = isFPS ? LOW_PRIORITY : HIGH_PRIORITY;
        }

        if (_fpsCamera != null)
        {
            _fpsCamera.Priority = isFPS ? HIGH_PRIORITY : LOW_PRIORITY;
        }

        if (_playerVisualTransform != null && _hideVisualInFPS)
        {
            _playerVisualTransform.gameObject.SetActive(!isFPS);
        }
    }

    private void SetCursorLock(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void ToggleCursorLock()
    {
        bool isLocked = (Cursor.lockState == CursorLockMode.Locked);
        SetCursorLock(!isLocked);
    }
}
