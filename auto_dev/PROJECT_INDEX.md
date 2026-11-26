# Compact Project Code Index (Auto-Generated, 0 Tokens)
Indexed files: `14`

- `Assets/_GameAssets/Scripts/Editor/AutoDev/AutoDevBridge.cs` (282L): types: class AutoDevBridge, class BridgeCommand, class BridgeResult | api: RunFromCommandLine()
- `Assets/_GameAssets/Scripts/Editor/AutoDev/AutoDevVerifier.cs` (296L): types: class AutoDevCheckAttribute: Attribute, struct AutoDevCheckResult, class AutoDevVerifier | api: Pass(string summary, string details = "", ...), Fail(string summary, string details = ""), EnsureSceneOpen(string scenePath), SaveActiveScene(), ReloadCleanScene(string scenePath), InvokeLifecycle(MonoBehaviour target, string methodName), SimulatePhysicsSteps(int steps, float fixedDeltaTime, Acti...), RunCheck(string checkId, string scenePath), RunSmokeCheck(), ExportSceneHierarchySnapshot(string outputPath)
- `Assets/_GameAssets/Scripts/Editor/CivCivAutoSetup.cs` (265L): types: class CivCivAutoSetup | api: SetupAll(), SetupSettingsUI()
- `Assets/_GameAssets/Scripts/Enums/PlayerState.cs` (15L): types: enum PlayerState
- `Assets/_GameAssets/Scripts/ICollectable.cs` (5L): types: interface ICollectable
- `Assets/_GameAssets/Scripts/PlayerController.cs` (607L): types: class PlayerController: MonoBehaviour | api: SetMovementReference(Transform movementReference) | fields: _playerOrientationTransform, _moveSpeed, _maxSpeed, _groundDrag, _airDrag, _airMultiplier, _jumpForce, _jumpCooldown, _slideKey, _maxSlideSpeed
- `Assets/_GameAssets/Scripts/StateController.cs` (25L): types: class StateController: MonoBehaviour | api: ChangeState(PlayerState newState), GetCurrentState()
- `Assets/_GameAssets/Scripts/TargetJump.cs` (21L): types: class TargetJump: MonoBehaviour | fields: jumpForce, jumpHeight
- `Assets/_GameAssets/Scripts/ThirdPersonCameraController.cs` (693L): types: enum CameraPerspective, class ThirdPersonCameraController: MonoBehaviour | api: AutoFindReferences(), TogglePerspective(), ApplyPerspective(CameraPerspective perspective) | fields: _playerTransform, _playerVisualTransform, _orientationTransform, _tpsCamera, _fpsCamera, _rotateWithMouse, _instantTurnInTPS, _tpsRotationSpeed, _instantTurnInFPS, _mouseSensitivity
- `Assets/_GameAssets/Scripts/UI/ClockUI.cs` (115L): types: class ClockUI: MonoBehaviour | fields: timerText, stick, startingSeconds, secondsAddedPerCoinValue, stickMoveSpeed
- `Assets/_GameAssets/Scripts/UI/CoinCollect.cs` (28L): types: class CoinCollect: MonoBehaviour, ICollectable | api: Collect() | fields: value
- `Assets/_GameAssets/Scripts/UI/CoinManager.cs` (38L): types: class CoinManager: MonoBehaviour | events: CoinsChanged, CoinsCollected | api: AddCoins(int amount)
- `Assets/_GameAssets/Scripts/UI/CoinUI.cs` (69L): types: class CoinUI: MonoBehaviour | fields: coinValueText
- `Assets/_GameAssets/Scripts/UI/SettingsUI.cs` (848L): types: class SettingsUI: MonoBehaviour | events: OnMusicToggled, OnSoundToggled, OnMenuStateChanged | api: ToggleSettings(), OpenSettings(), CloseSettings(), ToggleMusic(), ToggleSound(), RestartGame(), QuitGame(), PlayClickSound(), PlaySFX(AudioClip clip, float volume = 1f), AutoResolveReferences() | fields: _settingsButton, _popUpPanel, _closeButton, _musicButton, _soundButton, _restartButton, _quitButton, _musicImage, _soundImage, _activeColor
