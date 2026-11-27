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
    [Tooltip("Orientation nesnesi (İsteğe bağlıdır)")]
    [SerializeField] private Transform _orientationTransform;

    [Header("--- Cinemachine Cameras ---")]
    [Tooltip("3. Şahıs (TPS) Cinemachine Kamerası")]
    [SerializeField] private CinemachineCamera _tpsCamera;
    [Tooltip("1. Şahıs (FPS) Cinemachine Kamerası")]
    [SerializeField] private CinemachineCamera _fpsCamera;

    public CinemachineCamera TPSCamera => _tpsCamera;
    public CinemachineCamera FPSCamera => _fpsCamera;
    public CinemachineCamera ActiveCamera => _currentPerspective == CameraPerspective.FirstPerson ? _fpsCamera : _tpsCamera;
    public CameraPerspective CurrentPerspective => _currentPerspective;

    [Header("--- Mouse Rotation (Fare ile Dönüş) ---")]
    [Tooltip("Karakter fare / kamera yönüne doğru dönsün mü? (TPS ve FPS modlarında aktif)")]
    [SerializeField] private bool _rotateWithMouse = true;
    [Tooltip("TPS modunda anlık dönüş (FPS oyunlarındaki gibi gecikmesiz)")]
    [SerializeField] private bool _instantTurnInTPS = true;
    [Tooltip("TPS modunda karakterin dönüş yumuşaklık hızı (instantTurn kapalıysa)")]
    [SerializeField] private float _tpsRotationSpeed = 20f;
    [Tooltip("FPS modunda anlık dönüş (FPS oyunlarındaki gibi gecikmesiz)")]
    [SerializeField] private bool _instantTurnInFPS = true;
    [SerializeField] private float _mouseSensitivity = 3f;
    [SerializeField] private float _mouseVerticalSensitivity = 2f;
    [SerializeField] private float _minPitch = -60f;
    [SerializeField] private float _maxPitch = 75f;
    [SerializeField] private float _orbitHeight = 1.4f;

    [Header("--- Free Look / Alternative (Sadece _rotateWithMouse KAPALI ise) ---")]
    [Tooltip("Sadece fare dönüşü kapalıysa: Karakter hareket ederken koştuğu yöne dönsün")]
    [SerializeField] private bool _turnCharacterToMovement = false;
    [Tooltip("Sadece fare dönüşü kapalıysa: Karakter hareket ederken kamera arkasına dönsün")]
    [SerializeField] private bool _cameraFollowsCharacterOnMove = false;
    [SerializeField] private float _cameraFollowSpeed = 4f;

    [Header("--- Perspective Settings ---")]
    [Tooltip("Başlangıçtaki kamera modu")]
    [SerializeField] private CameraPerspective _currentPerspective = CameraPerspective.ThirdPerson;
    [Tooltip("Kamera açısını değiştirme tuşu (Varsayılan: V)")]
    [SerializeField] private KeyCode _switchKey = KeyCode.V;
    [Tooltip("FPS modunda karakter modelini gölge moduna al (kafanın ekrana girmesini engellerken yerdeki gölgenin dönmesini sağlar)")]
    [SerializeField] private bool _hideVisualInFPS = true;

    [Header("--- Cursor Settings ---")]
    [Tooltip("Oyun başladığında fare imlecini kilitlesin ve gizlesin mi?")]
    [SerializeField] private bool _lockCursor = true;

    // Aktif kamera öncelikleri (Cinemachine Priority)
    private const int HIGH_PRIORITY = 20;
    private const int LOW_PRIORITY = 10;

    private Camera _mainCamera;
    private Transform _orbitTarget;
    private Transform _movementReference;
    private Transform _fpsLookTarget;
    private PlayerController _playerController;
    private float _cameraYaw;
    private float _cameraPitch;
    private float _lastMouseInputTime = float.NegativeInfinity;

    private void Awake()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null) _mainCamera = FindAnyObjectByType<Camera>();

        // Eksik referansları otomatik toparla ve kök karakteri garantile
        AutoFindReferences();
        EnsureCamerasRuntime();

        if (_playerTransform != null)
        {
            _cameraYaw = _playerTransform.eulerAngles.y;
            _cameraPitch = 0f;
        }
        else if (_tpsCamera != null)
        {
            _cameraYaw = _tpsCamera.transform.eulerAngles.y;
            _cameraPitch = NormalizeAngle(_tpsCamera.transform.eulerAngles.x);
        }

        EnsureOrbitTarget();
        EnsureFpsLookTarget();
        EnsureMovementReference();
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
        HandleMouseLook();
        UpdateCharacterRotation();
    }

    private void LateUpdate()
    {
        UpdateOrbitTargetPosition();
        ApplyOrbitRotation();

        if (_currentPerspective == CameraPerspective.FirstPerson &&
            _fpsCamera != null &&
            _fpsLookTarget != null)
        {
            _fpsCamera.transform.SetPositionAndRotation(
                _fpsLookTarget.position,
                _fpsLookTarget.rotation);
        }
    }

    private void HandleInputs()
    {
        bool switchPressed = false;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.vKey.wasPressedThisFrame) switchPressed = true;
            if (kb.escapeKey.wasPressedThisFrame && !SettingsUI.IsOpen) ToggleCursorLock();
        }
#endif

        try
        {
            if (!switchPressed && Input.GetKeyDown(_switchKey)) switchPressed = true;
            if (Input.GetKeyDown(KeyCode.Escape) && !SettingsUI.IsOpen) ToggleCursorLock();
        }
        catch { }

        // İmleç kilitli değilken ekrana tıklandığında imleci otomatik kilitle (UI üzerinde değilse ve Settings menüsü kapalıysa)
        if (Cursor.lockState != CursorLockMode.Locked && !SettingsUI.IsOpen)
        {
            bool isOverUI = UnityEngine.EventSystems.EventSystem.current != null &&
                            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

            if (!isOverUI)
            {
#if ENABLE_INPUT_SYSTEM
                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    SetCursorLock(true);
                }
#endif
                try
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        SetCursorLock(true);
                    }
                }
                catch { }
            }
        }

        if (switchPressed)
        {
            TogglePerspective();
        }
    }

    private void HandleMouseLook()
    {
        if (!_rotateWithMouse || Cursor.lockState != CursorLockMode.Locked || _playerTransform == null)
        {
            return;
        }

        Vector2 mouseDelta = GetMouseDelta();
        if (mouseDelta.sqrMagnitude >= 0.0001f)
        {
            _lastMouseInputTime = Time.unscaledTime;

            _cameraPitch = Mathf.Clamp(
                _cameraPitch - mouseDelta.y * _mouseVerticalSensitivity,
                _minPitch,
                _maxPitch);

            float yaw = mouseDelta.x * _mouseSensitivity;
            _cameraYaw += yaw;
            _cameraYaw = NormalizeYaw(_cameraYaw);
        }

        ApplyOrbitRotation();

        if (_fpsLookTarget != null)
        {
            _fpsLookTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }
    }

    private void UpdateCharacterRotation()
    {
        if (_playerTransform == null) return;

        bool isFPS = (_currentPerspective == CameraPerspective.FirstPerson);

        // 1. Karakter Gövdesinin Dönüşü (Fare ile dönüş)
        if (_rotateWithMouse)
        {
            Quaternion targetPlayerRot = Quaternion.Euler(0f, _cameraYaw, 0f);

            if (isFPS)
            {
                // FPS modunda karakter gövdesi farenin baktığı yatay yöne anında/kesin döner
                if (_instantTurnInFPS)
                {
                    _playerTransform.rotation = targetPlayerRot;
                }
                else
                {
                    _playerTransform.rotation = Quaternion.Slerp(
                        _playerTransform.rotation,
                        targetPlayerRot,
                        Time.deltaTime * _tpsRotationSpeed);
                }

                if (_fpsLookTarget != null)
                {
                    _fpsLookTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
                }
            }
            else
            {
                // TPS modunda karakter fare / kamera yönüne döner
                if (_instantTurnInTPS || _tpsRotationSpeed <= 0f)
                {
                    _playerTransform.rotation = targetPlayerRot;
                }
                else
                {
                    _playerTransform.rotation = Quaternion.Slerp(
                        _playerTransform.rotation,
                        targetPlayerRot,
                        Time.deltaTime * _tpsRotationSpeed);
                }
            }
        }
        else if (!isFPS && _turnCharacterToMovement)
        {
            // Sadece fare dönüşü bilinçli olarak kapatıldıysa hareket yönüne dön
            Vector3 moveInput = GetMovementInput();
            if (moveInput.sqrMagnitude > 0.01f)
            {
                Quaternion targetCharRot = Quaternion.LookRotation(moveInput);
                _playerTransform.rotation = Quaternion.Slerp(
                    _playerTransform.rotation,
                    targetCharRot,
                    Time.deltaTime * _tpsRotationSpeed);

                if (_cameraFollowsCharacterOnMove && !IsMouseLookActive())
                {
                    float targetYaw = _playerTransform.eulerAngles.y;
                    _cameraYaw = Mathf.LerpAngle(_cameraYaw, targetYaw, Time.deltaTime * _cameraFollowSpeed);
                    ApplyOrbitRotation();
                }
            }
        }

        // 2. Orientation & Movement Reference Senkronizasyonu
        if (_orientationTransform != null)
        {
            _orientationTransform.position = _playerTransform.position;
            _orientationTransform.rotation = Quaternion.Euler(0f, _cameraYaw, 0f);
        }

        if (_movementReference != null)
        {
            _movementReference.position = _playerTransform.position;
            _movementReference.rotation = Quaternion.Euler(0f, _cameraYaw, 0f);
        }
    }

    private Vector3 GetMovementInput()
    {
        float h = 0f;
        float v = 0f;

#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h += 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) h -= 1f;
        }
#endif
        try
        {
            if (Mathf.Approximately(h, 0f) && Mathf.Approximately(v, 0f))
            {
                h = Input.GetAxisRaw("Horizontal");
                v = Input.GetAxisRaw("Vertical");
            }
        }
        catch { }

        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;
        if (_mainCamera != null)
        {
            camForward = _mainCamera.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            camRight = _mainCamera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
        }

        return camForward * v + camRight * h;
    }

    private void EnsureOrbitTarget()
    {
        if (_playerTransform == null)
        {
            return;
        }

        if (_orbitTarget == null)
        {
            GameObject orbitObject = new GameObject("Runtime_CameraOrbitTarget");
            _orbitTarget = orbitObject.transform;
        }

        _orbitTarget.position = _playerTransform.position + Vector3.up * _orbitHeight;
        _orbitTarget.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);

        if (_tpsCamera != null)
        {
            _tpsCamera.Target.TrackingTarget = _orbitTarget;
        }
    }

    private void EnsureFpsLookTarget()
    {
        if (_playerTransform == null)
        {
            return;
        }

        if (_fpsLookTarget == null)
        {
            Transform existing = _playerTransform.Find("Runtime_FPSLookTarget");
            if (existing != null)
            {
                _fpsLookTarget = existing;
            }
            else
            {
                GameObject lookObject = new GameObject("Runtime_FPSLookTarget");
                _fpsLookTarget = lookObject.transform;
                _fpsLookTarget.SetParent(_playerTransform);
                _fpsLookTarget.localPosition = new Vector3(0f, 1.6f, 0.15f);
            }
        }

        _fpsLookTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);

        if (_fpsCamera != null)
        {
            _fpsCamera.Target.TrackingTarget = _fpsLookTarget;
        }
    }

    private void EnsureMovementReference()
    {
        if (_playerTransform == null)
        {
            return;
        }

        _playerController = _playerTransform.GetComponent<PlayerController>();
        if (_playerController == null)
        {
            _playerController = _playerTransform.GetComponentInParent<PlayerController>();
        }

        if (_orientationTransform != null)
        {
            _movementReference = _orientationTransform;
        }
        else if (_movementReference == null)
        {
            GameObject referenceObject = new GameObject("Runtime_MovementReference");
            _movementReference = referenceObject.transform;
        }

        _movementReference.position = _playerTransform.position;
        _movementReference.rotation = Quaternion.Euler(0f, _cameraYaw, 0f);

        if (_playerController != null)
        {
            _playerController.SetMovementReference(_movementReference);
        }
    }

    private void UpdateOrbitTargetPosition()
    {
        if (_orbitTarget == null || _playerTransform == null)
        {
            return;
        }

        _orbitTarget.position = _playerTransform.position + Vector3.up * _orbitHeight;
        if (_movementReference != null)
        {
            _movementReference.position = _playerTransform.position;
            _movementReference.rotation = Quaternion.Euler(0f, _cameraYaw, 0f);
        }
    }

    private void ApplyOrbitRotation()
    {
        if (_orbitTarget == null)
        {
            return;
        }

        _orbitTarget.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
    }

    private bool IsMouseLookActive()
    {
        return Cursor.lockState == CursorLockMode.Locked &&
               Time.unscaledTime - _lastMouseInputTime < 0.2f;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        return angle > 180f ? angle - 360f : angle;
    }

    private static float NormalizeYaw(float yaw)
    {
        yaw %= 360f;
        if (yaw < 0f) yaw += 360f;
        return yaw;
    }

    private Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.delta.ReadValue() * 0.1f;
        }
#endif

        try
        {
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        }
        catch
        {
            return Vector2.zero;
        }
    }

    [ContextMenu("Referansları Otomatik Bağla (Auto Find)")]
    public void AutoFindReferences()
    {
        // 1. Player Transform: Eğer atanmamışsa veya kemiğe/alt nesneye atanmışsa kök karakteri bul
        if (_playerTransform == null || _playerTransform.GetComponent<PlayerController>() == null)
        {
            PlayerController pc = null;
            if (_playerTransform != null)
            {
                pc = _playerTransform.GetComponentInParent<PlayerController>();
            }
            if (pc == null)
            {
                pc = FindAnyObjectByType<PlayerController>();
            }
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
            else if (_playerTransform == null)
            {
                _playerTransform = transform;
            }
        }

        // 2. Player Visual (Mesh / 3D Model)
        if (_playerVisualTransform == null && _playerTransform != null)
        {
            var smr = _playerTransform.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null) _playerVisualTransform = smr.transform;
            else
            {
                var mr = _playerTransform.GetComponentInChildren<MeshRenderer>();
                if (mr != null) _playerVisualTransform = mr.transform;
            }
        }

        // 3. Orientation Transform (İsteğe bağlı)
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
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null) _mainCamera = FindAnyObjectByType<Camera>();
        }

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

        if (_fpsCamera != null)
        {
            if (_fpsCamera.GetComponent<CinemachineRotateWithFollowTarget>() == null)
            {
                var rotComp = _fpsCamera.gameObject.AddComponent<CinemachineRotateWithFollowTarget>();
                rotComp.Damping = 0f;
            }
            if (_fpsCamera.GetComponent<CinemachineHardLockToTarget>() == null)
            {
                var lockComp = _fpsCamera.gameObject.AddComponent<CinemachineHardLockToTarget>();
                lockComp.Damping = 0f;
            }
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

        if (_playerTransform != null)
        {
            _playerTransform.rotation = Quaternion.Euler(0f, _cameraYaw, 0f);
        }

        if (_fpsLookTarget != null)
        {
            _fpsLookTarget.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        }

        if (_tpsCamera != null)
        {
            _tpsCamera.Priority = isFPS ? LOW_PRIORITY : HIGH_PRIORITY;
            _tpsCamera.Prioritize();
            if (!isFPS && _orbitTarget != null)
            {
                _tpsCamera.Target.TrackingTarget = _orbitTarget;
            }
        }

        if (_fpsCamera != null)
        {
            _fpsCamera.Priority = isFPS ? HIGH_PRIORITY : LOW_PRIORITY;
            _fpsCamera.Prioritize();
            if (isFPS && _fpsLookTarget != null)
            {
                _fpsCamera.Target.TrackingTarget = _fpsLookTarget;
            }
        }

        // FPS modunda görsel yönetimi:
        // ShadowsOnly kullanılarak kameranın kafanın içine girmesi engellenir,
        // ancak karakterin yerdeki gölgesi fareyle dönmeye devam eder.
        if (_playerTransform != null)
        {
            Renderer[] renderers = _playerTransform.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (isFPS && _hideVisualInFPS)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
                else
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.enabled = true;
                }
            }
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
